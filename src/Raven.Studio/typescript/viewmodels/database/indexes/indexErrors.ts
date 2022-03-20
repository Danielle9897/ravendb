import app = require("durandal/app");
import awesomeMultiselect = require("common/awesomeMultiselect");
import generalUtils = require("common/generalUtils");
import clearIndexErrorsConfirm = require("viewmodels/database/indexes/clearIndexErrorsConfirm");
import shardViewModelBase from "viewmodels/shardViewModelBase";
import database from "models/resources/database";
import shardedDatabase from "models/resources/shardedDatabase";
import getIndexesErrorCountCommand from "commands/database/index/getIndexesErrorCountCommand";
import indexErrorInfoModel from "models/database/index/indexErrorInfoModel";


// todo unite 
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
        
        this.hasErrors = ko.pureComputed(() => this.errorInfoItems().some(x => x.totalErrorCount() > 0));
    }

    activate(args: any) {
        super.activate(args);
        this.updateHelpLink('ABUXGF');

        // return this.getErrorCountForAllNodesAndShards();
        this.getErrorCountForAllNodesAndShards();
    }

    getErrorCountForAllNodesAndShards() { // todo rename to load // ==> getAllErrorCount
        this.erroredIndexNames([]);
        this.erroredActionNames([]);
        this.errorInfoItems([]);      
        
        const arrayOfTasks = this.db.getLocations().map(location => this.getErrorCountForLocation(location));
    }
    // getErrorCountForAllNodesAndShards(): JQueryPromise<any> { // todo rename to load // ==> getAllErrorCount
    //     this.erroredIndexNames([]);
    //     this.erroredActionNames([]);
    //     this.errorInfoItems([]);
    //
    //     const arrayOfTasks: JQueryPromise<any>[] = [];
    //
    //     const isShardedDatabase = shardedDatabase.isSharded(this.db);
    //     const numberForLoop = isShardedDatabase ? (this.db as shardedDatabase).shards().length : 1;
    //    
    //     this.db.nodes().forEach(node => {
    //         for (let i = 0; i < numberForLoop; i++) {
    //             const location: databaseLocationSpecifier = { 
    //                 nodeTag: node,
    //                 shardNumber: isShardedDatabase ? i : undefined 
    //             };
    //             const errorCountTask = this.getErrorCountForLocation(location);
    //             arrayOfTasks.push(errorCountTask);
    //         }
    //     });
    //    
    //     return $.when<any>(...arrayOfTasks)
    //         .always(() => {
    //         //.then(() => {
    //         //.done(() => {
    //             this.erroredIndexNames(_.sortBy(this.erroredIndexNames(), x => x.indexName.toLocaleLowerCase()));
    //             this.erroredActionNames(_.sortBy(this.erroredActionNames(), x => x.actionName.toLocaleLowerCase()));
    //             this.errorInfoItems(_.sortBy(this.errorInfoItems(), x => x.location().nodeTag))
    //
    //             this.selectedIndexNames(this.erroredIndexNames().map(x => x.indexName));
    //             this.selectedActionNames(this.erroredActionNames().map(x => x.actionName));
    //         });
    // }

    private getErrorCountForLocation(location: databaseLocationSpecifier): JQueryPromise<any> { // todo rename 
        return new getIndexesErrorCountCommand(this.db, location)
            .execute()
            .done(results => {
                const resultsArray: indexErrorsCount[] = results.Results;

                // calc model item
                const totalErrorCount = this.calcErrorCountTotal(resultsArray);
                const item = new indexErrorInfoModel(this.db, location, totalErrorCount); // todo - pass parent this
                this.errorInfoItems.push(item);

                // calc index names for top dropdown
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

                // calc actions for top dropdown
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
            })
            .fail((result) => {
                const item = new indexErrorInfoModel(this.db, location, 0, result.responseJSON.Message);
                this.errorInfoItems.push(item);
            })
            .always(() => {
                this.erroredIndexNames(_.sortBy(this.erroredIndexNames(), x => x.indexName.toLocaleLowerCase()));
                this.erroredActionNames(_.sortBy(this.erroredActionNames(), x => x.actionName.toLocaleLowerCase()));
                this.errorInfoItems(_.sortBy(this.errorInfoItems(), x => x.location().nodeTag))

                this.selectedIndexNames(this.erroredIndexNames().map(x => x.indexName));
                this.selectedActionNames(this.erroredActionNames().map(x => x.actionName));
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
        this.getErrorCountForAllNodesAndShards();
            // .then(() => {
            //     indexErrors.syncMultiSelect();
            //     this.errorInfoItems().forEach(x => x.refresh());
            // });
    }

    private onSearchCriteriaChanged() {
        this.allIndexesSelected(this.erroredIndexNames().length === this.selectedIndexNames().length); // ==> todo computed
        
        this.errorInfoItems().forEach(x => { // todo - pass the parent (this)
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
        const listOfLocations = this.errorInfoItems().map(x => x.location());
        this.handleClearRequest(listOfLocations);
    }

    clearIndexErrorsForItem(item: indexErrorInfoModel) {
        this.handleClearRequest([item.location()]);
    }

    private handleClearRequest(locations: databaseLocationSpecifier[]) {
        const clearErrorsDialog = new clearIndexErrorsConfirm(this.allIndexesSelected() ? null : this.selectedIndexNames(), this.db, locations);
        app.showBootstrapDialog(clearErrorsDialog);

        clearErrorsDialog.clearErrorsTask
            .done((errorsCleared: boolean) => {
                if (errorsCleared) {
                    this.refresh();
                }
            });
    }
}

export = indexErrors; 
