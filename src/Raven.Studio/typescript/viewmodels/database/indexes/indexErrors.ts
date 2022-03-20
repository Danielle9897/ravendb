import app = require("durandal/app");
import appUrl = require("common/appUrl");
import awesomeMultiselect = require("common/awesomeMultiselect");
import generalUtils = require("common/generalUtils");
import clearIndexErrorsConfirm = require("viewmodels/database/indexes/clearIndexErrorsConfirm");
import shardViewModelBase from "viewmodels/shardViewModelBase";
import database from "models/resources/database";
import shardedDatabase from "models/resources/shardedDatabase";
import getIndexesErrorCountCommand from "commands/database/index/getIndexesErrorCountCommand";
import indexErrorInfoModel from "models/database/index/indexErrorInfoModel";

class indexNameAndCount {
    indexName: string;
    count: number;
}

type indexActionAndCount = {
    actionName: string;
    count: number;
}

class indexErrors extends shardViewModelBase {
    
    view = require("views/database/indexes/indexErrors.html");

    private isShardedDatabse: boolean;
    private numberOfShards: number;
    private errorInfoItems = ko.observableArray<indexErrorInfoModel>([]);
    
    private erroredIndexNames = ko.observableArray<indexNameAndCount>([]);
    private selectedIndexNames = ko.observableArray<string>([]);
    
    private erroredActionNames = ko.observableArray<indexActionAndCount>([]);
    private selectedActionNames = ko.observableArray<string>([]);

    searchText = ko.observable<string>();
    allIndexesSelected = ko.observable<boolean>();
    
    clearErrorsBtnText: KnockoutComputed<string>;
    hasErrors: KnockoutComputed<boolean>;

    constructor(db: database) {
        super(db);
        this.bindToCurrentInstance("toggleDetails", "clearIndexErrorsForItem", "clearIndexErrorsForAllItems");
        
        this.initObservables();
        
        this.isShardedDatabse = shardedDatabase.isSharded(db);
        if (this.isShardedDatabse) {
            this.numberOfShards = (this.db as shardedDatabase).shards().length;
        }
    }

    private initObservables() {
        this.searchText.throttle(200).subscribe(() => this.onSearchCriteriaChanged());
        this.selectedIndexNames.subscribe(() => this.onSearchCriteriaChanged());
        this.selectedActionNames.subscribe(() => this.onSearchCriteriaChanged());
      
        this.clearErrorsBtnText = ko.pureComputed(() => {
            if (this.allIndexesSelected() && this.erroredIndexNames().length) {
                return "Clear errors (All indexes)";
            } else if (this.selectedIndexNames().length) {
                return "Clear errors (Selected indexes)";
            } else {
                return "Clear errors";
            }
        });
        
        this.hasErrors = ko.pureComputed(() => {
           return this.errorInfoItems().some(x => x.totalErrorCount() > 0);
        });
    }

    canActivate(args: any): boolean | JQueryPromise<canActivateResultDto> {
        return $.when<any>(super.canActivate(args))
            .then(() => {
                const deferred = $.Deferred<canActivateResultDto>();

                return this.getErrorCount()
                    .then(() => {
                        return deferred.resolve({can: true})
                            .fail(() => deferred.resolve({redirect: appUrl.forStatus(this.db)}));
                    });
            });
    }
    
    getErrorCount(): JQueryPromise<any> {
        this.erroredIndexNames([]);
        this.erroredActionNames([]);
        this.errorInfoItems([]);
        
        const arrayOfTasks: JQueryPromise<any>[] = [];

        // todo.. remove if.. ???
        if (this.isShardedDatabse) {
            this.db.nodes().forEach(node => {
                for (let i = 0; i < this.numberOfShards; i++) {
                    const errorCountTask = this.fetchErrorCount(node, i.toString());
                    arrayOfTasks.push(errorCountTask);
                }
            });
        } else {
            const errorCountTask = this.fetchErrorCount();
            arrayOfTasks.push(errorCountTask);
        }

        return $.when<any>(...arrayOfTasks)
            .then(() => { 
                this.selectedIndexNames(this.erroredIndexNames().map(x => x.indexName));
                this.selectedActionNames(this.erroredActionNames().map(x => x.actionName));
            });
    }

    private fetchErrorCount(nodeTag?: string, shardNumber?: string): JQueryPromise<any> {
        return new getIndexesErrorCountCommand(this.db, nodeTag, shardNumber)
            .execute()
            .done(results => {
                const resultsArray: indexErrorsCount[] = results.Results;

                // calc all model items
                const totalErrorCount = this.calcErrorCountTotal(resultsArray);
                const item = new indexErrorInfoModel(this.db, nodeTag, shardNumber, totalErrorCount);
                this.errorInfoItems.push(item);

                // calc all index names for top dropdown
                resultsArray.forEach(resultItem => {
                    const index = this.erroredIndexNames().find(x => x.indexName === resultItem.Name);
                    if (index) {
                        index.count += this.calcErrorCountForIndex(resultItem);
                    } else {
                        const item = {
                            indexName: resultItem.Name,
                            count: this.calcErrorCountForIndex(resultItem)
                        };
                        this.erroredIndexNames.push(item);
                    }
                });

                // calc all actions for top dropdown
                resultsArray.forEach(resultItem => {
                    resultItem.Errors.forEach(errItem => {
                        const action = this.erroredActionNames().find(x => x.actionName === errItem.Action);
                        if (action) {
                            action.count += errItem.NumberOfErrors;
                        } else {
                            const item = {
                                actionName: errItem.Action,
                                count: errItem.NumberOfErrors
                            }
                            this.erroredActionNames.push(item);
                        }
                    });
                });

                this.erroredIndexNames(_.sortBy(this.erroredIndexNames(), x => x.indexName.toLocaleLowerCase()));
                this.erroredActionNames(_.sortBy(this.erroredActionNames(), x => x.actionName.toLocaleLowerCase()));
                this.errorInfoItems(_.sortBy(this.errorInfoItems(), x => x.nodeTag, x => x.shardNumber)) // todo.. check.. ??? 
            });
    }
    
    private calcErrorCountTotal(results: indexErrorsCount[]): number {
        let count = 0;
        
        for (let i = 0; i < results.length; i++) {
            count += this.calcErrorCountForIndex(results[i]);
        }
        
        return count;
    }
    
    private calcErrorCountForIndex(indexCount: indexErrorsCount): number {
        return indexCount.Errors.reduce((count, val) => val.NumberOfErrors + count, 0);
    }
    
    activate(args: any) {
        super.activate(args);
        this.updateHelpLink('ABUXGF');
    }

    attached() {
        super.attached();

        awesomeMultiselect.build($("#visibleIndexesSelector"), opts => {
            opts.enableHTML = true;
            opts.includeSelectAllOption = true;
            opts.nSelectedText = " indexes selected";
            opts.allSelectedText = "All indexes selected";
            opts.maxHeight = 500;
            opts.optionLabel = (element: HTMLOptionElement) => {
                const indexName = $(element).text();
                const indexNameEscaped = generalUtils.escape(indexName);
                const indexItem = this.erroredIndexNames().find(x => x.indexName === indexName);
                const indexItemCount = indexItem.count.toLocaleString();
                return `<span class="name" title="${indexNameEscaped}">${indexNameEscaped}</span><span class="badge" title="${indexItemCount}">${indexItemCount}</span>`;
            };
        });

        awesomeMultiselect.build($("#visibleActionsSelector"), opts => {
            opts.enableHTML = true;
            opts.includeSelectAllOption = true;
            opts.nSelectedText = " actions selected";
            opts.allSelectedText = "All actions selected";
            opts.maxHeight = 500;
            opts.optionLabel = (element: HTMLOptionElement) => {
                const actionName = $(element).text();
                const actionNameEscaped = generalUtils.escape(actionName);
                const actionItem = this.erroredActionNames().find(x => x.actionName === actionName);
                const actionItemCount = actionItem.count.toLocaleString();
                return `<span class="name" title="${actionNameEscaped}">${actionNameEscaped}</span><span class="badge" title="${actionItemCount}">${actionItemCount}</span>`;
            };
        });
    }

    private static syncMultiSelect() {
        awesomeMultiselect.rebuild($("#visibleIndexesSelector"));
        awesomeMultiselect.rebuild($("#visibleActionsSelector"));
    }

    compositionComplete() {
        super.compositionComplete();
        indexErrors.syncMultiSelect();
    }

    refresh() {
        this.getErrorCount()
            .then(() => {
                indexErrors.syncMultiSelect();
                this.errorInfoItems().forEach(x => x.refresh());
            });
    }

    private onSearchCriteriaChanged() {
        this.allIndexesSelected(this.erroredIndexNames().length === this.selectedIndexNames().length);
        
        this.errorInfoItems().forEach(x => {
            x.searchText(this.searchText());
            x.selectedIndexNames(this.selectedIndexNames());
            x.selectedActionNames(this.selectedActionNames());
            x.allIndexesSelected(this.allIndexesSelected());
            x.refresh();
        })
    }
    
    toggleDetails(item: indexErrorInfoModel) {
        item.toggleDetails();
    }

    clearIndexErrorsForAllItems() {
        
        const listOfNodes = this.errorInfoItems().map(x => x.nodeTag());
        const listOfShardNumbers = this.errorInfoItems().map(x => x.shardNumber());
        
        const clearErrorsDialog = new clearIndexErrorsConfirm(this.allIndexesSelected() ? null : this.selectedIndexNames(), this.db, listOfNodes, listOfShardNumbers);
        app.showBootstrapDialog(clearErrorsDialog);

        clearErrorsDialog.clearErrorsTask
            .done((errorsCleared: boolean) => {
                if (errorsCleared) {
                    this.refresh();
                }
            });
    }
    // clearIndexErrorsForAllItems() {
    //     this.errorInfoItems().forEach(x => {
    //         this.clearErrors(x.nodeTag(), x.shardNumber());
    //     })
    // }
    
    clearIndexErrorsForItem(item: indexErrorInfoModel) {
        
        const nodeTag = item.nodeTag() ? [item.nodeTag()] : null;
        const shardNumber = item.shardNumber() ? [item.shardNumber()] : null;
        
        const clearErrorsDialog = new clearIndexErrorsConfirm(this.allIndexesSelected() ? null : this.selectedIndexNames(), this.db, nodeTag, shardNumber); // todo - unite content.. with above
        app.showBootstrapDialog(clearErrorsDialog);

        clearErrorsDialog.clearErrorsTask
            .done((errorsCleared: boolean) => {
                if (errorsCleared) {
                    this.refresh();
                }
            });
    }
    
    // private clearErrors(nodeTag?: string, shardNumber?: string) {
    //     const clearErrorsDialog = new clearIndexErrorsConfirm(this.allIndexesSelected() ? null : this.selectedIndexNames(), this.db, nodeTag, shardNumber);
    //     app.showBootstrapDialog(clearErrorsDialog);
    //
    //     clearErrorsDialog.clearErrorsTask
    //         .done((errorsCleared: boolean) => {
    //             if (errorsCleared) {
    //                 this.refresh();
    //             }
    //         });
    // }
}

export = indexErrors; 
