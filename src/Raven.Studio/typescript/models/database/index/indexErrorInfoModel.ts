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
    isShardInfo = ko.observable<boolean>();
    nodeTag = ko.observable<string>();
    shardNumber = ko.observable<string>();
    totalErrorCount = ko.observable<number>();

    badgeText: KnockoutComputed<string>;
    badgeClass: KnockoutComputed<string>;

    clearErrorsBtnText: KnockoutComputed<string>;
    clearErrorsBtnTooltip: KnockoutComputed<string>;

    showDetails = ko.observable(false);
    
    indexErrors: IndexErrorPerDocument[] = null;
    filteredIndexErrors: IndexErrorPerDocument[] = null;
    
    gridController = ko.observable<virtualGridController<IndexErrorPerDocument>>();
    private columnPreview = new columnPreviewPlugin<IndexErrorPerDocument>();

    selectedIndexNames = ko.observableArray<string>([]);
    selectedActionNames = ko.observableArray<string>([]);
    
    allIndexesSelected = ko.observable<boolean>();
    searchText = ko.observable<string>();
    
    gridWasInitialized: boolean = false;
    gridId: KnockoutComputed<string>;

    constructor(db: database, nodeTag: string, shardNumber: string, count: number) {
        this.isShardInfo(!!shardNumber && !!nodeTag); // ???
        
        this.db(db);
        this.nodeTag(nodeTag);
        this.shardNumber(shardNumber);
        this.totalErrorCount(count);
    
        this.initObservables();
    }
    
    initObservables(): void {
        this.badgeText = ko.pureComputed(() => {
            return this.totalErrorCount() ? "Errors" : "Ok";
        });
        
        this.badgeClass = ko.pureComputed(() => {
            return this.totalErrorCount() ? "state-danger" : "state-success";
        });
        
        this.gridId = ko.pureComputed(() => `${this.nodeTag() || ''}${this.shardNumber() || ''}`);

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
            if (!this.isShardInfo()) {
                return "Click to clear errors";
            } else {
                return "Clear errors from this shard";
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
        });
    }

    private fetchIndexErrors(): JQueryPromise<pagedResult<IndexErrorPerDocument>> {
        if (this.indexErrors === null) {
            return this.fetchRemoteIndexesError().then(list => {
                this.indexErrors = list;
                return this.filterItems(this.indexErrors);
            });
        }

        return this.filterItems(this.indexErrors);
    }
    
    private fetchRemoteIndexesError(): JQueryPromise<IndexErrorPerDocument[]> {
        return new getIndexesErrorCommand(this.db(), this.nodeTag(), this.shardNumber())
            .execute()
            .then((result: Raven.Client.Documents.Indexes.IndexErrors[]) => this.mapItems(result));
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
