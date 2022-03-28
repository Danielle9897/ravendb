using System.Threading.Tasks;
using Raven.Server.Documents.Sharding.Handlers.Processors.TimeSeries;
using Raven.Server.Routing;

namespace Raven.Server.Documents.Sharding.Handlers
{
    public class ShardedTimeSeriesHandler : ShardedDatabaseRequestHandler
    {
        [RavenShardedAction("/databases/*/timeseries/stats", "GET")]
        public async Task Stats()
        {
            using (var processor = new ShardedTimeSeriesHandlerProcessorForGetTimeSeriesStats(this))
            {
                await processor.ExecuteAsync();
            }
        }
    }
}
