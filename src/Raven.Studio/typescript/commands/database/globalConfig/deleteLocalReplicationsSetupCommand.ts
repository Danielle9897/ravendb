import commandBase = require("commands/commandBase");
import database = require("models/resources/database");

class deleteLocalReplicationsSetupCommand extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<any> {
        this.reportInfo("Updating Replication config setup.");
        return $.when(this.deleteConflictResolution(), this.deleteLocalDestinations())
            .done(() => this.reportSuccess("Updated Replication config setup."))
            .fail((response: JQueryXHR) => this.reportError("Failed to update replication config setup.", response.responseText));
    }


    private deleteConflictResolution(): JQueryPromise<any> {
        var url = "/docs?id=Raven/Replication/Config";//TODO: use endpoints
        return this.del(url, null, this.db);
    }

    private deleteLocalDestinations(): JQueryGenericPromise<any> {
        var url = "/docs?id=Raven/Replication/Destinations";//TODO: use endpoints
        return this.del(url, null, this.db);
    }
}
export = deleteLocalReplicationsSetupCommand;
