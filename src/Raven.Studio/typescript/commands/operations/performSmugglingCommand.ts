import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import appUrl = require("common/appUrl");
import getOperationStatusCommand = require("commands/operations/getOperationStatusCommand");
 
class performSmugglingCommand extends commandBase {

    constructor(private migration: serverSmugglingDto, private db: database, private updateMigrationStatus: (status: serverSmugglingOperationStateDto) => void) { 
        super();
    }

    execute(): JQueryPromise<any> {
        var result = $.Deferred();
        this.post("/admin/serverSmuggling", JSON.stringify(this.migration), this.db)//TODO: use endpoints
            .fail((response: JQueryXHR) => {
                this.reportError("Failed to perform server migration!", response.responseText, response.statusText);
                result.reject();
            })
            .done((operationId: operationIdDto) => {
                this.monitorOperation(result, operationId.OperationId);
            });
        return result;
    }

    private monitorOperation(parentPromise: JQueryDeferred<any>, operationId: number) {
        new getOperationStatusCommand(this.db, operationId)
            .execute()
            /* TODO.done((result: serverSmugglingOperationStateDto) => {
            this.updateMigrationStatus(result);
            if (result.Completed) {
                if (result.Faulted || result.Canceled) {
                    this.reportError("Failed to perform server migration!", result.State.Error);
                    parentPromise.reject();
                } else {
                    this.reportSuccess("Server migration completed");
                    parentPromise.resolve();
                }
            } else {
                setTimeout(() => this.monitorOperation(parentPromise, operationId), 500);
            }
        });*/
    }


}

export = performSmugglingCommand;
