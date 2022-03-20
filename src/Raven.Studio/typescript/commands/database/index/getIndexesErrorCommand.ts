import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import endpoints = require("endpoints");

class getIndexesErrorCommand extends commandBase {

    constructor(private db: database, private nodeTag: string, private shardNumber: string) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Indexes.IndexErrors[]> {
        const args = this.getArgsToUse();
        const url = endpoints.databases.index.indexesErrors + (args ? this.urlEncodeArgs(args) : "");
        
        return this.query<Raven.Client.Documents.Indexes.IndexErrors[]>(url, null, this.db, x => x.Results)
            .fail((result: JQueryXHR) => this.reportError("Failed to get index errors", result.responseText, result.statusText));
    }
    
    private getArgsToUse() {
        if (this.nodeTag && this.shardNumber) {
            return {
                nodeTag: this.nodeTag,
                shardNumber: this.shardNumber
            }    
        }
        
        return null;
    }
}

export = getIndexesErrorCommand;
