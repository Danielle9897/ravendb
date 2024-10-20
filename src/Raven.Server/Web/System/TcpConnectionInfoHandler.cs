﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Raven.Server.Routing;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Web.System
{
    public class TcpConnectionInfoHandler : RequestHandler
    {
        [RavenAction("/info/tcp", "GET", "tcp-connections-state", NoAuthorizationRequired = true)]
        public async Task Get()
        {
            JsonOperationContext context;
            using (ServerStore.ContextPool.AllocateOperationContext(out context))
            using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                var tcpListenerStatus = await Server.GetTcpServerStatusAsync();

                string host = HttpContext.Request.Host.Host;
                if (string.IsNullOrWhiteSpace(Server.Configuration.Core.TcpServerUrl) == false)
                    host = new UriBuilder(Server.Configuration.Core.TcpServerUrl).Host;

                var output = new DynamicJsonValue
                {
                    ["Url"] = new UriBuilder("tcp", host, tcpListenerStatus.Port).Uri.ToString()
                };

                context.Write(writer, output);
            }
        }
    }
}