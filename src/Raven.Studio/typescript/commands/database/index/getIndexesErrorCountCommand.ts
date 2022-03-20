import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import endpoints = require("endpoints");

class getIndexesErrorCountCommand extends commandBase {

    constructor(private db: database, private nodeTag?: string, private shardNumber?: string) {
        super();
    }

    // execute(): JQueryPromise<indexErrorsCount[]> { // GetIndexErrorsCountCommand.IndexErrorsCount[] ???
    execute(): JQueryPromise<any> { // GetIndexErrorsCountCommand.IndexErrorsCount[] ???
        
        const url = endpoints.databases.studioIndex.studioIndexesErrorsCount;
        
        let args = null; // todo - one line..
        
        if (this.nodeTag && this.shardNumber) {
            args = {
                nodeTag: this.nodeTag,
                shardNumber: this.shardNumber
            } 
        }
        
        // return this.query<indexErrorsCount[]>(url, args, this.db) // todo  any ?
        return this.query<any>(url, args, this.db) // todo  any ?
            .fail((result: JQueryXHR) => this.reportError("Failed to get index errors count", result.responseText, result.statusText));
    }
}

export = getIndexesErrorCountCommand;
