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

    constructor(indexesToClear: Array<string>, private db: database, private locations: databaseLocationSpecifier[]) {
        super();
        this.indexesToClear(indexesToClear);
        this.clearAllIndexes(!indexesToClear);

        const hasShards = locations.some(x => x.shardNumber !== undefined);

        if (locations.length > 1 && hasShards) { // todo ==> location... // multiple calls for all shards
            this.title = `Clear errors for ALL shards ?`;
            this.subTitleHtml = `Errors will be cleared for ALL shards (${this.clearAllIndexes() ? 'ALL' : 'selected'} indexes)`;

        } else if (locations.length === 1 && hasShards) {
            this.title = `Clear errors on Shard ?`;
            this.subTitleHtml = `Errors will be cleared for <strong>Shard #${locations[0].shardNumber} on Node ${locations[0].nodeTag}</strong> (${this.clearAllIndexes() ? 'ALL' : 'selected'} indexes)`;

        } else if (this.clearAllIndexes()) {
            this.title = "Clear errors for ALL indexes ?";
            this.subTitleHtml = "Errors will be cleared for ALL indexes";

        } else if (this.indexesToClear() && this.indexesToClear().length === 1) {
            this.title = "Clear index Errors?";
            this.subTitleHtml = `You're clearing errors from index:`;

        } else {
            this.title = "Clear indexes Errors?";
            this.subTitleHtml = `You're clearing errors for <strong>${this.indexesToClear().length}</strong> indexes:`;
        }
    }

    clearIndexes() {
        const arrayOfTasks: JQueryPromise<any>[] = [];

        for (let i = 0; i < this.locations.length; i++) {
            const clearTask = this.clearTask(this.locations[i]);
            arrayOfTasks.push(clearTask);
        }

        $.when<any>(...arrayOfTasks)
            .always(() => {
            // .then(() => {
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


