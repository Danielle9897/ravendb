/// <reference path="../../../../typings/tsd.d.ts"/>
import app = require("durandal/app");
import virtualGridController = require("widgets/virtualGrid/virtualGridController");
import columnPreviewPlugin = require("widgets/virtualGrid/columnPreviewPlugin");
import getIndexesErrorCommand = require("commands/database/index/getIndexesErrorCommand");
import database = require("models/resources/database");
import actionColumn = require("widgets/virtualGrid/columns/actionColumn");
import hyperlinkColumn = require("widgets/virtualGrid/columns/hyperlinkColumn");
import textColumn = require("widgets/virtualGrid/columns/textColumn");
import generalUtils = require("common/generalUtils");
import appUrl = require("common/appUrl");
import moment = require("moment");
import indexErrorDetails = require("viewmodels/database/indexes/indexErrorDetails");

class indexErrorInfoModel {

    db = ko.observable<database>();
    location = ko.observable<databaseLocationSpecifier>(); // V
    
    totalErrorCount = ko.observable<number>(); // computed
    showDetails = ko.observable(false); 
        
    badgeText: KnockoutComputed<string>; // V
    badgeClass: KnockoutComputed<string>; // V

    clearErrorsBtnText: KnockoutComputed<string>;
    clearErrorsBtnTooltip: KnockoutComputed<string>;
    
    indexErrors: IndexErrorPerDocument[] = null;
    filteredIndexErrors: IndexErrorPerDocument[] = null;

    selectedIndexNames = ko.observableArray<string>([]);
    selectedActionNames = ko.observableArray<string>([]);
    
    allIndexesSelected = ko.observable<boolean>();
    searchText = ko.observable<string>();
    
    gridController = ko.observable<virtualGridController<IndexErrorPerDocument>>();
    private columnPreview = new columnPreviewPlugin<IndexErrorPerDocument>();
    
    gridWasInitialized: boolean = false;
    gridId: KnockoutComputed<string>;

    errMsg = ko.observable<string>();
    
    constructor(db: database, location: databaseLocationSpecifier, count: number, errMessage: string = null) {
        this.db(db);
        this.location(location);
        
        this.totalErrorCount(count);
        this.errMsg(errMessage);
        
        this.initObservables();
    }
    
    initObservables(): void {
        this.badgeText = ko.pureComputed(() => {
            if (this.errMsg()) {
                return "Not Available";
            }
            return this.totalErrorCount() ? "Errors" : "Ok";
        });
        
        this.badgeClass = ko.pureComputed(() => {
            if (this.errMsg()) {
                return "state-warning";
            }
            return this.totalErrorCount() ? "state-danger" : "state-success";
        });

        this.gridId = ko.pureComputed(() => `${this.location().nodeTag}${this.location().shardNumber || ""}`);

        this.clearErrorsBtnText = ko.pureComputed(() => {
            if (this.allIndexesSelected()) {
                return "Clear errors (All indexes)";
            } else if (this.selectedIndexNames() && this.selectedIndexNames().length) {
                return "Clear errors (Selected indexes)";
            } else {
                return "Clear errors";
            }
        });
        
        this.clearErrorsBtnTooltip = ko.pureComputed(() => {
            if (this.location().shardNumber !== undefined) {
                return "Clear errors from this shard";
            } else {
                return "Click to clear errors from this node";
            }
        });
    }

    toggleDetails() {
        this.showDetails.toggle();
        if (this.showDetails() === true) {
            this.indexErrors = null;
            
            if (!this.gridWasInitialized) {
                this.gridInit();
                this.gridWasInitialized = true;
            }
            
            this.gridController().reset();
        }
    }
    
    private gridInit() {
        const grid = this.gridController();
        grid.headerVisible(true);
        grid.init(() => this.fetchIndexErrors(), () =>
            [
                new actionColumn<IndexErrorPerDocument>(grid, (error, index) => this.showErrorDetails(index), "Show", `<i class="icon-preview"></i>`, "72px",
                    {
                        title: () => 'Show indexing error details'
                    }),
                new hyperlinkColumn<IndexErrorPerDocument>(grid, x => x.IndexName, x => appUrl.forEditIndex(x.IndexName, this.db()), "Index name", "25%", {
                    sortable: "string",
                    customComparator: generalUtils.sortAlphaNumeric
                }),
                new hyperlinkColumn<IndexErrorPerDocument>(grid, x => x.Document, x => appUrl.forEditDoc(x.Document, this.db()), "Document Id", "20%", {
                    sortable: "string",
                    customComparator: generalUtils.sortAlphaNumeric
                }),
                new textColumn<IndexErrorPerDocument>(grid, x => generalUtils.formatUtcDateAsLocal(x.Timestamp), "Date", "20%", {
                    sortable: "string"
                }),
                new textColumn<IndexErrorPerDocument>(grid, x => x.Action, "Action", "10%", {
                    sortable: "string"
                }),
                new textColumn<IndexErrorPerDocument>(grid, x => x.Error, "Error", "15%", {
                    sortable: "string"
                })
            ]
        );

        const specialTooltipClass = `.js-index-errors-tooltip${this.gridId()}`;
        const specialContainerSelector = `.virtual-grid-class${this.gridId()}`;
        
        this.columnPreview.install(specialContainerSelector, specialTooltipClass,
            (indexError: IndexErrorPerDocument, column: textColumn<IndexErrorPerDocument>, e: JQueryEventObject,
             onValue: (context: any, valueToCopy?: string) => void) => {
            if (column.header === "Action" || column.header === "Show") {
                // do nothing
            } else if (column.header === "Date") {
                onValue(moment.utc(indexError.Timestamp), indexError.Timestamp);
            } else {
                const value = column.getCellValue(indexError);
                if (!_.isUndefined(value)) {
                    onValue(generalUtils.escapeHtml(value), value);
                }
            }
            
            console.log(specialContainerSelector);
            
        });
    }

    private fetchIndexErrors(): JQueryPromise<pagedResult<IndexErrorPerDocument>> {
        if (this.indexErrors === null) {
            return this.fetchRemoteIndexesErrors().then(list => {
                this.indexErrors = list;
                return this.filterItems(this.indexErrors);
            });
        }

        return this.filterItems(this.indexErrors);
    }
    
    private fetchRemoteIndexesErrors(): JQueryPromise<IndexErrorPerDocument[]> {
        return new getIndexesErrorCommand(this.db(), this.location())
            .execute()
            .then((result: Raven.Client.Documents.Indexes.IndexErrors[]) => this.mapItems(result))
            .fail(result => this.errMsg(result)); // todo check !!! and show in UI... // todo - clear error is all is ok !!!
    }

    private mapItems(indexErrors: Raven.Client.Documents.Indexes.IndexErrors[]): IndexErrorPerDocument[] {
        const mappedItems = _.flatMap(indexErrors, value => {
            return value.Errors.map((error: Raven.Client.Documents.Indexes.IndexingError): IndexErrorPerDocument =>
                ({
                    Timestamp: error.Timestamp,
                    Document: error.Document,
                    Action: error.Action,
                    Error: error.Error,
                    IndexName: value.Name
                }));
        });

        return _.orderBy(mappedItems, [x => x.Timestamp], ["desc"]);
    }

    private filterItems(list: IndexErrorPerDocument[]): JQueryPromise<pagedResult<IndexErrorPerDocument>> {
        const deferred = $.Deferred<pagedResult<IndexErrorPerDocument>>();
        
        let filteredItems = list;
        filteredItems = filteredItems.filter(error => _.includes(this.selectedIndexNames(), error.IndexName));
        filteredItems = filteredItems.filter(error => _.includes(this.selectedActionNames(), error.Action));
       
        if (this.searchText()) {
            const searchText = this.searchText().toLowerCase();

            filteredItems = filteredItems.filter((error) => {
                return (error.Document && error.Document.toLowerCase().includes(searchText)) ||
                    error.Error.toLowerCase().includes(searchText)
            })
        }

        this.filteredIndexErrors = filteredItems;

        return deferred.resolve({
            items: filteredItems,
            totalResultCount: filteredItems.length
        });
    }

    private showErrorDetails(errorIdx: number) {
        const view = new indexErrorDetails(this.filteredIndexErrors, errorIdx);
        app.showBootstrapDialog(view);
    }
    
    refresh() {
        if (this.showDetails()) {
            this.indexErrors = null;
            this.gridController().reset();
        }
    }
}

export = indexErrorInfoModel;
