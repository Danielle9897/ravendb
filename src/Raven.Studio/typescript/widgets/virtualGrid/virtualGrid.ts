﻿/// <reference path="../../../typings/tsd.d.ts"/>

import virtualRow = require("widgets/virtualGrid/virtualRow");
import pagedResult = require("widgets/virtualGrid/pagedResult");
import itemFetch = require("widgets/virtualGrid/itemFetch");
import virtualColumn = require("widgets/virtualGrid/virtualColumn");
import virtualGridConfig = require("widgets/virtualGrid/virtualGridConfig");
import virtualGridConfigDefaults = require("widgets/virtualGrid/virtualGridConfigDefaults");
import textColumn = require("widgets/virtualGrid/textColumn");
import checkedColumn = require("widgets/virtualGrid/checkedColumn");

class virtualGrid<T> {
    private items: T[] = []; // The items loaded asynchronously.
    private totalItemCount: number | null = null;
    private virtualRows: virtualRow[] = []; // These are the fixed number of elements that get displayed on screen. Each virtual row displays an element from .items array. As the user scrolls, rows will be recycled to represent different items.
    private gridId: string;
    private $gridElement: JQuery;
    private $viewportElement: JQuery;
    private gridElementHeight: number;
    private virtualHeight = ko.observable(0);
    private scrollAnimationFrameHandle = 0;
    private isLoading = ko.observable(false);
    private queuedFetch: itemFetch | null = null;
    private columns = ko.observableArray<virtualColumn>();
    private isSelectAllChecked = ko.observable<boolean | null>(false);
    private isGridVisible = false;
    private selectedIndices: number[] = [];
    private renderHandle = 0;
    private settings = new virtualGridConfigDefaults();
    
    private static readonly minItemFetchCount = 100;
    private static readonly viewportSelector = ".viewport";
    private static readonly viewportScrollerSelector = ".viewport-scroller";

    constructor(params: virtualGridConfig<T>) {
        this.gridId = "vg-" + (1 + Math.random()).toString().replace(".", "");
        
        // Configure the grid using the parameters passed in from HTML.
        $.extend(this.settings, params);
        this.initializeColumns();
        this.isSelectAllChecked.subscribe(allSelected => this.selectAllChanged(allSelected));
        if (params.resetItems) {
            params.resetItems.subscribe(() => this.resetItems());
        }
        this.fetchItems(0, 100);
    }

    // Called by Knockout once the grid has been rendered.
    private afterRender() {
        this.initializeUIElements();
    }

    private initializeUIElements() {
        this.$gridElement = this.findGridElement();
        this.gridElementHeight = this.$gridElement.height();
        this.$gridElement.on("click", e => this.gridClicked(e));
        this.$viewportElement = this.$gridElement.find(virtualGrid.viewportSelector);
        this.initializeVirtualRows();
        this.$gridElement.find(virtualGrid.viewportSelector).on("scroll", () => this.gridScrolled());
    }

    private initializeVirtualRows() {
        this.virtualRows = this.createVirtualRows();
        this.$viewportElement
            .find(virtualGrid.viewportScrollerSelector)
            .empty()
            .append(this.virtualRows.map(r => r.element[0]));
    }

    private createVirtualRows(): virtualRow[] {
        const height = Math.max(100, this.gridElementHeight);
        const rowsNeededToCoverViewport = Math.ceil(height / virtualRow.height);
        const desiredRowCount = rowsNeededToCoverViewport * 2;
        const rows: virtualRow[] = [];
        rows.length = desiredRowCount;
        for (let i = 0; i < desiredRowCount; i++) {
            rows[i] = new virtualRow();
        }

        return rows;
    }

    private initializeColumns() {
        // If the consumer code passed in some columns, use those.
        if (this.settings.columns && this.settings.columns.length > 0) {
            // Also, insert the row selection checkbox column if we're configured to do so.
            if (this.settings.showRowSelectionCheckbox) {
                this.columns([new checkedColumn() as virtualColumn].concat(this.settings.columns));
            } else {
                this.columns(this.settings.columns);
            }
        }
    }

    private gridScrolled() {
        window.cancelAnimationFrame(this.scrollAnimationFrameHandle);
        this.scrollAnimationFrameHandle = window.requestAnimationFrame(() => this.render());
    }

    private render() {
        // The grid may not be visible if the results returned quickly and we haven't finished initializing the UI.
        // In such a case, we queue up a render to occur later.
        var isGridVisible = this.checkGridVisibility();
        if (isGridVisible) {
            this.checkForUpdatedGridHeight();
            this.layoutVirtualRowPositions();
            this.fillDataIntoRows();
        } else {
            // Grid isn't yet visible. Queue up a render later.
            window.cancelAnimationFrame(this.renderHandle);
            this.renderHandle = window.requestAnimationFrame(() => this.render());
        }
    }

    private findGridElement(): JQuery {
        const element = $(document.querySelector("#" + this.gridId));
        if (element.length === 0) {
            throw new Error("Couldn't find grid element with ID " + this.gridId);
        }

        return element;
    }

    private fetchItems(skip: number, take: number): void {
        if (this.isLoading()) {
            this.queuedFetch = { skip: skip, take: take };
        } else {
            this.isLoading(true);
            const safeSkip = skip;
            let safeTake = take;
            if (this.totalItemCount != null && skip > this.totalItemCount) {
                skip = this.totalItemCount;
            }
            if (this.totalItemCount != null && (skip + take) > this.totalItemCount) {
                safeTake = this.totalItemCount - skip;
            }

            if (safeTake > 0) {
                this.settings.fetcher(safeSkip, safeTake)
                    .then((results: pagedResult<T>) => this.chunkFetched(results, safeSkip, safeTake))
                    .fail(error => this.chunkFetchFailed(error, skip, safeTake))
                    .always(() => {
                        // When we're done loading, run the next queued fetch as necessary.
                        this.isLoading(false);
                        this.runQueuedFetch();
                    });
            }
        }
    }

    private checkForUpdatedGridHeight(): number {
        var oldHeight = this.gridElementHeight;
        var newHeight = this.$gridElement.height();
        this.gridElementHeight = newHeight;

        // If the grid grew taller, we may need more virtual rows.
        if (newHeight > oldHeight) {
            this.initializeVirtualRows();
        }
        
        return newHeight;
    }

    private checkGridVisibility(): boolean {
        // If we've already determined the grid is visible, roll with that.
        if (this.isGridVisible) {
            return true;
        }

        // Grid hasn't yet become visible. Do the more expensive JQuery call.
        this.isGridVisible = this.$gridElement.is(":visible");
        return this.isGridVisible;
    }

    private runQueuedFetch() {
        if (this.queuedFetch) {
            const { skip, take } = this.queuedFetch;
            this.queuedFetch = null;

            // The previous fetch may have fetched some or all of the items we're about to fetch now.
            // So, before running the queued fetch, modify it to fetch the next chunk of unavailable items.
            let indexOfNextUnavailableChunk = skip;
            for (let i = skip; i < this.items.length; i++) {
                if (!this.items[i]) {
                    indexOfNextUnavailableChunk = i;
                    break;
                }
            }

            this.fetchItems(indexOfNextUnavailableChunk, take);
        }
    }

    private layoutVirtualRowPositions() {
        // This is hot path, called multiple times when scrolling. 
        // Keep allocations to a minimum.

        // Determine the view port.
        const scrollTop = this.$viewportElement.scrollTop();
        const scrollBottom = scrollTop + this.gridElementHeight;
        let positionCheck = scrollTop;
        const columns = this.columns();
        const lastPossibleRowY = this.virtualHeight() - virtualRow.height; // Find out the last possible row in the grid so that we don't place virtual rows beneath this.

        while (positionCheck < scrollBottom && positionCheck <= lastPossibleRowY) {
            let rowAtPosition = this.findRowAtY(positionCheck);
            if (!rowAtPosition) {
                // There's no row at that spot. Find one we can put there.
                rowAtPosition = this.getOffscreenRow(scrollTop, scrollBottom);

                // Populate it with data.
                const rowIndex = Math.floor(positionCheck / virtualRow.height);
                const isChecked = this.selectedIndices.indexOf(rowIndex) !== -1;
                rowAtPosition.populate(this.items[rowIndex], rowIndex, isChecked, columns);
            }

            const newPositionCheck = rowAtPosition.top + virtualRow.height;
            if (newPositionCheck <= positionCheck) {
                throw new Error("Virtual grid defect: next position check was smaller or equal to last check, resulting in potentially infinite loop.");
            }

            positionCheck = newPositionCheck;
        }
    }

    private fillDataIntoRows() {
        // Find which rows are visible.
        const scrollTop = this.$viewportElement.scrollTop();
        const scrollBottom = scrollTop + this.gridElementHeight;
        const columns = this.columns();
        let firstVisibleRowIndex: number | null = null;
        let totalVisible = 0;
        for (let i = 0; i < this.virtualRows.length; i++) {
            const virtualRow = this.virtualRows[i];
            const isVisible = !virtualRow.isOffscreen(scrollTop, scrollBottom);
            if (isVisible) {
                firstVisibleRowIndex = firstVisibleRowIndex === null ? virtualRow.index : Math.min(virtualRow.index, firstVisibleRowIndex);
                totalVisible++;

                // Fill it with the data we've got loaded. If there's no data, it will display the loading indicator.
                const isRowChecked = this.selectedIndices.indexOf(virtualRow.index) !== -1;
                virtualRow.populate(this.items[virtualRow.index], virtualRow.index, isRowChecked, columns);
            }
        }

        // Of the visible rows, are we missing items for any of them?
        let needsToFetch = false;
        if (firstVisibleRowIndex !== null) {
            for (let i = firstVisibleRowIndex; i < firstVisibleRowIndex + totalVisible; i++) {
                if (!this.items[i]) {
                    needsToFetch = true;
                    break;
                }
            }
        }

        // We're missing items. Fetch them.
        if (needsToFetch) {
            // Take about 50 items before the first visible index (in case we're scrolling up)
            const skip = Math.max(0, firstVisibleRowIndex - 50);
            const take = Math.max(virtualGrid.minItemFetchCount, totalVisible);
            this.fetchItems(skip, take);
        }
    }

    private findRowAtY(y: number): virtualRow | null {
        // This is hot path, called multiple times when scrolling. 
        // Keep allocations to a minimum.
        for (let i = 0; i < this.virtualRows.length; i++) {
            const vRow = this.virtualRows[i];
            const vRowTop = vRow.top;
            const vRowBottom = vRowTop + virtualRow.height;
            if (vRowTop <= y && vRowBottom > y) {
                return vRow;
            }
        }

        return null;
    }

    private getOffscreenRow(viewportTop: number, viewportBottom: number): virtualRow {
        // This is hot path, called multiple times when scrolling.
        // Keep allocations to a minimum.
        for (let i = 0; i < this.virtualRows.length; i++) {
            const row = this.virtualRows[i];
            if (row.isOffscreen(viewportTop, viewportBottom)) {
                return row;
            }
        }

        throw new Error(`Virtual grid defect: couldn't find an offscreen row to recycle. viewportTop = ${viewportTop}, viewportBottom = ${viewportBottom}, recycle row count = ${this.virtualRows.length}`);
    }

    private chunkFetchFailed(error: any, skip: number, take: number) {
        // Any rows displaying these items, show an error.
        const endIndex = skip + take;
        const failedRows = this.virtualRows
            .filter(r => !r.hasData && r.index >= skip && r.index <= endIndex);
        failedRows.forEach(r => r.dataLoadError(error));
    }

    private chunkFetched(results: pagedResult<T>, skip: number, take: number) {
        if (!this.columns() || this.columns().length === 0) {
            this.assignColumnFromItems(results.items);
        }

        // Add these results to the .items array as necessary.
        this.items.length = results.totalResultCount;
        this.totalItemCount = results.totalResultCount;
        this.virtualHeight(results.totalResultCount * virtualRow.height);
        const endIndex = skip + results.items.length;
        for (let i = 0; i < results.items.length; i++) {
            const rowIndex = i + skip;
            this.items[rowIndex] = results.items[i];
        }

        this.render();
    }

    private assignColumnFromItems(items: T[]): void {
        const propertySet = {};
        const itemPropertyNames: string[] = [].concat.apply([], items.map(i => Object.keys(i)));
        const uniquePropertyNames = new Set(itemPropertyNames);
        const columnNames = Array.from(uniquePropertyNames);
        const viewportWidth = this.$viewportElement.prop("clientWidth");
        const checkedColumnWidth = this.settings.showRowSelectionCheckbox ? checkedColumn.columnWidth : 0;
        const columnWidth = Math.floor(viewportWidth / columnNames.length) - checkedColumnWidth + "px";
        
        // Put Id and Name columns first.
        const prioritizedColumns = ["Id", "Name"];
        prioritizedColumns
            .reverse()
            .forEach(c => {
                const columnIndex = columnNames.indexOf(c);
                if (columnIndex >= 0) {
                    columnNames.splice(columnIndex, 1);
                    columnNames.unshift(c);
                }
            });

        // Insert the row selection checkbox column as necessary.
        const initialColumns: virtualColumn[] = this.settings.showRowSelectionCheckbox ? [new checkedColumn()] : [];
        this.columns(initialColumns.concat(columnNames.map(p => new textColumn(p, p, columnWidth))));
    }

    private gridClicked(e: JQueryEventObject) {
        if (e.target) {
            const $target = $(e.target);
            // If we clicked the the checked column header, toggle select all.
            if ($target.hasClass("checked-column-header")) {
                this.isSelectAllChecked(!this.isSelectAllChecked());
            } else if ($target.hasClass("checked-cell-input")) {
                // If we clicked a checked cell, toggle its selected state.
                const rowIndex = this.findRowIndexForCell($target);
                if (rowIndex !== null) {
                    this.toggleRowSelected(rowIndex);
                }
            }
        }
    }

    private findRowIndexForCell(cellElement: JQuery): number | null {
        return this.virtualRows
            .filter(r => r.element.find(cellElement).length > 0)
            .map(r => r.index)[0];
    }

    /**
     * Clears the items from the grid and refetches the first chunk of items.
     */
    private resetItems() {
        this.items.length = 0;
        this.totalItemCount = null;
        this.queuedFetch = null;
        this.isLoading(false);
        this.virtualRows.forEach(r => r.reset());
        this.fetchItems(0, 100);
    }

    private toggleRowSelected(rowIndex: number) {
        const selectionIndex = this.selectedIndices.indexOf(rowIndex);
        if (selectionIndex === -1) {
            this.selectedIndices.push(rowIndex);
        } else {
            this.selectedIndices.splice(selectionIndex, 1);
        }

        // If we had all selected or none selected, remove that state.
        if (this.isSelectAllChecked() === true || this.isSelectAllChecked() === false) {
            this.isSelectAllChecked(null);
            $(".checked-column-header").prop("checked", false);
        }

        this.render();
    }

    /*
     * Handles when the "select all" observable changes, either by clicking the check column header or via code.
     */
    private selectAllChanged(allSelected: boolean | null) {
        // When selectAll is set to false, remove all selected indices.
        if (allSelected === false) {
            this.selectedIndices.length = 0;
        } else if (allSelected === true) {
            this.selectedIndices.length = this.items.length;
            for (let i = 0; i < this.selectedIndices.length; i++) {
                this.selectedIndices[i] = i;
            }
        }

        // Note: allSelected can be set to null, meaning some items are checked while others are not.

        this.render();
    }

    /**
     * Installs the virtual grid component if it's not already installed.
     */
    static install() {
        const componentName = "virtual-grid";
        if (!ko.components.isRegistered(componentName)) {
            // TODO: we may want to move this to an HTML file that's fetched with RequireJS. Knockout components do support this.
            ko.components.register(componentName, {
                viewModel: virtualGrid,
                template: `
<div class="virtual-grid" data-bind="attr: { id: gridId }">
    <!-- Columns -->
    <div class="column-container" data-bind="foreach: columns, visible: settings.showColumns"><div class="column" data-bind="style: { width: $data.width }"><strong data-bind="html: $data.display"></strong></div></div>    

    <!-- Viewport -->
    <!-- The viewport is the section of the grid showing visible rows -->
    <div class="viewport" data-bind="css: { 'columns-visible': settings.showColumns }">

        <!-- The viewport scroller is the very tall scrolling part -->
        <div class="viewport-scroller" data-bind="style: { height: virtualHeight() + 'px' }, template: { afterRender: afterRender.bind($data) }">

        </div>
    </div>
    
</div>
`
            });
        }
    }
}

export = virtualGrid;