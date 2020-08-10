import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import endpoints = require("endpoints");

class deleteReplicationHubAccessConfigCommand extends commandBase {
    
    constructor(private db: database, private hubTaskName: string, private thumbprint: string) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = { 
            name: this.hubTaskName,
            thumbprint: this.thumbprint
        };

        const url = endpoints.databases.pullReplication.adminTasksPullReplicationHubAccess + this.urlEncodeArgs(args);
        
        return this.del<void>(url, null, this.db)
            .done(() => this.reportSuccess(`Successfully deleted replication access for Hub task ${this.hubTaskName}`))
            .fail((response: JQueryXHR) => this.reportError(`Failed to delete replication acces for ub task ${this.hubTaskName}`, response.responseText));
    }
}

export = deleteReplicationHubAccessConfigCommand;
