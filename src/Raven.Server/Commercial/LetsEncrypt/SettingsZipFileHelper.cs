﻿using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Security;
using Raven.Server.Config;
using Raven.Server.Config.Categories;
using Raven.Server.Utils;
using Raven.Server.Utils.Cli;
using Raven.Server.Utils.Features;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Sparrow.Platform;
using Sparrow.Server.Platform.Posix;
using Sparrow.Threading;
using Sparrow.Utils;
using StudioConfiguration = Raven.Client.Documents.Operations.Configuration.StudioConfiguration;

namespace Raven.Server.Commercial.LetsEncrypt;

public static class SettingsZipFileHelper
{
    internal static async Task<byte[]> GetSetupZipFileSecuredSetup(GetSetupZipFileParameters parameters)
    {
        
        parameters.Progress?.AddInfo("Writing certificates to zip archive.");
        parameters.OnProgress?.Invoke(parameters.Progress);
        
        await using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            using (var context = new JsonOperationContext(1024, 1024 * 4, 32 * 1024, SharedMultipleUseFlag.None))
            {
                try
                {
                    
                    var entry = archive.CreateEntry($"admin.client.certificate.{parameters.CompleteClusterConfigurationResult.Domain}.pfx");

                    // Structure of external attributes field: https://unix.stackexchange.com/questions/14705/the-zip-formats-external-file-attribute/14727#14727
                    // The permissions go into the most significant 16 bits of an int
                    entry.ExternalAttributes = (int)(FilePermissions.S_IRUSR | FilePermissions.S_IWUSR) << 16;

                    await using (var entryStream = entry.Open())
                    {
                        var export = parameters.CompleteClusterConfigurationResult.ClientCert.Export(X509ContentType.Pfx);
                        if (parameters.Token != CancellationToken.None)
                            await entryStream.WriteAsync(export, parameters.Token);
                        else
                            await entryStream.WriteAsync(export, CancellationToken.None);
                    }
                    
                    await LetsEncryptCertificateUtil.WriteCertificateAsPemToZipArchiveAsync(
                        $"admin.client.certificate.{parameters.CompleteClusterConfigurationResult.Domain}",
                        parameters.CompleteClusterConfigurationResult.CertBytes,
                        null,
                        archive);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Failed to write the certificates to a zip archive.", e);
                }

                BlittableJsonReaderObject settingsJson;
                if (parameters.OnSettingsPath != null)
                {
                    string settingsPath = parameters.OnSettingsPath.Invoke();
                    await using (var fs = SafeFileStream.Create(settingsPath, FileMode.Open, FileAccess.Read))
                    {
                        settingsJson = await context.ReadForMemoryAsync(fs, "settings-json");
                    }
                }
                else
                {
                    await using (var ms2 = new MemoryStream())
                    {
                        ms2.WriteByte((byte)'{');
                        ms2.WriteByte((byte)'}');
                        ms2.Position = 0;
                        settingsJson = await context.ReadForMemoryAsync(ms2, "settings-json");
                    }
                }

                settingsJson.Modifications = new DynamicJsonValue(settingsJson);

                if (parameters.SetupMode == SetupMode.LetsEncrypt)
                {
                    settingsJson.Modifications[RavenConfiguration.GetKey(x => x.Security.CertificateLetsEncryptEmail)] = parameters.SetupInfo.Email;

                    try
                    {
                        var licenseString = JsonConvert.SerializeObject(parameters.SetupInfo.License, Formatting.Indented);

                        var entry = archive.CreateEntry("license.json");
                        entry.ExternalAttributes = ((int)(FilePermissions.S_IRUSR | FilePermissions.S_IWUSR)) << 16;

                        await using (var entryStream = entry.Open())
                        await using (var writer = new StreamWriter(entryStream))
                        {
                            await writer.WriteAsync(licenseString);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException("Failed to write license.json in zip archive.", e);
                    }
                }

                settingsJson.Modifications[RavenConfiguration.GetKey(x => x.Core.SetupMode)] = parameters.SetupMode.ToString();

                if (parameters.SetupInfo.EnableExperimentalFeatures)
                {
                    settingsJson.Modifications[RavenConfiguration.GetKey(x => x.Core.FeaturesAvailability)] = FeaturesAvailability.Experimental;
                }

                if (parameters.SetupInfo.Environment != StudioConfiguration.StudioEnvironment.None)
                {
                    if (parameters.OnPutServerWideStudioConfigurationValues != null)
                        await parameters.OnPutServerWideStudioConfigurationValues(parameters.SetupInfo.Environment);
                }

                var certificateFileName = $"cluster.server.certificate.{parameters.CompleteClusterConfigurationResult.Domain}.pfx";
                
                string certPath = parameters.OnGetCertificatePath?.Invoke(certificateFileName);

                if (parameters.SetupInfo.ModifyLocalServer)
                {
                    await using (var certFile = SafeFileStream.Create(certPath, FileMode.Create))
                    {
                        await certFile.WriteAsync(parameters.CompleteClusterConfigurationResult.ServerCertBytes, parameters.Token);
                    } // we'll be flushing the directory when we'll write the settings.json
                }

                settingsJson.Modifications[RavenConfiguration.GetKey(x => x.Security.CertificatePath)] = certPath ?? certificateFileName;
                if (string.IsNullOrEmpty(parameters.SetupInfo.Password) == false)
                    settingsJson.Modifications[RavenConfiguration.GetKey(x => x.Security.CertificatePassword)] = parameters.SetupInfo.Password;

                foreach (var node in parameters.SetupInfo.NodeSetupInfos)
                {
                    var currentNodeSettingsJson = settingsJson.Clone(context);
                    currentNodeSettingsJson.Modifications ??= new DynamicJsonValue(currentNodeSettingsJson);

                    parameters.Progress?.AddInfo($"Creating settings file 'settings.json' for node {node.Key}.");
                    parameters.OnProgress?.Invoke(parameters.Progress);

                    if (node.Value.Addresses.Count != 0)
                    {
                        currentNodeSettingsJson.Modifications[RavenConfiguration.GetKey(x => x.Core.ServerUrls)] =
                            string.Join(";", node.Value.Addresses.Select(ip => IpAddressToUrl(ip, node.Value.Port)));
                        currentNodeSettingsJson.Modifications[RavenConfiguration.GetKey(x => x.Core.TcpServerUrls)] =
                            string.Join(";", node.Value.Addresses.Select(ip => IpAddressToTcpUrl(ip, node.Value.TcpPort)));
                    }

                    var httpUrl = CertificateUtils.GetServerUrlFromCertificate(parameters.CompleteClusterConfigurationResult.ServerCert, parameters.SetupInfo, node.Key, node.Value.Port, node.Value.TcpPort,
                        out var tcpUrl,
                        out var _);

                    if (string.IsNullOrEmpty(node.Value.ExternalIpAddress) == false)
                        currentNodeSettingsJson.Modifications[RavenConfiguration.GetKey(x => x.Core.ExternalIp)] = node.Value.ExternalIpAddress;

                    currentNodeSettingsJson.Modifications[RavenConfiguration.GetKey(x => x.Core.PublicServerUrl)] =
                        string.IsNullOrEmpty(node.Value.PublicServerUrl)
                            ? httpUrl
                            : node.Value.PublicServerUrl;

                    currentNodeSettingsJson.Modifications[RavenConfiguration.GetKey(x => x.Core.PublicTcpServerUrl)] =
                        string.IsNullOrEmpty(node.Value.PublicTcpServerUrl)
                            ? tcpUrl
                            : node.Value.PublicTcpServerUrl;

                    var modifiedJsonObj = context.ReadObject(currentNodeSettingsJson, "modified-settings-json");

                    var indentedJson = JsonStringHelper.Indent(modifiedJsonObj.ToString());
                    if (node.Key == parameters.SetupInfo.LocalNodeTag && parameters.SetupInfo.ModifyLocalServer)
                    {
                        try
                        {
                            parameters.OnWriteSettingsJsonLocally?.Invoke(indentedJson);
                        }
                        catch (Exception e)
                        {
                            throw new InvalidOperationException("Failed to write settings file 'settings.json' for the local sever.", e);
                        }
                    }

                    parameters.Progress?.AddInfo($"Adding settings file for node '{node.Key}' to zip archive.");
                    parameters.OnProgress?.Invoke(parameters.Progress);

                    try
                    {
                        var entry = archive.CreateEntry($"{node.Key}/settings.json");
                        entry.ExternalAttributes = ((int)(FilePermissions.S_IRUSR | FilePermissions.S_IWUSR)) << 16;

                        await using (var entryStream = entry.Open())
                        await using (var writer = new StreamWriter(entryStream))
                        {
                            await writer.WriteAsync(indentedJson);
                        }

                        // we save this multiple times on each node, to make it easier
                        // to deploy by just copying the node
                        entry = archive.CreateEntry($"{node.Key}/{certificateFileName}");
                        entry.ExternalAttributes = (int)(FilePermissions.S_IRUSR | FilePermissions.S_IWUSR) << 16;

                        await using (var entryStream = entry.Open())
                        {
                            await entryStream.WriteAsync(parameters.CompleteClusterConfigurationResult.ServerCertBytes);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException($"Failed to write settings.json for node '{node.Key}' in zip archive.", e);
                    }
                }

                parameters.Progress?.AddInfo("Adding readme file to zip archive.");
                parameters.OnProgress?.Invoke(parameters.Progress);

                string readmeString = CreateReadmeText(parameters.SetupInfo.LocalNodeTag, parameters.CompleteClusterConfigurationResult.PublicServerUrl, parameters.SetupInfo.NodeSetupInfos.Count > 1,
                    parameters.SetupInfo.RegisterClientCert);

                if (parameters.Progress != null)
                {
                    parameters.Progress.Readme = readmeString;
                }

                try
                {
                    var entry = archive.CreateEntry("readme.txt");
                    entry.ExternalAttributes = ((int)(FilePermissions.S_IRUSR | FilePermissions.S_IWUSR)) << 16;

                    await using (var entryStream = entry.Open())
                    await using (var writer = new StreamWriter(entryStream))
                    {
                        await writer.WriteAsync(readmeString);
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Failed to write readme.txt to zip archive.", e);
                }

                parameters.Progress.AddInfo("Adding setup.json file to zip archive.");
                parameters.OnProgress?.Invoke(parameters.Progress);

                try
                {
                    var settings = new SetupSettings {Nodes = parameters.SetupInfo.NodeSetupInfos.Select(tag => new SetupSettings.Node {Tag = tag.Key}).ToArray()};

                    var modifiedJsonObj = context.ReadObject(settings.ToJson(), "setup-json");

                    var indentedJson = JsonStringHelper.Indent(modifiedJsonObj.ToString());

                    var entry = archive.CreateEntry("setup.json");
                    entry.ExternalAttributes = ((int)(FilePermissions.S_IRUSR | FilePermissions.S_IWUSR)) << 16;

                    await using (var entryStream = entry.Open())
                    await using (var writer = new StreamWriter(entryStream))
                    {
                        await writer.WriteAsync(indentedJson);
                        await writer.FlushAsync();
                        await entryStream.FlushAsync(parameters.Token);
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Failed to write setup.json to zip archive.", e);
                }
            }
            return ms.ToArray();
        }
    }
    internal static async Task<byte[]> GetSetupZipFileUnsecuredSetup(GetSetupZipFileParameters parameters)
    {
        
        parameters.Progress?.AddInfo("Writing settings files to zip archive.");
        parameters.OnProgress?.Invoke(parameters.Progress);
        
        await using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            using (var context = new JsonOperationContext(1024, 1024 * 4, 32 * 1024, SharedMultipleUseFlag.None))
            {
                BlittableJsonReaderObject settingsJson;
                if (parameters.OnSettingsPath != null)
                {
                    string settingsPath = parameters.OnSettingsPath.Invoke();
                    await using (var fs = SafeFileStream.Create(settingsPath, FileMode.Open, FileAccess.Read))
                    {
                        settingsJson = await context.ReadForMemoryAsync(fs, "settings-json");
                    }
                }
                else
                {
                    await using (var ms2 = new MemoryStream())
                    {
                        ms2.WriteByte((byte)'{');
                        ms2.WriteByte((byte)'}');
                        ms2.Position = 0;
                        settingsJson = await context.ReadForMemoryAsync(ms2, "settings-json");
                    }
                }

                settingsJson.Modifications = new DynamicJsonValue(settingsJson)
                {
                    [RavenConfiguration.GetKey(x => x.Core.SetupMode)] = parameters.SetupMode.ToString()
                };

                if (parameters.UnsecuredSetupInfo.EnableExperimentalFeatures)
                {
                    settingsJson.Modifications[RavenConfiguration.GetKey(x => x.Core.FeaturesAvailability)] = FeaturesAvailability.Experimental;
                }

                if (parameters.UnsecuredSetupInfo.Environment != StudioConfiguration.StudioEnvironment.None && parameters.ModifyLocalServer)
                {
                    if (parameters.OnPutServerWideStudioConfigurationValues != null)
                        await parameters.OnPutServerWideStudioConfigurationValues(parameters.UnsecuredSetupInfo.Environment);
                }

                foreach (var node in parameters.UnsecuredSetupInfo.NodeSetupInfos)
                {
                    var currentNodeSettingsJson = settingsJson.Clone(context);
                    currentNodeSettingsJson.Modifications ??= new DynamicJsonValue(currentNodeSettingsJson);

                    parameters.Progress?.AddInfo($"Creating settings file 'settings.json' for node {node.Key}.");
                    parameters.OnProgress?.Invoke(parameters.Progress);

                    if (node.Value.Addresses.Count != 0)
                    {
                        currentNodeSettingsJson.Modifications[RavenConfiguration.GetKey(x => x.Core.ServerUrls)] =
                            string.Join(";", node.Value.Addresses.Select(ip => IpAddress(ip, node.Value.Port)));
                        currentNodeSettingsJson.Modifications[RavenConfiguration.GetKey(x => x.Core.TcpServerUrls)] =
                            string.Join(";", node.Value.Addresses.Select(ip => IpAddressToTcp(ip, node.Value.TcpPort)));
                    }
                    
                    var modifiedJsonObj = context.ReadObject(currentNodeSettingsJson, "modified-settings-json");

                    var indentedJson = JsonStringHelper.Indent(modifiedJsonObj.ToString());
                    if (node.Key == parameters.UnsecuredSetupInfo.LocalNodeTag)
                    {
                        try
                        {
                            parameters.OnWriteSettingsJsonLocally?.Invoke(indentedJson);
                        }
                        catch (Exception e)
                        {
                            throw new InvalidOperationException("Failed to write settings file 'settings.json' for the local sever.", e);
                        }
                    }

                    parameters.Progress?.AddInfo($"Adding settings file for node '{node.Key}' to zip archive.");
                    parameters.OnProgress?.Invoke(parameters.Progress);

                    try
                    {
                        var entry = archive.CreateEntry($"{node.Key}/settings.json");
                        entry.ExternalAttributes = ((int)(FilePermissions.S_IRUSR | FilePermissions.S_IWUSR)) << 16;

                        await using (var entryStream = entry.Open())
                        await using (var writer = new StreamWriter(entryStream))
                        {
                            await writer.WriteAsync(indentedJson);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException($"Failed to write settings.json for node '{node.Key}' in zip archive.", e);
                    }
                }

                parameters.Progress?.AddInfo("Adding readme file to zip archive.");
                parameters.OnProgress?.Invoke(parameters.Progress);

                string readmeString = CreateReadmeTextForUnsecured(parameters.UnsecuredSetupInfo.LocalNodeTag,
                    parameters.CompleteClusterConfigurationResult.PublicServerUrl,
                    parameters.UnsecuredSetupInfo.NodeSetupInfos.Count > 1,
                    false);

                if (parameters.Progress != null)
                {
                    parameters.Progress.Readme = readmeString;
                }

                try
                {
                    var entry = archive.CreateEntry("readme.txt");
                    entry.ExternalAttributes = ((int)(FilePermissions.S_IRUSR | FilePermissions.S_IWUSR)) << 16;

                    await using (var entryStream = entry.Open())
                    await using (var writer = new StreamWriter(entryStream))
                    {
                        await writer.WriteAsync(readmeString);
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Failed to write readme.txt to zip archive.", e);
                }

                parameters.Progress?.AddInfo("Adding setup.json file to zip archive.");
                parameters.OnProgress?.Invoke(parameters.Progress);

                try
                {
                    var settings = new SetupSettings {Nodes = parameters.UnsecuredSetupInfo.NodeSetupInfos.Select(tag => new SetupSettings.Node {Tag = tag.Key}).ToArray()};

                    var modifiedJsonObj = context.ReadObject(settings.ToJson(), "setup-json");

                    var indentedJson = JsonStringHelper.Indent(modifiedJsonObj.ToString());

                    var entry = archive.CreateEntry("setup.json");
                    entry.ExternalAttributes = ((int)(FilePermissions.S_IRUSR | FilePermissions.S_IWUSR)) << 16;

                    await using (var entryStream = entry.Open())
                    await using (var writer = new StreamWriter(entryStream))
                    {
                        await writer.WriteAsync(indentedJson);
                        await writer.FlushAsync();
                        await entryStream.FlushAsync(parameters.Token);
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Failed to write setup.json to zip archive.", e);
                }
            }
            return ms.ToArray();
        }
    }


    public static void WriteSettingsJsonLocally(string settingsPath, string json)
    {
        var tmpPath = string.Empty;
        try
        {
            tmpPath = settingsPath + ".tmp";
            using (var file = SafeFileStream.Create(tmpPath, FileMode.Create))
            using (var writer = new StreamWriter(file))
            {
                writer.Write(json);
                writer.Flush();
                file.Flush(true);
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or SecurityException)
        {
            throw new UnsuccessfulFileAccessException(e, tmpPath, FileAccess.Write);
        }

        try
        {
            File.Replace(tmpPath, settingsPath, settingsPath + ".bak");
            if (PlatformDetails.RunningOnPosix)
                Syscall.FsyncDirectoryFor(settingsPath);
        }
        catch (UnauthorizedAccessException e)
        {
            throw new UnsuccessfulFileAccessException(e, settingsPath, FileAccess.Write);
        }
    }

    public static string CreateReadmeTextForUnsecured(string nodeTag, string serverUrl, bool isCluster, bool zipOnly = false)
    {
        var str =
            string.Format(WelcomeMessage.AsciiHeader, Environment.NewLine) + Environment.NewLine + Environment.NewLine +
            "RavenDB Setup package has been downloaded successfully." + Environment.NewLine +
            "Your cluster settings configuration, are contained in the downloaded zip file."
            + Environment.NewLine;

        str += Environment.NewLine +
               $"The new server is available at: {serverUrl}"
               + Environment.NewLine;

        if (zipOnly)
        {
            str += "You can use this file to configure your RavenDB cluster in the Cloud or any other environment of your choice other than this machine.";
            return str;
        }
        
        str += $"The current node ('{nodeTag}') has already been configured and requires no further action on your part." +
               Environment.NewLine;

        if (isCluster)
        {
            str +=
                Environment.NewLine +
                "You are setting up a cluster. The cluster topology and node addresses have already been configured." +
                Environment.NewLine +
                "The next step is to download a new RavenDB server for each of the other nodes." +
                Environment.NewLine +
                Environment.NewLine +
                "When you enter the setup wizard on a new node, please choose 'Continue Existing Cluster Setup'." +
                Environment.NewLine +
                "Do not try to start a new setup process again in this new node, it is not supported." +
                Environment.NewLine +
                "You will be asked to upload the zip file which was just downloaded." +
                Environment.NewLine +
                "The new server node will join the already existing cluster." +
                Environment.NewLine +
                Environment.NewLine +
                "When the wizard is done and the new node was restarted, the cluster will automatically detect it. " +
                Environment.NewLine +
                "There is no need to manually add it again from the studio. Simply access the 'Cluster' view and " +
                Environment.NewLine +
                "observe the topology being updated." +
                Environment.NewLine;
        }

        return str;
        
    }
    // TODO:
    // 1. Call this method w/ relevant 'zipOnly' param
    // 2. Pass relevant 'publicServerUrl' when this is Unsecure
    public static string CreateReadmeText(string nodeTag, string publicServerUrl, bool isCluster, bool registerClientCert, bool zipOnly = false)
    {
        var str =
            string.Format(WelcomeMessage.AsciiHeader, Environment.NewLine) + Environment.NewLine + Environment.NewLine +
            "RavenDB Setup package has been downloaded successfully." + Environment.NewLine +
            "Your cluster settings configuration, and the certificate (if in Secure Mode), are contained in the downloaded zip file."
            + Environment.NewLine;

        str += Environment.NewLine +
               $"The new server is available at: {publicServerUrl}"
               + Environment.NewLine;

        if (zipOnly)
        {
            str += "You can use this file to configure your RavenDB cluster in the Cloud or any other environment of your choice other than this machine.";
            return str;
        }
        
        str += $"The current node ('{nodeTag}') has already been configured and requires no further action on your part." +
               Environment.NewLine;

        str += Environment.NewLine;
        if (registerClientCert && PlatformDetails.RunningOnPosix == false)
        {
            str +=
                $"An administrator client certificate has been installed on this machine ({Environment.MachineName})." +
                Environment.NewLine +
                $"You can now restart the server and access the studio at {publicServerUrl}." +
                Environment.NewLine +
                "Chrome will let you select this certificate automatically. " +
                Environment.NewLine +
                "If it doesn't, you will get an authentication error. Please restart all instances of Chrome to make sure nothing is cached." +
                Environment.NewLine;
        }
        else
        {
            str +=
                "An administrator client certificate has been generated and is located in the zip file." +
                Environment.NewLine +
                $"However, the certificate was not installed on this machine ({Environment.MachineName}), this can be done manually." +
                Environment.NewLine;
        }

        str +=
            "If you are using Firefox (or Chrome under Linux), the certificate must be imported manually to the browser." +
            Environment.NewLine +
            "You can do that via: Tools > Options > Advanced > 'Certificates: View Certificates'." +
            Environment.NewLine;

        if (PlatformDetails.RunningOnPosix)
            str +=
                "In Linux, importing the client certificate to the browser might fail for 'Unknown Reasons'." +
                Environment.NewLine +
                "If you encounter this bug, use the RavenCli command 'generateClientCert' to create a new certificate with a password." +
                Environment.NewLine +
                "For more information on this workaround, read the security documentation in 'ravendb.net'." +
                Environment.NewLine;

        str +=
            Environment.NewLine +
            "It is recommended to generate additional certificates with reduced access rights for applications and users." +
            Environment.NewLine +
            "This can be done using the RavenDB Studio, in the 'Manage Server' > 'Certificates' page." +
            Environment.NewLine;

        if (isCluster)
        {
            str +=
                Environment.NewLine +
                "You are setting up a cluster. The cluster topology and node addresses have already been configured." +
                Environment.NewLine +
                "The next step is to download a new RavenDB server for each of the other nodes." +
                Environment.NewLine +
                Environment.NewLine +
                "When you enter the setup wizard on a new node, please choose 'Continue Existing Cluster Setup'." +
                Environment.NewLine +
                "Do not try to start a new setup process again in this new node, it is not supported." +
                Environment.NewLine +
                "You will be asked to upload the zip file which was just downloaded." +
                Environment.NewLine +
                "The new server node will join the already existing cluster." +
                Environment.NewLine +
                Environment.NewLine +
                "When the wizard is done and the new node was restarted, the cluster will automatically detect it. " +
                Environment.NewLine +
                "There is no need to manually add it again from the studio. Simply access the 'Cluster' view and " +
                Environment.NewLine +
                "observe the topology being updated." +
                Environment.NewLine;
        }

        return str;
    }
    
    internal static string IpAddress(string address, int port)
    {
        var url = "http://" + address;
        if (port != 0)
            url += ":" + port;
        return url;
    }
    
    private static string IpAddressToTcp(string address, int port)
    {
        var url = "tcp://" + address;
        if (port != 0)
            url += ":" + port;
        return url;
    }
    
    private static string IpAddressToUrl(string address, int port)
    {
        var url = "https://" + address;
        if (port != 443)
            url += ":" + port;
        return url;
    }

    private static string IpAddressToTcpUrl(string address, int port)
    {
        var url = "tcp://" + address;
        if (port != 0)
            url += ":" + port;
        return url;
    }
}
