import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import endpoints = require("endpoints");

class clearIndexErrorsCommand extends commandBase {
    constructor(private indexesNames: string[], private db: database, private nodeTag?: string, private shardNumber?: string) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = this.getArgsToUse();
        const url = endpoints.databases.index.indexesErrors + this.urlEncodeArgs(args);

        return this.del<void>(url, null, this.db)
            .done(() => this.reportSuccess("Indexing errors cleared successfully."));
    }

    private getArgsToUse() {
        if (this.nodeTag && this.shardNumber && this.indexesNames) {
            return {
                nodeTag: this.nodeTag,
                shardNumber: this.shardNumber,
                name: this.indexesNames
            }
        }
        else if (this.nodeTag && this.shardNumber){
            return {
                nodeTag: this.nodeTag,
                shardNumber: this.shardNumber
            }
        } else {
            return this.indexesNames ? { name: this.indexesNames } : {};
        }
    }
}

export = clearIndexErrorsCommand;
