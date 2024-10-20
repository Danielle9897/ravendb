import commandBase = require("commands/commandBase");
import database = require("models/resources/database");

class deleteIndexCommand extends commandBase {
    constructor(private indexName: string, private db: database) {
        super();
    }

    execute(): JQueryPromise<any> {
        this.reportInfo("Deleting " + this.indexName + "...");
        const args = {
            name: this.indexName
        };
        return this.del("/indexes" + this.urlEncodeArgs(args), null, this.db)//TODO: use endpoints
            .fail((response: JQueryXHR) => this.reportError("Failed to delete index " + this.indexName, response.responseText))
            .done(() => this.reportSuccess("Deleted " + this.indexName));
    }
}

export = deleteIndexCommand;
