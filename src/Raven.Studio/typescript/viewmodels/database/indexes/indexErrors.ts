import app = require("durandal/app");
import appUrl = require("common/appUrl");
import timeHelpers = require("common/timeHelpers");
import awesomeMultiselect = require("common/awesomeMultiselect");
import generalUtils = require("common/generalUtils");
import clearIndexErrorsConfirm = require("viewmodels/database/indexes/clearIndexErrorsConfirm");
import shardViewModelBase from "viewmodels/shardViewModelBase";
import database from "models/resources/database";
import shardedDatabase from "models/resources/shardedDatabase";
import clusterTopologyManager from "common/shell/clusterTopologyManager";
import getIndexesErrorCountCommand from "commands/database/index/getIndexesErrorCountCommand";
import indexErrorInfoModel from "models/database/index/indexErrorInfoModel";

type indexNameAndCount = {
    indexName: string;
    count: number;
}

// type indexActionAndCount = {
//     actionName: string;
//     count: number;
// }

class indexErrors extends shardViewModelBase {
    
    view = require("views/database/indexes/indexErrors.html");

    private isShardedDatabse: boolean;
    private numberOfShards: number;
    private errorInfoItems = ko.observableArray<indexErrorInfoModel>([]);
    
    // private allIndexErrors: IndexErrorPerDocument[] = null; // ==> indexErrors in model
    // private filteredIndexErrors: IndexErrorPerDocument[] = null; // ===>
    
    // private gridController = ko.observable<virtualGridController<IndexErrorPerDocument>>(); // ===>
    // private columnPreview = new columnPreviewPlugin<IndexErrorPerDocument>(); // ===>

    // private allErroredIndexNamesSet = ko.observable<Set<string>>(); // todo remove this
    
    private allErroredIndexNames = ko.observableArray<indexNameAndCount>([]); //  V 
    private allSelectedIndexNames = ko.observableArray<string>([]); // ==> V (and in model also ...)     
    
    // private allErroredActionNames = ko.observableArray<indexActionAndCount>([]); // TODO when we have info about 'Action' from error-count ep
    // private selectedActionNames = ko.observableArray<string>([]); // TODO
        
    //private ignoreSearchCriteriaUpdatesMode = false; // ?
    searchText = ko.observable<string>(); // ==> V (and in model)

    private localLatestIndexErrorTime = ko.observable<string>(null);
    private remoteLatestIndexErrorTime = ko.observable<string>(null);

    private isDirty: KnockoutComputed<boolean>;
    
    allIndexesSelected: KnockoutComputed<boolean>;
    clearErrorsBtnText: KnockoutComputed<string>; // Clear All
    clearErrorsBtnTooltip: KnockoutComputed<string>;

    constructor(db: database) {
        super(db);
        this.bindToCurrentInstance("toggleDetails");
        
        this.initObservables();
        this.bindToCurrentInstance("clearIndexErrors");
                
        // new
        const localNodeTag = clusterTopologyManager.default.localNodeTag;
        const allNodes = this.db.nodes;
        
        this.isShardedDatabse = shardedDatabase.isSharded(db);
        if (this.isShardedDatabse) {
            this.numberOfShards = (this.db as shardedDatabase).shards().length;
        }
        // end new
    }

    private initObservables() {
        this.searchText.throttle(200).subscribe(() => this.onSearchCriteriaChanged());
        this.allSelectedIndexNames.subscribe(() => this.onSearchCriteriaChanged());
        //this.selectedActionNames.subscribe(() => this.onSearchCriteriaChanged());

        this.isDirty = ko.pureComputed(() => {
            const local = this.localLatestIndexErrorTime();
            const remote = this.remoteLatestIndexErrorTime();

            return local !== remote;
        });        
      
        this.allIndexesSelected = ko.pureComputed(() => { 
            return this.allErroredIndexNames().length === this.allSelectedIndexNames().length;
        });

        this.clearErrorsBtnText = ko.pureComputed(() => {
            if (this.allIndexesSelected() && this.allErroredIndexNames().length) {
                return "Clear errors (All indexes)";
            } else if (this.allSelectedIndexNames().length) {
                return "Clear errors (Selected indexes)";
            } else {
                return "Clear errors";
            }
        });

        this.clearErrorsBtnTooltip = ko.pureComputed(() => {
            if (this.allIndexesSelected() && this.allErroredIndexNames().length) {
                return "Clear errors for all indexes";
            } else if (this.allSelectedIndexNames().length) {
                return "Clear errors for selected indexes";
            }
        });
    }

    getErrorCount(): JQueryPromise<any> {
        const arrayOfTasks: JQueryPromise<any>[] = [];

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
            .then(() => this.allSelectedIndexNames(this.allErroredIndexNames().map(x => x.indexName)));
    }

    canActivate(args: any): boolean | JQueryPromise<canActivateResultDto> {
        return $.when<any>(super.canActivate(args))
            .then(() => {
                    // get general info about number of errors
                    const deferred = $.Deferred<canActivateResultDto>();

                    return this.getErrorCount()
                        .then(() => { return deferred.resolve({can: true})
                        .fail(() => deferred.resolve({redirect: appUrl.forStatus(this.db)}));
                });
           });
    }
    // canActivate(args: any): boolean | JQueryPromise<canActivateResultDto> {
    //     return $.when<any>(super.canActivate(args))
    //         .then(() => {
    //             // get general info about number of errors
    //             const deferred = $.Deferred<canActivateResultDto>();
    //            
    //             const arrayOfTasks: JQueryPromise<any>[] = [];
    //
    //             if (this.isShardedDatabse) {
    //                 this.db.nodes().forEach(node => {
    //                     for (let i = 0; i < this.numberOfShards; i++) {
    //                         const errorCountTask = this.fetchErrorCount(node, i.toString());
    //                         arrayOfTasks.push(errorCountTask);
    //                     }
    //                 });
    //             } else {
    //                 const errorCountTask = this.fetchErrorCount();
    //                 arrayOfTasks.push(errorCountTask);
    //             }
    //
    //             return $.when<any>(...arrayOfTasks)                    
    //                 .then(() => {
    //                     // this.allSelectedIndexNames(this.allErroredIndexNames().map(x => x.indexName));
    //                     return deferred.resolve({can: true});
    //                  })
    //                 .fail(() => deferred.resolve({redirect: appUrl.forStatus(this.db)}));
    //         }
    //     );
    // }

    private fetchErrorCount(nodeTag?: string, shardNumber?: string): JQueryPromise<any> {
        return new getIndexesErrorCountCommand(this.db, nodeTag, shardNumber)
            .execute()
            // .done((results: indexErrorsCount[]) => {
            .done(results => {
                const resultsArray: indexErrorsCount[] = results.Results;
                
                const totalErrorCount = resultsArray.reduce((count, val) => val.NumberOfErrors + count, 0);
                const item = new indexErrorInfoModel(this.db, nodeTag, shardNumber, totalErrorCount);
                
                this.errorInfoItems().push(item);
                
                // calc all index names for top dropdown
                resultsArray.forEach(resultItem => {
                    const index = this.allErroredIndexNames().find(x => x.indexName === resultItem.Name);
                    if (index) {
                        index.count += resultItem.NumberOfErrors; 
                    } else {
                        this.allErroredIndexNames().push({
                            indexName: resultItem.Name,
                            count: resultItem.NumberOfErrors
                        })
                    }
                });
            });
    }

    // not good
    // // TODO - unite with above...
    // private updateErrorCount(nodeTag?: string, shardNumber?: string) {
    //     return new getIndexesErrorCountCommand(this.db, nodeTag, shardNumber)
    //         .execute()
    //         .done(results => {
    //             const resultsArray: indexErrorsCount[] = results.Results;
    //             const totalErrorCount = resultsArray.reduce((count, val) => val.NumberOfErrors + count, 0);
    //                            
    //             // find item and update
    //             const item = this.errorInfoItems().find(x => x.nodeTag() === nodeTag && x.shardNumber() === shardNumber);
    //             item.totalErrorCount(totalErrorCount);
    //
    //             // update index names for top dropdown
    //             resultsArray.forEach(resultItem => {
    //                 const index = this.allErroredIndexNames().find(x => x.indexName === resultItem.Name);
    //                 if (index) {
    //                     index.count = resultItem.NumberOfErrors;
    //                 } else {
    //                     this.allErroredIndexNames().push({
    //                         indexName: resultItem.Name,
    //                         count: resultItem.NumberOfErrors
    //                     })
    //                 }
    //             });
    //         });
    // }
    
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
                const indexItem = this.allErroredIndexNames().find(x => x.indexName === indexName);
                return `<span class="name">${generalUtils.escape(indexName)}</span><span class="badge">${indexItem.count}</span>`;
            };
        });

        // awesomeMultiselect.build($("#visibleActionsSelector"), opts => {
        //     opts.enableHTML = true;
        //     opts.includeSelectAllOption = true;
        //     opts.nSelectedText = " actions selected";
        //     opts.allSelectedText = "All actions selected";
        //     opts.maxHeight = 500;
        //     opts.optionLabel = (element: HTMLOptionElement) => {
        //         const actionName = $(element).text();
        //         const actionItem = this.allErroredActionNames().find(x => x.actionName === actionName);
        //         return `<span class="name">${generalUtils.escape(actionName)}</span><span class="badge">${actionItem.count}</span>`;
        //     };
        // });
    }

    private static syncMultiSelect() {
        awesomeMultiselect.rebuild($("#visibleIndexesSelector"));
        //awesomeMultiselect.rebuild($("#visibleActionsSelector"));
    }

    // TODO marcin ???
    protected afterClientApiConnected() {
        this.addNotification(this.changesContext.databaseNotifications().watchAllDatabaseStatsChanged(stats => this.onStatsChanged(stats)));
    }

    compositionComplete() {
        super.compositionComplete();        
        
        // do this per error item ? comment out for now..
        
        // const grid = this.gridController();
        // grid.headerVisible(true);
        // grid.init(() => this.fetchIndexErrors(), () =>
        //     [
        //         new actionColumn<IndexErrorPerDocument>(grid, (error, index) => this.showErrorDetails(index), "Show", `<i class="icon-preview"></i>`, "72px",
        //             {
        //                 title: () => 'Show indexing error details'
        //             }),
        //         new hyperlinkColumn<IndexErrorPerDocument>(grid, x => x.IndexName, x => appUrl.forEditIndex(x.IndexName, this.db), "Index name", "25%", {
        //             sortable: "string",
        //             customComparator: generalUtils.sortAlphaNumeric
        //         }),
        //         new hyperlinkColumn<IndexErrorPerDocument>(grid, x => x.Document, x => appUrl.forEditDoc(x.Document, this.db), "Document Id", "20%", {
        //             sortable: "string",
        //             customComparator: generalUtils.sortAlphaNumeric
        //         }),
        //         new textColumn<IndexErrorPerDocument>(grid, x => generalUtils.formatUtcDateAsLocal(x.Timestamp), "Date", "20%", {
        //             sortable: "string"
        //         }),
        //         new textColumn<IndexErrorPerDocument>(grid, x => x.Action, "Action", "10%", {
        //             sortable: "string"
        //         }),
        //         new textColumn<IndexErrorPerDocument>(grid, x => x.Error, "Error", "15%", {
        //             sortable: "string"
        //         })
        //     ]
        // );

        // this.columnPreview.install("virtual-grid", ".js-index-errors-tooltip", 
        //     (indexError: IndexErrorPerDocument, column: textColumn<IndexErrorPerDocument>, e: JQueryEventObject, 
        //      onValue: (context: any, valueToCopy?: string) => void) => {
        //     if (column.header === "Action" || column.header === "Show") {
        //         // do nothing
        //     } else if (column.header === "Date") {
        //         onValue(moment.utc(indexError.Timestamp), indexError.Timestamp);
        //     } else {
        //         const value = column.getCellValue(indexError);
        //         if (!_.isUndefined(value)) {
        //             onValue(generalUtils.escapeHtml(value), value);
        //         }
        //     }
        // });
        
        // this.registerDisposable(timeHelpers.utcNowWithMinutePrecision.subscribe(() => this.onTick()));
        
        indexErrors.syncMultiSelect();
    }

    // private showErrorDetails(errorIdx: number) {
    //     const view = new indexErrorDetails(this.filteredIndexErrors, errorIdx);
    //     app.showBootstrapDialog(view);
    // }

    refresh() {
        this.getErrorCount()
            .then(() => 
                this.errorInfoItems().forEach(x => {
                    x.refresh();
        }));
    }

    // private onTick() {
    //     // reset grid on tick - it neighter move scroll position not download data from remote, but it will render contents again, updating time 
    //     this.gridController().reset(false);
    // }

    // private fetchIndexErrors(): JQueryPromise<pagedResult<IndexErrorPerDocument>> {
    //     if (this.allIndexErrors === null) {
    //         return this.fetchRemoteIndexesError().then(list => {
    //             this.allIndexErrors = list;
    //             return this.filterItems(this.allIndexErrors);
    //         });
    //     }
    //
    //     return this.filterItems(this.allIndexErrors);
    // }

    // private fetchRemoteIndexesError(): JQueryPromise<IndexErrorPerDocument[]> {
    //    
    //     return new getIndexesErrorCommand(this.db)
    //         .execute()
    //         .then((result: Raven.Client.Documents.Indexes.IndexErrors[]) => {
    //             this.ignoreSearchCriteriaUpdatesMode = true;
    //
    //             const indexNamesAndCount = indexErrors.extractIndexNamesAndCount(result);
    //             const actionNamesAndCount = this.extractActionNamesAndCount(result);
    //
    //             this.allErroredIndexNames(indexNamesAndCount);
    //             //this.allErroredActionNames(actionNamesAndCount);
    //             this.selectedIndexNames(this.allErroredIndexNames().map(x => x.indexName));
    //             //this.selectedActionNames(this.allErroredActionNames().map(x => x.actionName));
    //
    //             indexErrors.syncMultiSelect();
    //
    //             this.ignoreSearchCriteriaUpdatesMode = false;
    //
    //             const mappedItems = this.mapItems(result);
    //             const itemWithMax = _.maxBy<IndexErrorPerDocument>(mappedItems, x => x.Timestamp);
    //             this.localLatestIndexErrorTime(itemWithMax ? itemWithMax.Timestamp : null);
    //             this.remoteLatestIndexErrorTime(this.localLatestIndexErrorTime());
    //             return mappedItems;
    //         });
    // }

    // private filterItems(list: IndexErrorPerDocument[]): JQueryPromise<pagedResult<IndexErrorPerDocument>> {
    //     const deferred = $.Deferred<pagedResult<IndexErrorPerDocument>>();
    //     let filteredItems = list;
    //     if (this.selectedIndexNames().length !== this.allErroredIndexNames().length) {
    //         filteredItems = filteredItems.filter(error => _.includes(this.selectedIndexNames(), error.IndexName));
    //     }
    //     // if (this.selectedActionNames().length !== this.allErroredActionNames().length) {
    //     //     filteredItems = filteredItems.filter(error => _.includes(this.selectedActionNames(), error.Action));
    //     // }
    //
    //     if (this.searchText()) {
    //         const searchText = this.searchText().toLowerCase();
    //        
    //         filteredItems = filteredItems.filter((error) => {
    //             return (error.Document && error.Document.toLowerCase().includes(searchText)) ||
    //                    error.Error.toLowerCase().includes(searchText)
    //        })
    //     }
    //    
    //     // save copy used for details viewer
    //     this.filteredIndexErrors = filteredItems;
    //    
    //     return deferred.resolve({
    //         items: filteredItems,
    //         totalResultCount: filteredItems.length
    //     });
    // }

    // private static extractIndexNamesAndCount(indexErrors: Raven.Client.Documents.Indexes.IndexErrors[]): Array<indexNameAndCount> {
    //     const array = indexErrors.filter(error => error.Errors.length > 0).map(errors => {
    //         return {
    //             indexName: errors.Name,
    //             count: errors.Errors.length
    //         }
    //     });
    //
    //     return _.sortBy(array, x => x.indexName.toLocaleLowerCase());
    // }

    // private extractActionNamesAndCount(indexErrors: Raven.Client.Documents.Indexes.IndexErrors[]): indexActionAndCount[] {
    //     const mappedItems: indexActionAndCount[] = _.flatMap(indexErrors,
    //         value => {
    //             return value.Errors.map(x => ({
    //                 actionName: x.Action,
    //                 count: 1
    //             }));
    //         });
    //
    //     const mappedReducedItems: indexActionAndCount[] = mappedItems.reduce((result: indexActionAndCount[], next: indexActionAndCount) => {
    //         var existing = result.find(x => x.actionName === next.actionName);
    //         if (existing) {
    //             existing.count += next.count;
    //         } else {
    //             result.push(next);
    //         }
    //         return result;
    //     }, []);
    //
    //     return _.sortBy(mappedReducedItems, x => x.actionName.toLocaleLowerCase());
    // }

    // private mapItems(indexErrors: Raven.Client.Documents.Indexes.IndexErrors[]): IndexErrorPerDocument[] {
    //     const mappedItems = _.flatMap(indexErrors, value => {
    //         return value.Errors.map((error: Raven.Client.Documents.Indexes.IndexingError): IndexErrorPerDocument =>
    //             ({
    //                 Timestamp: error.Timestamp,
    //                 Document: error.Document,
    //                 Action: error.Action,
    //                 Error: error.Error,
    //                 IndexName: value.Name
    //             }));
    //     });
    //    
    //     return _.orderBy(mappedItems, [x => x.Timestamp], ["desc"]);
    // }

    private onStatsChanged(stats: Raven.Server.NotificationCenter.Notifications.DatabaseStatsChanged) {
        this.remoteLatestIndexErrorTime(stats.LastIndexingErrorTime);
    }

    private onSearchCriteriaChanged() {
        //if (!this.ignoreSearchCriteriaUpdatesMode) {
            this.errorInfoItems().forEach(x => {
                x.searchText(this.searchText());
                x.selectedIndexNames(this.allSelectedIndexNames());
                x.refresh();
            })
        //}
    }

    clearIndexErrors() {
        const clearErrorsDialog = new clearIndexErrorsConfirm(this.allIndexesSelected() ? null : this.allSelectedIndexNames(), this.db);
        app.showBootstrapDialog(clearErrorsDialog);
            
        clearErrorsDialog.clearErrorsTask
            .done((errorsCleared: boolean) => { 
                if (errorsCleared) { 
                    this.refresh(); 
                } 
        });
    }
    
    toggleDetails(item: indexErrorInfoModel) {
        item.toggleDetails();
    }

    // todo - on model ???
    clearIndexErrors1() {
        alert("clear");
    }
}

export = indexErrors; 
