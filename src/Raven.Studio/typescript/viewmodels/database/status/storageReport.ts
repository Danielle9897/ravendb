import viewModelBase = require("viewmodels/viewModelBase");
import getStorageReportCommand = require("commands/database/debug/getStorageReportCommand");
import getEnvironmentStorageReportCommand = require("commands/database/debug/getEnvironmentStorageReportCommand");
import protractedCommandsDetector = require("common/notifications/protractedCommandsDetector");
import generalUtils = require("common/generalUtils");
import app = require("durandal/app");
import storageReportItem = require("models/database/status/storageReportItem");
import d3 = require("d3");

type positionAndSizes = {
    dx: number,
    dy: number,
    x: number,
    y: number
}

class storageReport extends viewModelBase {

    static readonly animationLegth = 200;

    private rawData = [] as storageReportItemDto[];

    private currentPath: KnockoutComputed<Array<storageReportItem>>;

    private x: d3.scale.Linear<number, number>;
    private y: d3.scale.Linear<number, number>;
    private root: storageReportItem;
    private node = ko.observable<storageReportItem>();
    private treemap: d3.layout.Treemap<any>;
    private svg: d3.Selection<any>;
    private g: d3.Selection<any>;
    private tooltip: d3.Selection<any>;

    private w: number;
    private h: number;

    private transitioning = false;

    showLoader = ko.observable<boolean>(false);
    showPagesColumn: KnockoutObservable<boolean>;
    showEntriesColumn: KnockoutObservable<boolean>;

    constructor() {
        super();
        this.bindToCurrentInstance("onClick");
    }

    activate(args: any) {
        super.activate(args);

        this.initObservables();

        return new getStorageReportCommand(this.activeDatabase())
            .execute()
            .done(result => {
                this.rawData = result;
            });
    }

    compositionComplete() {
        super.compositionComplete();
        this.processData();
        this.initGraph();
        this.draw(undefined, undefined);
    }

    private initObservables() {
        this.currentPath = ko.pureComputed(() => {
            const node = this.node();

            const items = [] as Array<storageReportItem>;

            let currentItem = node;
            while (currentItem) {
                items.unshift(currentItem);
                currentItem = currentItem.parent;
            }

            return items;
        });

        this.showEntriesColumn = ko.pureComputed(() => {
            const node = this.node();
            return !!node.internalChildren.find(x => x.type === "table" || x.type === "tree");
        });

        this.showPagesColumn = ko.pureComputed(() => {
            const node = this.node();
            return !!node.internalChildren.find(x => x.type === "tree");
        });
    }

    private processData() {
        const data = this.rawData;

        const mappedData = data.map(x => this.mapReport(x));
        const totalSize = mappedData.reduce((p, c) => p + c.size, 0);
        const item = new storageReportItem("/", "Database", false, totalSize, mappedData);

        this.root = item;

        this.sortBySize(this.root);

        this.node(this.root);
    }

    private sortBySize(node: storageReportItem) {
        if (node.internalChildren && node.internalChildren.length) {
            node.internalChildren.forEach(x => this.sortBySize(x));

            node.internalChildren.sort((a, b) => d3.descending(a.size, b.size));
        }
    }

    private mapReport(reportItem: storageReportItemDto): storageReportItem {
        const dataFile = this.mapDataFile(reportItem.Report);
        const journals = this.mapJournals(reportItem.Report);

        return new storageReportItem(reportItem.Name,
            reportItem.Type.toLowerCase(),
            storageReport.showDisplayReportType(reportItem.Type),
            dataFile.size + journals.size,
            [dataFile, journals]);
    }

    private static showDisplayReportType(reportType: string): boolean {
        return reportType !== "Configuration" && reportType !== "Subscriptions";
    }

    private mapDataFile(report: Voron.Debugging.StorageReport): storageReportItem {
        const dataFile = report.DataFile;

        const storageItem = new storageReportItem("Datafile", "data", false, dataFile.AllocatedSpaceInBytes);
        storageItem.lazyLoadChildren = true;

        return storageItem;
    }

    private mapDetailedReport(report: Voron.Debugging.DetailedStorageReport, d: storageReportItem) {
        d.lazyLoadChildren = false;

        const tables = this.mapTables(report.Tables);
        const trees = this.mapTrees(report.Trees, "Trees");
        const freeSpace = new storageReportItem("Free", "free", false, report.DataFile.FreeSpaceInBytes, []);

        d.internalChildren = [tables, trees, freeSpace];
    }

    private mapTables(tables: Voron.Data.Tables.TableReport[]): storageReportItem {
        const mappedTables = tables.map(x => this.mapTable(x));

        return new storageReportItem("Tables", "tables", false, mappedTables.reduce((p, c) => p + c.size, 0), mappedTables);
    }

    private mapTable(table: Voron.Data.Tables.TableReport): storageReportItem {
        const structure = this.mapTrees(table.Structure, "Structure");

        const data = new storageReportItem("Table Data", "table_data", false, table.DataSizeInBytes, []);
        const indexes = this.mapTrees(table.Indexes, "Indexes");

        const totalSize = structure.size + table.DataSizeInBytes + indexes.size;

        const tableItem = new storageReportItem(table.Name, "table", true, totalSize, [
            structure,
            data,
            indexes
        ]);

        tableItem.numberOfEntries = table.NumberOfEntries;

        return tableItem;
    }

    private mapTrees(trees: Voron.Debugging.TreeReport[], name: string): storageReportItem {
        return new storageReportItem(name, name.toLowerCase(), false, trees.reduce((p, c) => p + c.AllocatedSpaceInBytes, 0), trees.map(x => this.mapTree(x)));
    }

    private mapTree(tree: Voron.Debugging.TreeReport): storageReportItem {
        const item = new storageReportItem(tree.Name, "tree", true, tree.AllocatedSpaceInBytes, []);
        item.pageCount = tree.PageCount;
        item.numberOfEntries = tree.NumberOfEntries;
        return item;
    }

    private mapJournals(report: Voron.Debugging.StorageReport): storageReportItem {
        const journals = report.Journals;

        const mappedJournals = journals.map(journal => 
            new storageReportItem(
                "Journal #" + journal.Number,
                "journal",
                false,
                journal.AllocatedSpaceInBytes,
                []
            ));

        return new storageReportItem("Journals", "journals", false, mappedJournals.reduce((p, c) => p + c.size, 0), mappedJournals);
    }

    private initGraph() {
        this.detectContainerSize();
        this.x = d3.scale.linear().range([0, this.w]);
        this.y = d3.scale.linear().range([0, this.h]);

        this.svg = d3.select("#storage-report-container .chart")
            .append("svg:svg")
            .attr("width", this.w)
            .attr("height", this.h)
            .attr("transform", "translate(.5,.5)");

        this.addHashing();
    }

    private detectContainerSize() {
        const $chartNode = $("#storage-report-container .chart");
        this.w = $chartNode.width();
        this.h = $chartNode.height();
    }

    private addHashing() {
        const defs = this.svg.append('defs');
        const g = defs.append("pattern")
            .attr('id', 'hash')
            .attr('patternUnits', 'userSpaceOnUse')
            .attr('width', '10')
            .attr('height', '10')
            .append("g").style("fill", "none")
            .style("stroke", "grey")
            .style("stroke-width", 1);
        g.append("path").attr("d", "M0,0 l10,10");
        g.append("path").attr("d", "M10,0 l-10,10");
    }

    private getChildren(node: storageReportItem, depth: number) {
        return depth === 0 ? node.internalChildren : [];
    }

    private draw(goingIn: boolean, previousNode: storageReportItem) {

        const levelDown = goingIn === true;
        const levelUp = goingIn === false;

        this.treemap = d3.layout.treemap<any>()
            .children((n, depth) => this.getChildren(n, depth))
            .value(d => d.size)
            .size([this.w, this.h]);

        this.tooltip = d3.select(".chart-tooltip");

        const oldLocation: positionAndSizes = {
            dx: this.node().dx,
            dy: this.node().dy,
            x: this.node().x,
            y: this.node().y
        };

        const nodes = this.treemap.nodes(this.node())
            .filter(n => !n.children);

        if (levelDown) {
            this.animateZoomIn(nodes, oldLocation);
        } else if (levelUp) {
            this.animateZoomOut(nodes);
        } else {
            // initial state
            this.svg.select(".treemap")
                .remove();
            const container = this.svg.append("g")
                .classed("treemap", true);
            this.drawNewTreeMap(nodes, container);
        }
    }

    private animateZoomIn(nodes: storageReportItem[], oldLocation: positionAndSizes) {
        this.transitioning = true;

        const oldContainer = this.svg.select(".treemap");
        const oldCells = oldContainer.selectAll("g.cell");

        const newGroup = this.svg.append("g")
            .classed("treemap", true);

        const scaleX = this.w / oldLocation.dx;
        const scaleY = this.h / oldLocation.dy;
        const transX = -oldLocation.x * scaleX;
        const transY = -oldLocation.y * scaleY;

        oldContainer
            .selectAll("text")
            .transition()
            .duration(storageReport.animationLegth / 4)
            .style('opacity', 0);

        oldContainer
            .transition()
            .duration(storageReport.animationLegth)
            .attr("transform", "translate(" + transX + "," + transY + ")scale(" + scaleX + "," + scaleY + ")")
            .each("end", () => {
                const newCells = this.drawNewTreeMap(nodes, newGroup);
                newCells
                    .style('opacity', 0)
                    .transition()
                    .duration(storageReport.animationLegth)
                    .style('opacity', 1)
                    .each("end", () => {
                        oldContainer.remove();
                        this.transitioning = false;
                    });
            });
    }

    private animateZoomOut(nodes: storageReportItem[]) {
        this.transitioning = true;

        const oldContainer = this.svg.select(".treemap");
        const oldCells = oldContainer.selectAll("g.cell");

        const newGroup = this.svg.append("g")
            .classed("treemap", true);

        const newCells = this.drawNewTreeMap(nodes, newGroup);

        newCells
            .style('opacity', 0)
            .transition()
            .duration(storageReport.animationLegth)
            .style('opacity', 1)
            .each("end", () => {
                oldContainer.remove();
                this.transitioning = false;
            });
    }

    private drawNewTreeMap(nodes: storageReportItem[], container: d3.Selection<any>) {
        const self = this;
        const showTypeOffset = 7;
        const showTypePredicate = (d: storageReportItem) => d.showType && d.dy > 22 && d.dx > 20;

        const cell = container.selectAll("g.cell-no-such") // we always select non-existing nodes to draw from scratch - we don't update elements here
            .data(nodes)
            .enter().append("svg:g")
            .attr("class", d => "cell " + d.type)
            .attr("transform", d => "translate(" + d.x + "," + d.y + ")")
            .on("click", d => this.onClick(d, true))
            .on("mouseover", d => this.onMouseOver(d))
            .on("mouseout", d => this.onMouseOut(d))
            .on("mousemove", d => this.onMouseMove(d));

        const rectangles = cell.append("svg:rect")
            .attr("width", d => d.dx - 1)
            .attr("height", d => d.dy - 1);

        rectangles
            .filter(x => x.hasChildren() || x.lazyLoadChildren)
            .style('cursor', 'pointer');

        cell.append("svg:text")
            .filter(d => d.dx > 20 && d.dy > 8)
            .attr("x", d => d.dx / 2)
            .attr("y", d => showTypePredicate(d) ? d.dy / 2 - showTypeOffset : d.dy / 2)
            .attr("dy", ".35em")
            .attr("text-anchor", "middle")
            .text(d => d.name)
            .each(function (d) {
                self.wrap(this, d.dx);
            });

        cell.filter(d => showTypePredicate(d))
            .append("svg:text")
            .attr("x", d => d.dx / 2)
            .attr("y", d => showTypePredicate(d) ? d.dy / 2 + showTypeOffset : d.dy / 2)
            .attr("dy", ".35em")
            .attr("text-anchor", "middle")
            .text(d => _.upperFirst(d.type))
            .each(function (d) {
                self.wrap(this, d.dx);
            });

        return cell;
    }

    wrap($self: any, width: number) {
        let self = d3.select($self),
            textLength = (self.node() as any).getComputedTextLength(),
            text = self.text();
        while (textLength > (width - 6) && text.length > 0) {
            text = text.slice(0, -1);
            self.text(text + '...');
            textLength = (self.node() as any).getComputedTextLength();
        }
    } 

    private loadDetailedReport(d: storageReportItem): JQueryPromise<detailedStorageReportItemDto> {
        if (!d.lazyLoadChildren) {
            return;
        }

        const env = d.parent;

        const showLoaderTimer = setTimeout(() => {
            this.showLoader(true);
        }, 100);

        return new getEnvironmentStorageReportCommand(this.activeDatabase(), env.name, _.capitalize(env.type))
            .execute()
            .done((envReport) => {
                this.mapDetailedReport(envReport.Report, d);
            })
            .always(() => {
                if (this.showLoader()) {
                    this.showLoader(false);
                } else {
                    clearTimeout(showLoaderTimer);
                }
            });
    }

    onClick(d: storageReportItem, goingIn: boolean) {
        if (this.transitioning || this.node() === d) {
            return;
        }

        if (d.lazyLoadChildren) {
            const requestExecution = protractedCommandsDetector.instance.requestStarted(500);

            this.loadDetailedReport(d)
                .done(() => {
                    const prev = this.node();
                    this.node(d);
                    this.draw(true, prev);
                })
                .always(() => requestExecution.markCompleted());

            if (d3.event) {
                (d3.event as any).stopPropagation();
            }
            return;
        }

        if (!d.internalChildren || !d.internalChildren.length) {
            return;
        }

        const prev = this.node();
        this.node(d);
        this.draw(goingIn, prev);

        if (d3.event) {
            (d3.event as any).stopPropagation();
        }
    }

    private onMouseMove(d: storageReportItem) {
        let [x, y] = d3.mouse(this.svg.node());

        const tooltipWidth = $(".chart-tooltip").width() + 20;

        x = Math.min(x, Math.max(this.w - tooltipWidth, 0));

        this.tooltip
            .style("left", (x + 10) + "px")
            .style("top", (y + 10) + "px");
    }

    private onMouseOver(d: storageReportItem) {
        this.tooltip.transition()
            .duration(200)
            .style("opacity", 1);
        let html = "Name: " + d.name;
        if (d.showType) {
            html += "<br />Type: <strong>" + _.upperFirst(d.type) + "</strong>";
        }
        if (this.shouldDisplayNumberOfEntires(d)) {
            html += "<br />Entries: <strong>" + d.numberOfEntries.toLocaleString() + "</strong>";
        }
        html += " <br /> <span class='size'>Size: <strong>" + generalUtils.formatBytesToSize(d.size) + "</strong></span>";

        this.tooltip.html(html);
        this.onMouseMove(d);
    }

    private shouldDisplayNumberOfEntires(d: storageReportItem) {
        return d.type === "tree" || d.type === "table";
    }

    private onMouseOut(d: storageReportItem) {
        this.tooltip.transition()
            .duration(500)
            .style("opacity", 0);	
    }

   
}

export = storageReport;    
