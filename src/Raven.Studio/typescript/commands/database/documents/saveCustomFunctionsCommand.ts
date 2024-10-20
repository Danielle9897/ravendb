import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import customFunctions = require("models/database/documents/customFunctions");

class saveCustomFunctionsCommand extends commandBase {
    constructor(private db: database, private toSave: customFunctions, private global = false) {
        super();

        if (!db) {
            throw new Error("Must specify database");
        }
    }

    private validateCustomFunctions(document: string):JQueryPromise<any> {
        return this.post("/studio-tasks/validateCustomFunctions", document, this.db, { dataType: 'text' });//TODO: use endpoints
    }

    execute(): JQueryPromise<any> {
        var args = JSON.stringify(this.toSave.toDto());

        return this.validateCustomFunctions(args)
            .fail((response) => this.reportError("Failed to validate custom functions!", response.responseText, response.statusText))
            .then(() => {
                var url = this.global ? "/docs?id=Raven/Global/Javascript/Functions" : "/docs?id=Raven/Javascript/Functions";//TODO: use endpoints
                var saveTask = this.put(url, args, this.db, null);

                saveTask.done(() => this.reportSuccess("Custom functions saved!"));
                saveTask.fail((response: JQueryXHR) => this.reportError("Failed to save custom functions!", response.responseText, response.statusText));
                return saveTask;
            });
    }
}

export = saveCustomFunctionsCommand;
