/// <reference path="../../typings/tsd.d.ts" />
import database = require("models/resources/database");
import d3 = require("d3");
import abstractWebSocketClient = require("common/abstractWebSocketClient");
import endpoints = require("endpoints");

class liveSubscriptionStatsWebSocketClient extends abstractWebSocketClient<resultsDto<Raven.Server.Documents.Subscriptions.SubscriptionStats>> {

    private static readonly isoParser = d3.time.format.iso;
    private readonly onData: (data: Raven.Server.Documents.Subscriptions.SubscriptionStats[]) => void;

    private readonly dateCutOff: Date;
    private mergedData: Raven.Server.Documents.Subscriptions.SubscriptionStats[] = [];
    private pendingDataToApply: Raven.Server.Documents.Subscriptions.SubscriptionStats[] = [];

    private updatesPaused = false;
    loading = ko.observable<boolean>(true);

    constructor(db: database, 
                onData: (data: Raven.Server.Documents.Subscriptions.SubscriptionStats[]) => void,
                dateCutOff?: Date) {
        super(db);
        this.onData = onData;
        this.dateCutOff = dateCutOff;
    }

    get connectionDescription() {
        return "Live Subscriptions Stats";
    }

    protected webSocketUrlFactory() {
        return endpoints.databases.subscriptions.subscriptionPerformanceLive;
    }

    get autoReconnect() {
        return false;
    }

    pauseUpdates() {
        this.updatesPaused = true;
    }

    resumeUpdates() {
        this.updatesPaused = false;

        if (this.pendingDataToApply.length) {
            this.mergeIncomingData(this.pendingDataToApply);
        }
        this.pendingDataToApply = [];
        this.onData(this.mergedData);
    }

    protected onHeartBeat() {
        this.loading(false);
    }

    protected onMessage(e: resultsDto<Raven.Server.Documents.Subscriptions.SubscriptionStats>) {
        this.loading(false);

        if (this.updatesPaused) {
            this.pendingDataToApply.push(...e.Results);
        } else {
            const hasAnyChange = this.mergeIncomingData(e.Results);
            if (hasAnyChange) {
                this.onData(this.mergedData);    
            }
        }
    }

    private mergeIncomingData(e: Raven.Server.Documents.Subscriptions.SubscriptionStats[]) {
        let hasAnyChange = false;
        
        e.forEach(subscriptionStatsFromEndpoint => {
            const subscriptionName = subscriptionStatsFromEndpoint.TaskName;
            const subscriptionId = subscriptionStatsFromEndpoint.TaskId;

            let existingSubscriptionStats = this.mergedData.find(x => x.TaskName === subscriptionName);
            
            if (!existingSubscriptionStats) {
                existingSubscriptionStats = {
                    TaskName: subscriptionName,
                    TaskId: subscriptionId,
                    TaskStats: []
                };
                this.mergedData.push(existingSubscriptionStats);
                hasAnyChange = true;
            }

            const idToIndexCache = new Map<number, number>();
            existingSubscriptionStats.TaskStats.forEach((v, idx) => {
                idToIndexCache.set(subscriptionId, idx); // todo - not sure about this..
            });

            // todo...
            subscriptionStatsFromEndpoint.TaskStats.forEach(stat => {
                liveSubscriptionStatsWebSocketClient.fillCache(stat);
                
                if (this.dateCutOff && this.dateCutOff.getTime() >= (stat as SubscriptionPerformanceBaseWithCache).StartedAsDate.getTime()) {
                    return;
                }
                
                hasAnyChange = true;

                // todo..
                if (idToIndexCache.has(subscriptionId)) { 
                    // update 
                    const subscriptionToUpdate = idToIndexCache.get(subscriptionId);
                    existingSubscriptionStats.TaskStats[subscriptionToUpdate] = stat;
                } else {
                    // this shouldn't invalidate idToIndexCache as we always append only
                    existingSubscriptionStats.TaskStats.push(stat);
                }
            });
        });
        
        //console.log(this.mergedData);
        
        return hasAnyChange;
    }

    static fillCache(stat: Raven.Server.Documents.Subscriptions.SubscriptionPerformanceStats) {

        const withCache = stat as SubscriptionPerformanceBaseWithCache;
        withCache.CompletedAsDate = stat.Completed ? liveSubscriptionStatsWebSocketClient.isoParser.parse(stat.Completed) : undefined;
        withCache.StartedAsDate = liveSubscriptionStatsWebSocketClient.isoParser.parse(stat.Started);
        withCache.Type = "Subscription";
        withCache.HasErrors = false; // this for now.. // todo - check this later..
        //withCache.Description = ??? // todo - check this later..
    }
}

export = liveSubscriptionStatsWebSocketClient;
