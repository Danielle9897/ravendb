import commandBase = require("commands/commandBase");
import endpoints = require("endpoints");

class saveUnusedDatabaseIDsCommand extends commandBase {

    constructor(private unusedDatabaseIDs: string[], private dbName: string) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            name: this.dbName
        }

        const url = endpoints.global.adminDatabases.adminDatabasesUnusedIds + this.urlEncodeArgs(args);

        return this.post(url, JSON.stringify(this.unusedDatabaseIDs))
            .done(() => this.reportSuccess("the unused database IDs were saved successfully"))
            .fail((response: JQueryXHR) => this.reportError("Failed to save the unused database IDs", response.responseText));
    }
}

export = saveUnusedDatabaseIDsCommand;
