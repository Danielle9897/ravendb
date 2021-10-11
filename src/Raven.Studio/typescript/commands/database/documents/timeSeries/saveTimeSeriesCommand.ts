import database = require("models/resources/database");
import commandBase = require("commands/commandBase");
import endpoints = require("endpoints");

class saveTimeSeriesCommand extends commandBase {
    constructor(private documentId: string, private name: string, private dto: Raven.Client.Documents.Operations.TimeSeries.TimeSeriesOperation.AppendOperation, 
                private db: database, private isIncremental: boolean, private isRollup: boolean) {
        super();
    }
    
    execute(): JQueryPromise<void> {
        const args = {
            docId: this.documentId
        };

        const url = endpoints.databases.timeSeries.timeseries + this.urlEncodeArgs(args);
        
        // const payload: TimeSeriesOperation = {
        //     Name: this.name,
        //     Deletes: [],
        //     Appends: this.isIncremental ? [] : [this.dto],
        //     Increments: this.isIncremental ? [this.dto] : []
        // };

        const payload: TimeSeriesOperation = {
            Name: this.name,
            Deletes: [],
            Appends: this.isRollup || !this.isIncremental ? [this.dto] : [],
            Increments: !this.isRollup && this.isIncremental ? [this.dto] : []
        };
        
        return this.post(url, JSON.stringify(payload), this.db, { dataType: undefined })
            .fail((response: JQueryXHR) => this.reportError("Failed to save time series.", response.responseText, response.statusText));
    }
}

export = saveTimeSeriesCommand;
