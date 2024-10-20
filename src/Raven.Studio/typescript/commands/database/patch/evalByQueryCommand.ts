import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import getOperationStatusCommand = require("commands/operations/getOperationStatusCommand");
import endpoints = require("endpoints");

class evalByQueryCommand extends commandBase {

    constructor(private indexName: string, private queryStr: string, private patchRequest: Raven.Server.Documents.Patch.PatchRequest, private db: database) {
        super();
    }

    execute(): JQueryPromise<operationIdDto> {
        this.reportInfo("Patching documents...");

        const url = endpoints.databases.queries.queries$ + this.indexName;
        const urlParams = "?query=" + encodeURIComponent(this.queryStr) + "&allowStale=true";
        return this.patch(url + urlParams, JSON.stringify(this.patchRequest), this.db)
            .done((response: operationIdDto) => {
                this.reportSuccess("Scheduled patch of index: " + this.indexName);
            })
            .fail((response: JQueryXHR) => this.reportError("Failed to schedule patch of index " + this.indexName, response.responseText, response.statusText));
    }

}

export = evalByQueryCommand; 
