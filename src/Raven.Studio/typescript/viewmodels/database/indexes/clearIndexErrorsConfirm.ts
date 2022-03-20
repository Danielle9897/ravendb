import dialog = require("plugins/dialog");
import database = require("models/resources/database");
import dialogViewModelBase = require("viewmodels/dialogViewModelBase");
import clearIndexErrorsCommand = require("commands/database/index/clearIndexErrorsCommand");
import messagePublisher = require("common/messagePublisher");

class clearIndexErrorsConfirm extends dialogViewModelBase {

    view = require("views/database/indexes/clearIndexErrorsConfirm.html");

    title: string;
    subTitleHtml: string;
    clearErrorsTask = $.Deferred<boolean>();
    indexesToClear = ko.observableArray<string>();
    clearAllIndexes = ko.observable<boolean>();

    constructor(indexesToClear: Array<string>, private db: database, private locations: databaseLocationSpecifier[]) { // todo - revert to original code
        super();
        this.indexesToClear(indexesToClear);
        this.clearAllIndexes(!indexesToClear);

        const hasShards = locations.some(x => x.shardNumber !== undefined);// todo - use db instance of shard..

        if (hasShards) {
            const indexesText = `(${this.clearAllIndexes() ? 'ALL' : 'selected'} indexes)`;
            
            if (locations.length > 1) {
                this.title = `Clear errors on ALL shards ?`;
                this.subTitleHtml = `Errors will be cleared for ALL shards ${indexesText}`;

            } else if (locations.length === 1) {
                this.title = `Clear errors on Shard ?`;
                this.subTitleHtml = `Errors will be cleared for <strong>Shard #${locations[0].shardNumber} on Node ${locations[0].nodeTag}</strong> ${indexesText}`;
            }
        } else {
            const nodesText = locations.length === 1 ? `on Node ${locations[0].nodeTag}` : "on ALL nodes";
            
            if (this.clearAllIndexes()) {
                this.title = "Clear errors for ALL indexes ?";
                this.subTitleHtml = `Errors will be cleared for ALL indexes ${nodesText}`;

            } else if (this.indexesToClear() && this.indexesToClear().length === 1) {
                this.title = "Clear index Errors?";
                this.subTitleHtml = `You're clearing errors from this index ${nodesText}`;

            } else {
                this.title = "Clear indexes Errors?";
                this.subTitleHtml = `You're clearing errors for <strong>${this.indexesToClear().length}</strong> indexes ${nodesText}`;
            }
        }
    }

    clearIndexes() {
        const arrayOfTasks = this.locations.map(location => this.clearTask(location));
                
        $.when<any>(...arrayOfTasks)
            .always(() => {
                messagePublisher.reportSuccess("Done clearing indexing errors.");
                this.clearErrorsTask.resolve(true);
                dialog.close(this);
            });
    }

    private clearTask(location: databaseLocationSpecifier): JQueryPromise<any> {
        return new clearIndexErrorsCommand(this.indexesToClear(), this.db, location)
            .execute();
    }

    cancel() {
        this.clearErrorsTask.resolve(false);
        dialog.close(this);
    }
}

export = clearIndexErrorsConfirm;


