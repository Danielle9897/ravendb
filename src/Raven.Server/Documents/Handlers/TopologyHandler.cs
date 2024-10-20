﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raven.Abstractions.Data;
using Raven.Abstractions.Replication;
using Raven.Client.Http;
using Raven.Server.Json;
using Raven.Server.Routing;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Documents.Handlers
{
    public class TopologyHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/topology", "GET")]
        public Task GetTopology()
        {
            DocumentsOperationContext context;
            using (Database.DocumentsStorage.ContextPool.AllocateOperationContext(out context))
            using ( var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                Document configurationDocument;
                using (context.OpenReadTransaction())
                {
                    configurationDocument = Database.DocumentsStorage.Get(context, Constants.Replication.DocumentReplicationConfiguration);
                }                
                //This is the case where we don't have real replication topology.
                if (configurationDocument == null)
                {
                    GenerateTopology(context, writer);
                    return Task.CompletedTask;
                }
                //here we need to construct the topology from the replication document 
                var replicationDocument = JsonDeserializationServer.ReplicationDocument(configurationDocument.Data);
            
                var nodes = GenerateNodesFromReplicationDocument(replicationDocument);
                
                GenerateTopology(context, writer, nodes, configurationDocument.Etag);
            }
            return Task.CompletedTask;
        }

        private void GenerateTopology(DocumentsOperationContext context, BlittableJsonTextWriter writer, IEnumerable<DynamicJsonValue> nodes = null, long etag = -1)
        {
            context.Write(writer, new DynamicJsonValue
            {
                [nameof(Topology.LeaderNode)] = new DynamicJsonValue
                {
                    [nameof(ServerNode.Url)] = GetStringQueryString("url", required: false) ?? Server.Configuration.Core.ServerUrl,
                    [nameof(ServerNode.Database)] = Database.Name,
                },
                [nameof(Topology.Nodes)] = (nodes == null)? new DynamicJsonArray(): new DynamicJsonArray(nodes),
                [nameof(Topology.ReadBehavior)] =
                ReadBehavior.LeaderWithFailoverWhenRequestTimeSlaThresholdIsReached.ToString(),
                [nameof(Topology.WriteBehavior)] = WriteBehavior.LeaderOnly.ToString(),
                [nameof(Topology.SLA)] = new DynamicJsonValue
                {
                    [nameof(TopologySla.RequestTimeThresholdInMilliseconds)] = 100,
                },
                [nameof(Topology.Etag)] = etag,
            });
        }

        private IEnumerable<DynamicJsonValue> GenerateNodesFromReplicationDocument(ReplicationDocument replicationDocument)
        {
            var destinations = new DynamicJsonValue[replicationDocument.Destinations.Count];
            var etags = new long[replicationDocument.Destinations.Count];
            for (int index = 0; index < replicationDocument.Destinations.Count; index++)
            {
                var des = replicationDocument.Destinations[index];
                if (des.CanBeFailover() == false || des.Disabled || des.IgnoredClient ||
                    des.SpecifiedCollections?.Count > 0)
                    continue;
                etags[index] = Database.DocumentReplicationLoader.GetLastReplicatedEtagForDestination(des) ??
                               -1;
                destinations[index] = new DynamicJsonValue
                {
                    [nameof(ServerNode.Url)] = des.Url,
                    [nameof(ServerNode.ApiKey)] = des.ApiKey,
                    [nameof(ServerNode.Database)] = des.Database
                };
            }

            // We want to have the client failover to the most up to date destination if it needs to, so we sort
            // them by the last replicated etag

            Array.Sort(etags,destinations);
            for (int i = destinations.Length - 1; i >= 0; i--)
            {
                if (destinations[i] != null)
                    yield return destinations[i];
            }
        }
    }
}