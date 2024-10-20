﻿using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Raven.Server.Routing;
using Sparrow.Logging;

namespace Raven.Server.Documents.Handlers.Admin
{
    public class AdminLogsHandler : AdminRequestHandler
    {
        [RavenAction("/admin/logs/watch", "GET", "/admin/logs/watch")]
        public async Task RegisterForLogs()
        {
            using (var socket = await HttpContext.WebSockets.AcceptWebSocketAsync())
            {
                var context = new LoggingSource.WebSocketContext();

                foreach (var filter in HttpContext.Request.Query["only"])
                {
                    context.Filter.Add(filter, true);
                }
                foreach (var filter in HttpContext.Request.Query["except"])
                {
                    context.Filter.Add(filter, false);
                }

                await LoggingSource.Instance.Register(socket, context, ServerStore.ServerShutdown);
            }
        }

    }
}