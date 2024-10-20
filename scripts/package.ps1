$NETSTANDARD13 = 'netstandard1.3'
$NET46 = 'net46'
$SUPPORTED_CLIENT_FRAMEWORKS = @( $NETSTANDARD13 )
$SUPPORTED_NEW_CLIENT_FRAMEWORKS = @( $NETSTANDARD13 )

function CreateArchiveFromDir ( $targetFilename, $dir, $spec ) {
    if ($spec.PkgType -eq "zip") {
        ZipFilesFromDir $targetFilename $dir
    } elseif ($spec.PkgType -eq "tar.bz2") {
        TarBzFilesFromDir $targetFilename $dir
    } elseif ($spec.PkgType -eq "tar") {
        TarFilesFromDir $targetFilename $dir
    } else {
        throw "Unknown archive method for $targetFilename"
    }
}

function ZipFilesFromDir( $targetFilename, $sourceDir )
{
    $toZipGlob = [io.path]::combine($sourceDir, '*')
    $zipFile = "$targetFilename.zip"
    Compress-Archive -Path "$toZipGlob" -DestinationPath "$zipFile"
}

function TarBzFilesFromDir ( $targetFilename, $sourceDir ) {
    $glob = [io.path]::combine($sourceDir, '*')
    if ($($IsWindows -eq $False) -and $(Get-Command "tar" -ErrorAction SilentlyContinue))
    {
        & tar -C $sourceDir -cjvf "$targetFilename.tar.bz2" .
        CheckLastExitCode
    }
    else
    {
        $7za = [io.path]::combine("scripts", "assets", "bin", "7za.exe")
        & "$7za" a -ttar "$targetFilename.tar" $glob
        CheckLastExitCode
        & "$7za" a -tbzip2 "$targetFilename.tar.bz2" "$targetFilename.tar"
        CheckLastExitCode
        rm "$targetFilename.tar"
        CheckLastExitCode
    }
}

function TarFilesFromDir ( $targetFilename, $sourceDir ) {
    $glob = [io.path]::combine($sourceDir, '*')
    if ($($IsWindows -eq $False) -and $(Get-Command "tar" -ErrorAction SilentlyContinue))
    {
        & tar -C $sourceDir -cvf "$targetFilename.tar" .
        CheckLastExitCode
    }
    else
    {
        $7za = [io.path]::combine("scripts", "assets", "bin", "7za.exe")
        & "$7za" a -ttar "$targetFilename.tar" $glob
        CheckLastExitCode
    }
}

function CreateRavenPackage ( $projectDir, $releaseDir, $outDirs, $spec, $version ) {
    write-host "Create package for $($spec.runtime)..."
    $packageDir = [io.path]::combine($outDirs.Main, "package")
    New-Item -ItemType Directory -Path $packageDir

    CreatePackageLayout $packageDir $projectDir $outDirs $spec

    $releaseArchiveFile = GetRavenArchiveFileName $version $spec
    $releaseArchivePath = [io.path]::combine($releaseDir, $releaseArchiveFile)
    CreateArchiveFromDir $releaseArchivePath $packageDir $spec
}

function GetRavenArchiveFileName ( $version, $spec ) {
    "RavenDB-$version-$($spec.Name)"
}

function CreatePackageLayout ( $packageDir, $projectDir, $outDirs, $spec ) {
    if ($spec.Name.Contains('raspberry-pi') -eq $False) {
        LayoutRegularPackage $packageDir $projectDir $outDirs $spec
    } else {
        LayoutRaspberryPiPackage $packageDir $projectDir $outDirs $spec
    }
}

function LayoutRegularPackage ( $packageDir, $projectDir, $outDirs, $spec ) {
    CopyStudioPackage $outDirs
    CopyLicenseFile $packageDir
    CopyAckFile $packageDir
    CreatePackageServerLayout $projectDir $($outDirs.Server) $packageDir $spec
    CreatePackageClientLayout $outDirs $packageDir $projectDir $spec
    CopyClientReadMe $(Join-Path $packageDir -ChildPath 'Client')
}

function LayoutRaspberryPiPackage ( $packageDir, $projectDir, $outDirs, $spec ) {
    CopyStudioPackage $outDirs
    CopyLicenseFile $packageDir
    CopyAckFile $packageDir
    CreatePackageServerLayout $projectDir $($outDirs.Server) $packageDir $spec
    CreatePackageClientLayout $outDirs $packageDir $projectDir $spec

    CopyDaemonScripts $projectDir $packageDir
    CopyLinuxScripts $projectDir $packageDir
    CreateRavenDBTarForRaspberryPi $projectDir $packageDir
    CopyDotnetTarForRaspberryPi $packageDir
    $wrapperDir = Join-Path $packageDir -ChildPath "RavenDB.4.0"
    $rpiPkgContents = Get-ChildItem -Path $packageDir;
    New-Item -ItemType Directory -Path $wrapperDir

    Push-Location
    cd $packageDir
    foreach ($item in $rpiPkgContents) {
        Move-Item $item $wrapperDir
    }
    Pop-Location
}

function CopyStudioPackage ( $outDirs ) {
    $studioZipPath = [io.path]::combine($outDirs.Studio, "Raven.Studio.zip")
    $dst = $outDirs.Server
    write-host "Copying Studio package from $studioZipPath to $dst"
    Copy-Item "$studioZipPath" -Destination $dst
    CheckLastExitCode
}

function CopyLinuxScripts ( $projectDir, $packageDir ) {
    write-host "Copy Linux scripts..."

    $scriptsDir = [io.path]::combine($projectDir, "scripts", "raspberry-pi")
    $scriptsList = @( "start.sh", "setup.sh")

    Foreach ($scriptName in $scriptsList) {
        $scriptPath = Join-Path $scriptsDir -ChildPath $scriptName

        Copy-Item "$scriptPath" -Destination "$packageDir"
        CheckLastExitCode

        if ($(Get-Command "chmod" -ErrorAction SilentlyContinue)) {
            $scriptInPkgPath = Join-Path $packageDir -ChildPath $scriptName
            & "chmod" a+x $scriptInPkgPath
            CheckLastExitCode
        }
    }
}

function CopyDaemonScripts ( $projectDir, $packageDir ) {
    write-host "Copy daemon files..."

    $scriptsDir = [io.path]::combine($projectDir, "scripts", "raspberry-pi")
    $scriptsList = @( "ravendbd", "ravendb.watchdog.sh" )

    Foreach ($scriptName in $scriptsList) {
        $scriptPath = Join-Path $scriptsDir -ChildPath $scriptName
        Copy-Item "$scriptPath" -Destination "$packageDir"
        CheckLastExitCode
    }
}

function CreatePackageServerLayout ( $projectDir, $serverOutDir, $packageDir, $spec ) {
    write-host "Create package server directory layout..."

    $settingsFileName = If ($spec.IsUnix) { "settings_posix.json" } Else { "settings_windows.json" }
    $settingsFilePath = [io.path]::combine($projectDir, 'src', 'Raven.Server', $settingsFileName)

    Copy-Item "$settingsFilePath" $serverOutDir

    if ($spec.Name -eq "raspberry-pi") {
        del $([io.path]::combine($serverOutDir, "*.so"))
        del $([io.path]::combine($serverOutDir, "*.ni.*"))
    }

    Copy-Item "$serverOutDir" -Recurse -Destination "$packageDir"
}

function CreatePackageClientLayout ( $outDirs, $packageDir, $projectDir, $spec ) {
    if ($spec.Name.Contains("raspberry-pi")) {
        CreateRaspberryPiClientLayout $outDirs $packageDir $projectDir
    } else {
        CreateRegularPackageClientLayout $outDirs $packageDir $projectDir
    }
}

function CreateRaspberryPiClientLayout ( $outDirs, $packageDir, $projectDir ) {
    $clientOutDir = [io.path]::combine($outDirs.Client, $NETSTANDARD13)
    $clientPkgDir = [io.path]::combine($packageDir, "Client")
    New-Item -ItemType Directory -Path $clientPkgDir

    Copy-Item "$(Join-Path $clientOutDir -ChildPath "Raven.Client.dll")" -Destination "$clientPkgDir"
    Copy-Item "$(Join-Path $clientOutDir -ChildPath "Raven.Client.pdb")" -Destination "$clientPkgDir"
    Copy-Item "$(Join-Path $clientOutDir -ChildPath "Sparrow.dll")" -Destination "$clientPkgDir"
    Copy-Item "$(Join-Path $clientOutDir -ChildPath "Sparrow.pdb")" -Destination "$clientPkgDir"

    $newClientOutDir = [io.path]::combine($outDirs.NewClient, $NETSTANDARD13)
    Copy-Item "$(Join-Path $newClientOutDir -ChildPath "Raven.NewClient.dll")" -Destination "$clientPkgDir"
    Copy-Item "$(Join-Path $newClientOutDir -ChildPath "Raven.NewClient.pdb")" -Destination "$clientPkgDir"
}

function CopyDotnetTarForRaspberryPi ( $packageDir ) {
    DownloadDotnetForRPi $packageDir
    CheckLastExitCode
}

function CreateRavenDBTarForRaspberryPi ( $projectDir, $packageDir ) {
    $targetFilename = "ravendb.4.0"

    $wrapperDir = Join-Path $packageDir -ChildPath $targetFilename
    New-Item -ItemType Directory -Path $wrapperDir

    Push-Location
    cd $packageDir

    $rpiRdbTarContents = ( "Client", "Server", "acknowledgements.txt", "license.txt", "ravendbd", "ravendb.watchdog.sh" )

    foreach ($item in $rpiRdbTarContents) {
        Move-Item $item $wrapperDir
    }

    if ($($IsWindows -eq $False) -and $(Get-Command "tar" -ErrorAction SilentlyContinue))
    {
        & tar -cjvf "$targetFilename.tar.bz2" "$targetFilename"
        CheckLastExitCode
    }
    else
    {
        $7za = [io.path]::combine($projectDir, "scripts", "assets", "bin", "7za.exe")
        & "$7za" a -ttar "$targetFilename.tar" "$targetFilename"
        CheckLastExitCode

        & "$7za" a -tbzip2 "$targetFilename.tar.bz2" "$targetFilename.tar"
        CheckLastExitCode

        Remove-Item "$targetFilename.tar"
    }

    Remove-Item -Recurse "$targetFilename"
    Pop-Location
}

function CreateRegularPackageClientLayout( $outDirs, $packageDir, $projectDir) {
    write-host "Create package client directory layout..."

    $assetsDir = [io.path]::combine($projectDir, "scripts", "assets", "pkg")

    foreach ($framework in $SUPPORTED_CLIENT_FRAMEWORKS) {
        $frameworkClientOutDir = [io.path]::combine($outDirs.Client, $framework)
        CopyClient $frameworkClientOutDir $packageDir $assetsDir $framework
    }

    foreach ($framework in $SUPPORTED_NEW_CLIENT_FRAMEWORKS) {
        $frameworkNewClientOutDir = [io.path]::combine($outDirs.NewClient, $framework)
        CopyNewClient $frameworkNewClientOutDir $packageDir $assetsDir $framework
    }

    $sparrowFrameworks = $($SUPPORTED_CLIENT_FRAMEWORKS + $SUPPORTED_NEW_CLIENT_FRAMEWORKS) | select -uniq

    foreach ($framework in $sparrowFrameworks) {
        $frameworkOutDir = [io.path]::combine($outDirs.Sparrow, $framework)
        CopySparrow $frameworkOutDir $packageDir $assetsDir $framework
    }

}

function CopyClient ( $clientOutDir, $packageDir, $assetsDir, $framework ) {
    $ravenClientAssetsDir = [io.path]::combine($assetsDir, "Raven.Client")
    $ravenClientDir = [io.path]::combine($packageDir, 'Client', $framework, 'Raven.Client')
    $ravenClientDllDir = [io.path]::combine($ravenClientDir, $framework)
    New-Item -ItemType Directory -Path $ravenClientDllDir

    Copy-Item "$(Join-Path $clientOutDir -ChildPath "Raven.Client.dll")" -Destination "$ravenClientDllDir"
    Copy-Item "$(Join-Path $clientOutDir -ChildPath "Raven.Client.pdb")" -Destination "$ravenClientDllDir"

    $ravenClientProjectTemplate = Get-Content -Raw -Path $(Join-Path $ravenClientAssetsDir "project.json.template") | ConvertFrom-Json
    $ravenClientProjectOrig = Get-Content -Raw -Path $([io.path]::combine($projectDir, "src", "Raven.Client",  "project.json")) | ConvertFrom-Json
    $ravenClientProjectTemplate.dependencies = $ravenClientProjectOrig.dependencies
    $ravenClientProjectTemplate.dependencies.Sparrow = $ravenClientProjectOrig.dependencies.Sparrow.version
    $ravenClientProjectTemplate `
    | ConvertTo-Json -Depth 100 `
    | Out-File $(Join-Path $ravenClientDir -ChildPath "project.json") -Encoding UTF8

    Copy-Item "$([io.path]::combine($projectDir, "src", "Raven.Client",  "Raven.Client.xproj"))" -Destination "$(Join-Path $ravenClientDir -ChildPath  "Raven.Client.xproj")"
}

function CopyNewClient ( $clientOutDir, $packageDir, $assetsDir, $framework ) {
    if (IsFullFramework $framework) {
        $ravenClientDllDir = [io.path]::combine($packageDir, 'Client', $framework)
        New-Item -ItemType Directory -Path $ravenClientDllDir

        Copy-Item "$(Join-Path $clientOutDir -ChildPath "*")" -Recurse -Destination "$ravenClientDllDir"
        CheckLastExitCode
    } else {
        $ravenClientAssetsDir = [io.path]::combine($assetsDir, "Raven.NewClient")
        $ravenClientDir = [io.path]::combine($packageDir, 'Client', $framework, 'Raven.NewClient')
        $ravenClientDllDir = [io.path]::combine($ravenClientDir, $framework)
        New-Item -ItemType Directory -Path $ravenClientDllDir

        Copy-Item "$(Join-Path $clientOutDir -ChildPath "Raven.NewClient.dll")" -Destination "$ravenClientDllDir"
        Copy-Item "$(Join-Path $clientOutDir -ChildPath "Raven.NewClient.pdb")" -Destination "$ravenClientDllDir"

        $ravenClientProjectTemplate = Get-Content -Raw -Path $(Join-Path $ravenClientAssetsDir "project.json.template") | ConvertFrom-Json
        $ravenClientProjectOrig = Get-Content -Raw -Path $([io.path]::combine($projectDir, "src", "Raven.NewClient",  "project.json")) | ConvertFrom-Json
        $ravenClientProjectTemplate.dependencies = $ravenClientProjectOrig.dependencies
        $ravenClientProjectTemplate.dependencies.Sparrow = $ravenClientProjectOrig.dependencies.Sparrow.version
        $ravenClientProjectTemplate `
        | ConvertTo-Json -Depth 100 `
        | Out-File $(Join-Path $ravenClientDir -ChildPath "project.json") -Encoding UTF8

        Copy-Item "$([io.path]::combine($projectDir, "src", "Raven.NewClient", "Raven.NewClient.xproj"))" -Destination "$(Join-Path $ravenClientDir -ChildPath  "Raven.NewClient.xproj")"
    }
}

function IsFullFramework ($fwname) {
    $fwname -eq $NET46
}

function CopySparrow ( $sparrowOutDir, $packageDir, $assetsDir, $framework ) {
    if (IsFullFramework $framework) {
        return
    } else {
        $sparrowAssetsDir = [io.path]::combine($assetsDir, "Sparrow")
        $sparrowDir = [io.path]::combine($packageDir, 'Client', $framework, 'Sparrow')
        $sparrowDllDir = [io.path]::combine($packageDir, 'Client', $framework, 'Sparrow', $framework)
        New-Item -ItemType Directory -Path $sparrowDllDir

        Copy-Item "$(Join-Path $sparrowOutDir -ChildPath "Sparrow.dll")" -Destination "$sparrowDllDir"
        Copy-Item "$(Join-Path $sparrowOutDir -ChildPath "Sparrow.pdb")" -Destination "$sparrowDllDir"
        Copy-Item "$([io.path]::combine($projectDir, "src", "Sparrow", "Sparrow.xproj"))" -Destination "$(Join-Path $sparrowDir -ChildPath  "Sparrow.xproj")"
        Copy-Item "$(Join-Path $sparrowAssetsDir -ChildPath "project.json.template")" -Destination "$(Join-Path $sparrowDir -ChildPath "project.json")"
        CheckLastExitCode
    }
}
