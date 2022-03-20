import dialog = require("plugins/dialog");
import database = require("models/resources/database");
import dialogViewModelBase = require("viewmodels/dialogViewModelBase");
import clearIndexErrorsCommand = require("commands/database/index/clearIndexErrorsCommand");

class clearIndexErrorsConfirm extends dialogViewModelBase {

    view = require("views/database/indexes/clearIndexErrorsConfirm.html");

    title: string;
    subTitleHtml: string;
    clearErrorsTask = $.Deferred<boolean>();
    indexesToClear = ko.observableArray<string>();
    clearAllIndexes = ko.observable<boolean>();

    // constructor(indexesToClear: Array<string>, private db: database, private nodeTag?: string, private shardNumber?: string) {
    constructor(indexesToClear: Array<string>, private db: database, private nodeTagList: string[] = null, private shardNumberList: string[] = null) {
        super();
        this.indexesToClear(indexesToClear);
        this.clearAllIndexes(!indexesToClear);

        if (nodeTagList && nodeTagList.length > 1) { // todo ==> location... // multiple calls for all shards
            this.title = `Clear errors for ALL shards ?`;
            this.subTitleHtml = `Errors will be cleared for ALL shards (${this.clearAllIndexes() ? 'ALL' : 'selected'} indexes)`;
            
        } else if (nodeTagList && nodeTagList.length === 1) {
            this.title = `Clear errors on Shard ?`;
            this.subTitleHtml = `Errors will be cleared for <strong>Shard #${shardNumberList[0]} on Node ${nodeTagList[0]}</strong> (${this.clearAllIndexes() ? 'ALL' : 'selected'} indexes)`;
            
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
        
        // no shards
        if (!this.nodeTagList) { // todo move to locations..
            new clearIndexErrorsCommand(this.indexesToClear(), this.db)
                .execute()
                .done(() => {
                    this.clearErrorsTask.resolve(true);
                    dialog.close(this);
                }); 
        } else {
            // single or multiple shards
            for (let i = 0; i < this.nodeTagList.length; i++) {
                const delTask = this.clearTask(this.nodeTagList[i], this.shardNumberList[i]);
                arrayOfTasks.push(delTask);
            }
            
            $.when<any>(...arrayOfTasks)
                .done(() => {
                    this.clearErrorsTask.resolve(true);
                    dialog.close(this);
                });
        }
    }
    
    private clearTask(nodeTag?: string, shardNumber?: string): JQueryPromise<any> {
        return new clearIndexErrorsCommand(this.indexesToClear(), this.db, nodeTag, shardNumber)
            .execute();
    }

    cancel() {
        this.clearErrorsTask.resolve(false);
        dialog.close(this);
    }
}

export = clearIndexErrorsConfirm;


// import dialog = require("plugins/dialog");
// import database = require("models/resources/database");
// import dialogViewModelBase = require("viewmodels/dialogViewModelBase");
// import clearIndexErrorsCommand = require("commands/database/index/clearIndexErrorsCommand");
//
// class clearIndexErrorsConfirm extends dialogViewModelBase {
//
//     view = require("views/database/indexes/clearIndexErrorsConfirm.html");
//
//     title: string;
//     subTitleHtml: string;
//     clearErrorsTask = $.Deferred<boolean>();
//     indexesToClear = ko.observableArray<string>();
//     clearAllIndexes = ko.observable<boolean>();
//
//     constructor(indexesToClear: Array<string>, private db: database, private nodeTag?: string, private shardNumber?: string) {
//         super();
//         this.indexesToClear(indexesToClear);
//         this.clearAllIndexes(!indexesToClear);
//        
//         if (!!nodeTag && !!shardNumber) {
//             this.title = `Clear index errors on Shard ?`;
//             this.subTitleHtml = `Errors will be cleared for ${this.clearAllIndexes() ? 'ALL' : 'selected'} indexes on <strong>Shard #${shardNumber} on Node ${nodeTag}</strong>`;
//         } else if (this.clearAllIndexes()) {
//             this.title = "Clear errors for ALL indexes ?";
//             this.subTitleHtml = "Errors will be cleared for ALL indexes";
//         } else if (this.indexesToClear() && this.indexesToClear().length === 1) {
//             this.title = "Clear index Errors?";
//             this.subTitleHtml = `You're clearing errors from index:`;
//         } else {
//             this.title = "Clear indexes Errors?";
//             this.subTitleHtml = `You're clearing errors for <strong>${this.indexesToClear().length}</strong> indexes:`;
//         }
//     }
//
//     clearIndexes() {
//         new clearIndexErrorsCommand(this.indexesToClear(), this.db, this.nodeTag, this.shardNumber)
//             .execute()
//             .done(() => {
//                 this.clearErrorsTask.resolve(true);
//             });
//
//         dialog.close(this);
//     }
//
//     cancel() {
//         this.clearErrorsTask.resolve(false);
//         dialog.close(this);
//     }
// }
//
// export = clearIndexErrorsConfirm;
