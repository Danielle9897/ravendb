import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import endpoints = require("endpoints");

class getTimeSeriesEntryOffsetCommand extends commandBase {
    
    constructor(private docId: string, private timeSeriesName: string, private db: database, private goToTime: string) {
        super();
    }
    
    execute(): JQueryPromise<Raven.Server.Documents.Handlers.TimeSeriesHandler.OffsetResult> {
        const args = {
            docId: this.docId,
            name: this.timeSeriesName,
            timestamp: this.goToTime
        };
        
        const url = endpoints.databases.timeSeries.timeseriesOffset;
        
        return this.query<Raven.Server.Documents.Handlers.TimeSeriesHandler.OffsetResult>(url + this.urlEncodeArgs(args), null, this.db)
            .fail((result: JQueryXHR) => {
                this.reportError("Failed to get entries data for the requested timestamp", result.responseText, result.statusText)
            });
    }
}

export = getTimeSeriesEntryOffsetCommand;
