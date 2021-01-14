using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Raven.Server.Json;
using Raven.Server.ServerWide.Context;
using Raven.Server.Utils.Stats;
using Sparrow.Json;

namespace Raven.Server.Documents.Subscriptions
{
    public enum SubscriptionInfoType
    {
        ClientConnected,
        BatchCompleted,
        ClientAcknowledge,
        ClientDisconnected
    }

    // *** Run ****************
    public class SubscriptionRunStats // statistics of a single run of a batch
    {
        public string ClientUri;
        public SubscriptionInfoType InfoType { get; set; }
     
        public DateTime Started { get; set; }
        public DateTime Completed { get; set; }
        
        public long NumberOfDocumentsInBatch { get; set; }
        public long SizeOfDocumentsInBatch { get; set; }
    }
    
    // *** Perf ****************
    public class SubscriptionPerformanceStats // this is the obj to ws (as part of a list in SubscriptionStats object)
    {
        public int Id { get; set; } // todo ???
        public double DurationInMs { get; }
        
        public SubscriptionInfoType InfoType { get; set; }
        public string ClientUri { get; set; }
        
        public DateTime Started { get; set; }
        public DateTime Completed { get; set; }
        
        public SubscriptionPerformanceOperation Details { get; set; }
        
        public long NumberOfDocumentsInBatch { get; set; }
        public long SizeOfDocumentsInBatch { get; set; }
        
        public SubscriptionPerformanceStats(TimeSpan duration)
        {
            DurationInMs = Math.Round(duration.TotalMilliseconds, 2);
        }
    }
    
    // *** Performance Operation
    public class SubscriptionPerformanceOperation
    {
        public string Name { get; set; }
        public double DurationInMs { get; }
        public SubscriptionPerformanceOperation[] Operations { get; set; }
        
        public SubscriptionPerformanceOperation(TimeSpan duration)
        {
            DurationInMs = Math.Round(duration.TotalMilliseconds, 2);
            Operations = new SubscriptionPerformanceOperation[0];
        }
    }
    
    // *** Aggregator ******************
    public class SubscriptionStatsAggregator : StatsAggregator<SubscriptionRunStats, SubscriptionStatsScope>
    {
        // For subscription we have a single scope
        private volatile SubscriptionPerformanceStats _performanceStats;

        public SubscriptionStatsAggregator(int id, StatsAggregator<SubscriptionRunStats, SubscriptionStatsScope> lastStats) : base(id, lastStats)
        {
        }

        public override SubscriptionStatsScope CreateScope()
        {
            Debug.Assert(Scope == null);
            return Scope = new SubscriptionStatsScope(Stats);
        }

        public SubscriptionPerformanceStats ToPerformanceStats()
        {
            if (_performanceStats != null)
                return _performanceStats;

            lock (Stats)
            {
                if (_performanceStats != null)
                    return _performanceStats;

                return _performanceStats = CreatePerformanceStats(completed: true);
            }
        }
        
        private SubscriptionPerformanceStats CreatePerformanceStats(bool completed) 
        {
            return new SubscriptionPerformanceStats(Scope.Duration)
            {
                InfoType = Stats.InfoType,
                ClientUri = Stats.ClientUri,
                
                Started = Stats.Started,
                Completed = Stats.Completed,
                
                Details = Scope.ToPerformanceOperation("Subscription"),
                
                NumberOfDocumentsInBatch = Stats.NumberOfDocumentsInBatch,
                SizeOfDocumentsInBatch = Stats.SizeOfDocumentsInBatch,
            };
        }
    }

    // *** Scope *****************
    public class SubscriptionStatsScope : StatsScope<SubscriptionRunStats, SubscriptionStatsScope>
    {
        private readonly SubscriptionRunStats _stats;

        public SubscriptionInfoType InfoType => _stats.InfoType;
        public string ClientUri => _stats.ClientUri;
        
        // public DateTime BatchStartTime => _stats.Started;
        // public DateTime BatchEndTime => _stats.Completed;
        public DateTime Started => _stats.Started;
        public DateTime Completed => _stats.Completed;
        
        public long NumberOfDocumentsInBatch => _stats.NumberOfDocumentsInBatch;
        public long SizeOfDocumentsInBatch => _stats.SizeOfDocumentsInBatch;
        
        public SubscriptionStatsScope(SubscriptionRunStats stats, bool start = true) : base(stats, start)
        {
            _stats = stats;
        }

        protected override SubscriptionStatsScope OpenNewScope(SubscriptionRunStats stats, bool start)
        {
            return new SubscriptionStatsScope(stats, start);
        }

        public void RecordClientConnect(DateTime connectTime, string clientUri)
        {
            _stats.ClientUri = clientUri;
            _stats.InfoType = SubscriptionInfoType.ClientConnected;
            
            // // todo... take from param...
            // _stats.Started = DateTime.UtcNow;
            // _stats.Completed = DateTime.UtcNow;
            _stats.Started = connectTime;
            _stats.Completed = connectTime;
        }
        
        public void RecordBatchInfo(long docsCount, long docsSize, DateTime startTime, DateTime endTime, string clientUri)
        {
            _stats.ClientUri = clientUri;
            _stats.InfoType = SubscriptionInfoType.BatchCompleted;
            
            _stats.NumberOfDocumentsInBatch = docsCount;
            _stats.SizeOfDocumentsInBatch = docsSize;
            
            _stats.Started = startTime;
            _stats.Completed = endTime;
        }
        
        public void RecordClientAcknowledge(DateTime ackTime, string clientUri)
        {
            _stats.ClientUri = clientUri;
            _stats.InfoType = SubscriptionInfoType.ClientAcknowledge;
            
            // todo... take from param...
            // _stats.Started = DateTime.UtcNow;
            // _stats.Completed = DateTime.UtcNow;
            _stats.Started = ackTime;
            _stats.Completed = ackTime;
        }
        
        public void RecordClientDisconnect(DateTime disconnectTime, string clientUri)
        {
            _stats.ClientUri = clientUri;
            _stats.InfoType = SubscriptionInfoType.ClientDisconnected;
            
            // todo... take from param...
            // _stats.Started = DateTime.UtcNow;
            // _stats.Completed = DateTime.UtcNow;
            _stats.Started = disconnectTime;
            _stats.Completed = disconnectTime;
        }
        
        public SubscriptionPerformanceOperation ToPerformanceOperation(string name)
        {
            var operation = new SubscriptionPerformanceOperation(Duration)
            {
                Name = name
            };

            if (Scopes != null)
            {
                operation.Operations = Scopes
                    .Select(x => x.Value.ToPerformanceOperation(x.Key))
                    .ToArray();
            }

            return operation;
        }
    }
    
    // *** For collector ******************
    public class SubscriptionStats // this class is parallel to EtlTaskPerformanceStats
    {
        public long TaskId { get; set;  }
        public string TaskName { get; set; }
        public SubscriptionPerformanceStats[] TaskStats { get; set; }

        public SubscriptionStats()
        {
            TaskStats = Array.Empty<SubscriptionPerformanceStats>();
        }
    }
     
    // *** Collector ******************
   public class LiveSubscriptionPerformanceCollector : DatabaseAwareLivePerformanceCollector<SubscriptionStats>
    {
        // dictionary from <subscription name> to <object that has subscription process & list of events on this subscription process>
        private readonly ConcurrentDictionary<string, SubscriptionAndStats> _perSubscriptionStats = new ConcurrentDictionary<string, SubscriptionAndStats>();

        public LiveSubscriptionPerformanceCollector(DocumentDatabase documentDatabase, IEnumerable<string> subscriptionNames) : base(documentDatabase)
        {
            using (Database.ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            using (context.OpenReadTransaction())
            {
                foreach (var name in subscriptionNames)
                {
                    var subscription = Database.SubscriptionStorage.GetSubscriptionConnection(context, name);
                    _perSubscriptionStats.TryAdd(name, new SubscriptionAndStats(subscription));
                }
            }

            Start();
        }

        protected override async Task StartCollectingStats()
        {
            Database.OnSubscriptionEvent += OnSubscriptionEvent;

            // try was here - moved to below...
            var stats = Client.Extensions.EnumerableExtension.ForceEnumerateInThreadSafeManner(_perSubscriptionStats)
                .Select(item =>
                {
                    var stats = new SubscriptionStats()
                    {
                        TaskName = item.Value.SubscriptionTaskName,
                        TaskStats = item.Value.Handler.Connection.GetPerformanceStats()
                    };
                    
                    return stats;
                });

            Stats.Enqueue(stats.ToList()); // expects list of SubscriptionStats
                
            try
            {
                await RunInLoop();
            }
            finally
            {
                Database.OnSubscriptionEvent -= OnSubscriptionEvent;
            }
        }

        // Translate from the dictionary to the list that goes to the ws
        protected override List<SubscriptionStats> PreparePerformanceStats()
        {
            var preparedStats = new List<SubscriptionStats>(_perSubscriptionStats.Count);

            foreach (var dictionaryItem in _perSubscriptionStats)
            {
                var listOfEvents = dictionaryItem.Value.Performance; // performance is list of aggregators

                List<SubscriptionPerformanceStats> performanceStats = new List<SubscriptionPerformanceStats>();
                while (listOfEvents.TryTake(out SubscriptionStatsAggregator statsAggregator)) // remove events from the dictionary
                {
                    performanceStats.Add(statsAggregator.ToPerformanceStats());
                }

                var connection = dictionaryItem.Value.Handler.Connection;
                var id = connection?.SubscriptionId ?? 0;
                
                // use this 'if' for now ... otherwise we get exception if Connection is null, and event method drops
                if (id > 0)
                {
                    var listItem = new SubscriptionStats()
                    {
                        TaskName = dictionaryItem.Key, 
                        TaskId = id, 
                        TaskStats = performanceStats.ToArray()
                    };
                    
                    preparedStats.Add(listItem);
                }
            }
            
            return preparedStats;
        }

        // Write to client/stream 
        protected override void WriteStats(List<SubscriptionStats> stats, AsyncBlittableJsonTextWriter writer, JsonOperationContext context)
        {
            writer.WriteSubscriptionPerformanceStats(context, stats);
        }

        // update local dictionary
        private void OnSubscriptionEvent(string subscriptionName)
        {
            // New
            if (_perSubscriptionStats.TryGetValue(subscriptionName, out var subscriptionAndStats) == false)
            {
                using (Database.ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
                using (context.OpenReadTransaction())
                {
                    var sub = Database.SubscriptionStorage.GetSubscriptionConnection(context, subscriptionName);
                    _perSubscriptionStats.TryAdd(subscriptionName, new SubscriptionAndStats(sub));
                }
            }
            else  // Name exists in dictionary - just add the aggregator data
            {
                var latestStatsAggregator = subscriptionAndStats.Handler.Connection.GetLatestPerformanceStats();
                if (latestStatsAggregator != null)
                {
                    subscriptionAndStats.Performance.Add(latestStatsAggregator); // list of aggregators
                }
            } 
        } 

        // *** class for the dictionary ****************
        private class SubscriptionAndStats : HandlerAndPerformanceStatsList<SubscriptionConnectionState, SubscriptionStatsAggregator>
        // SubscriptionConnectionState ==> the subscription process object, taken from the subscription storage
        // SubscriptionStatsAggregator ==> building block of a collection, represents an event (i.e. batch sent...)
        {
            public string SubscriptionTaskName { get; } 
            // and also this class has list of SubscriptionStatsAggregator (a BlockingCollection, in the parent class)
            
            public SubscriptionAndStats(SubscriptionConnectionState subscription) : base(subscription)
            {
                SubscriptionTaskName = subscription.SubscriptionName;
            }
        }
    }
}
