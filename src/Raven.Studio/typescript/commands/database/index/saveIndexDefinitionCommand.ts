import commandBase = require("commands/commandBase");
import database = require("models/resources/database");

class saveIndexDefinitionCommand extends commandBase {

    constructor(private index: Raven.Client.Indexing.IndexDefinition, private db: database) {
        super();
    }

    execute(): JQueryPromise<any> {
        this.reportInfo("Saving " + this.index.Name + "...");

        return this.saveDefinition()
            .fail((response: JQueryXHR) => {
                this.reportError("Failed to save " + this.index.Name, response.responseText, response.statusText);
            })
            .done(() => {
                this.reportSuccess(`${this.index.Name} was Saved`);
            });

    }

    private saveDefinition(): JQueryPromise<any> {
        var urlArgs = {
            definition: "yes",
            name: this.index.Name
        };
        var putArgs = JSON.stringify(this.index);
        var url = "/indexes" + this.urlEncodeArgs(urlArgs);//TODO: use endpoints
        return this.put(url, putArgs, this.db);
    }
}

export = saveIndexDefinitionCommand; 
