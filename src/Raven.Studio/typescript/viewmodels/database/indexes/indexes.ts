import viewModelBase = require("viewmodels/viewModelBase");
import index = require("models/database/index/index");
import appUrl = require("common/appUrl");
import saveIndexLockModeCommand = require("commands/database/index/saveIndexLockModeCommand");
import app = require("durandal/app");
import changeSubscription = require("common/changeSubscription");
import indexReplaceDocument = require("models/database/index/indexReplaceDocument");
import getPendingIndexReplacementsCommand = require("commands/database/index/getPendingIndexReplacementsCommand");
import cancelSideBySizeConfirm = require("viewmodels/database/indexes/cancelSideBySizeConfirm");
import deleteIndexesConfirm = require("viewmodels/database/indexes/deleteIndexesConfirm");
import forceIndexReplace = require("commands/database/index/forceIndexReplace");
import saveIndexPriorityCommand = require("commands/database/index/saveIndexPriorityCommand");
import getIndexesStatsCommand = require("commands/database/index/getIndexesStatsCommand");
import getIndexesStatusCommand = require("commands/database/index/getIndexesStatusCommand");
import resetIndexCommand = require("commands/database/index/resetIndexCommand");
import toggleIndexingCommand = require("commands/database/index/toggleIndexingCommand");
import eventsCollector = require("common/eventsCollector");
import enableIndexCommand = require("commands/database/index/enableIndexCommand");
import disableIndexCommand = require("commands/database/index/disableIndexCommand");

type indexGroup = {
    entityName: string;
    indexes: KnockoutObservableArray<index>;
    groupHidden: KnockoutObservable<boolean>;
};

class indexes extends viewModelBase {

    indexGroups = ko.observableArray<indexGroup>();
    sortedGroups: KnockoutComputed<indexGroup[]>;
    newIndexUrl = appUrl.forCurrentDatabase().newIndex;
    searchText = ko.observable<string>();
    lockModeCommon: KnockoutComputed<string>;
    selectedIndexesName = ko.observableArray<string>();
    indexesSelectionState: KnockoutComputed<checkbox>;

    spinners = {
        globalStartStop: ko.observable<boolean>(false),
        globalLockChanges: ko.observable<boolean>(false),
        localPriority: ko.observableArray<string>([]),
        localLockChanges: ko.observableArray<string>([]),
        localState: ko.observableArray<string>([])
    }

    indexingEnabled = ko.observable<boolean>(true);

    resetsInProgress = new Set<string>();

    constructor() {
        super();
        this.initObservables();
        this.bindToCurrentInstance(
            "lowPriority", "highPriority", "normalPriority",
            "resetIndex", "deleteIndex",
            "unlockIndex", "lockIndex", "lockErrorIndex", "lockSideBySide",
            "enableIndex", "disableIndex",
            "unlockSelectedIndexes", "lockSelectedIndexes", "lockSideBySideSelectedIndexes", "lockErrorSelectedIndexes",
            "deleteSelectedIndexes", "startIndexing", "stopIndexing", "resumeIndexing", "pauseUntilRestart", "toggleSelectAll"
        );
    }

    private getAllIndexes(): index[] {
        const all: index[] = [];
        this.indexGroups().forEach(g => all.push(...g.indexes()));
        return _.uniq(all);
    }

    private getSelectedIndexes(): Array<index> {
        const selectedIndexes = this.selectedIndexesName();
        return this.getAllIndexes().filter(x => _.includes(selectedIndexes, x.name));
    }

    private initObservables() {
        this.searchText.throttle(200).subscribe(() => this.filterIndexes());

        this.sortedGroups = ko.computed<indexGroup[]>(() => {
            var groups = this.indexGroups().slice(0).sort((l, r) => l.entityName.toLowerCase() > r.entityName.toLowerCase() ? 1 : -1);

            groups.forEach((group: { entityName: string; indexes: KnockoutObservableArray<index> }) => {
                group.indexes(group.indexes().slice(0).sort((l: index, r: index) => l.name.toLowerCase() > r.name.toLowerCase() ? 1 : -1));
            });

            return groups;
        });

        this.lockModeCommon = ko.computed(() => {
            const selectedIndexes = this.getSelectedIndexes();
            if (selectedIndexes.length === 0)
                return "None";

            const firstLockMode = selectedIndexes[0].lockMode();
            for (let i = 1; i < selectedIndexes.length; i++) {
                if (selectedIndexes[i].lockMode() !== firstLockMode) {
                    return "Mixed";
                }
            }
            return firstLockMode;
        });
        this.indexesSelectionState = ko.pureComputed<checkbox>(() => {
            const selectedCount = this.selectedIndexesName().length;
            const indexesCount = this.getAllIndexes().length;
            if (indexesCount && selectedCount === indexesCount)
                return checkbox.Checked;
            if (selectedCount > 0)
                return checkbox.SomeChecked;
            return checkbox.UnChecked;
        });
    }

    activate(args: any) {
        super.activate(args);
        this.updateHelpLink('AIHAR1');

        return this.fetchIndexes();
    }

    attached() {
        super.attached();
        ko.postbox.publish("SetRawJSONUrl", appUrl.forIndexesRawData(this.activeDatabase())); //TODO: do we need it?
    }

    private fetchIndexes(): JQueryPromise<void> {
        const statsTask = new getIndexesStatsCommand(this.activeDatabase())
            .execute();

        const statusTask = new getIndexesStatusCommand(this.activeDatabase())
            .execute();

        const replacementTask = new getPendingIndexReplacementsCommand(this.activeDatabase()).execute(); //TODO: this is not working yet!

        return $.when<any>(statsTask, replacementTask, statusTask)
            .done(([stats]: [Array<Raven.Client.Data.Indexes.IndexStats>], [replacements]: [indexReplaceDocument[]], [statuses]: [Raven.Client.Data.Indexes.IndexingStatus]) => this.processData(stats, replacements, statuses));
    }

    private processData(stats: Array<Raven.Client.Data.Indexes.IndexStats>, replacements: indexReplaceDocument[], statuses: Raven.Client.Data.Indexes.IndexingStatus) {
        //TODO: handle replacements

        this.indexingEnabled(statuses.Status !== "Paused");

        stats
            .map(i => new index(i))
            .forEach(i => {
                const paused = !!statuses.Indexes.find(x => x.Name === i.name && x.Status === "Paused");
                i.pausedUntilRestart(paused);
                this.putIndexIntoGroups(i);
            });
    }

    private putIndexIntoGroups(i: index): void {
        this.putIndexIntoGroupNamed(i, i.getGroupName());
    }

    private putIndexIntoGroupNamed(i: index, groupName: string): void {
        const group = this.indexGroups().find(g => g.entityName === groupName);
        if (group) {
            const oldIndex = group.indexes().find((cur: index) => cur.name === i.name);
            if (oldIndex) {
                group.indexes.replace(oldIndex, i);
            } else {
                group.indexes.push(i);
            }
        } else {
            this.indexGroups.push({
                entityName: groupName,
                indexes: ko.observableArray([i]),
                groupHidden: ko.observable<boolean>(false)
            });
        }
    }

    private filterIndexes() {
        const filterLower = this.searchText().toLowerCase();
        this.selectedIndexesName([]);
        this.indexGroups().forEach(indexGroup => {
            var hasAnyInGroup = false;
            indexGroup.indexes().forEach(index => {
                var match = index.name.toLowerCase().indexOf(filterLower) >= 0;
                index.filteredOut(!match);
                if (match) {
                    hasAnyInGroup = true;
                }
            });

            indexGroup.groupHidden(!hasAnyInGroup);
        });
    }

    resetIndex(indexToReset: index) {
        this.confirmationMessage("Reset index?", "You're resetting " + indexToReset.name)
            .done(result => {
                if (result.can) {

                    eventsCollector.default.reportEvent("indexes", "reset");

                    // reset index is implemented as delete and insert, so we receive notification about deleted index via changes API
                    // let's issue marker to ignore index delete information for next few seconds because it might be caused by reset.
                    // Unfortunettely we can't use resetIndexVm.resetTask.done, because we receive event via changes api before resetTask promise 
                    // is resolved. 
                    this.resetsInProgress.add(indexToReset.name);

                    new resetIndexCommand(indexToReset.name, this.activeDatabase())
                        .execute();

                    setTimeout(() => {
                        this.resetsInProgress.delete(indexToReset.name);
                    }, 30000);
                }
            });
    }

    deleteIndex(i: index) {
        eventsCollector.default.reportEvent("indexes", "delete");
        this.promptDeleteIndexes([i]);
        this.resetsInProgress.delete(i.name);
    }

    private processIndexEvent(e: Raven.Abstractions.Data.IndexChangeNotification) {
        const indexRemovedEvent = "IndexRemoved" as Raven.Abstractions.Data.IndexChangeTypes;
        if (e.Type === indexRemovedEvent) {
            if (!this.resetsInProgress.has(e.Name)) {
                this.removeIndexesFromAllGroups(this.findIndexesByName(e.Name));
            }
        } else {
            setTimeout(() => {
                this.fetchIndexes();
            }, 5000);
        }
    }

    private removeIndexesFromAllGroups(indexes: index[]) {
        this.indexGroups().forEach(g => {
            g.indexes.removeAll(indexes);
        });

        // Remove any empty groups.
        this.indexGroups.remove((item: indexGroup) => item.indexes().length === 0);
    }

    private findIndexesByName(indexName: string): index[] {
        var result = new Array<index>();
        this.indexGroups().forEach(g => {
            g.indexes().forEach(i => {
                if (i.name === indexName) {
                    result.push(i);
                }
            });
        });

        return result;
    }

    private promptDeleteIndexes(indexes: index[]) {
        if (indexes.length > 0) {
            const deleteIndexesVm = new deleteIndexesConfirm(indexes.map(i => i.name), this.activeDatabase());
            app.showBootstrapDialog(deleteIndexesVm); 
            deleteIndexesVm.deleteTask
                .done((deleted: boolean) => {
                    if (deleted) {
                        this.removeIndexesFromAllGroups(indexes);
                    }
                });
        }
    }

    unlockIndex(i: index) {
        eventsCollector.default.reportEvent("indexes", "set-lock-mode", "Unlock");
        this.updateIndexLockMode(i, "Unlock", "Unlocked");
    }

    lockIndex(i: index) {
        eventsCollector.default.reportEvent("indexes", "set-lock-mode", "LockedIgnore");
        this.updateIndexLockMode(i, "LockedIgnore", "Locked");
    }

    lockErrorIndex(i: index) {
        eventsCollector.default.reportEvent("indexes", "set-lock-mode", "LockedError");
        this.updateIndexLockMode(i, "LockedError","Locked (Error)");
    }

    lockSideBySide(i: index) {
        eventsCollector.default.reportEvent("indexes", "set-lock-mode", "SideBySide");
        this.updateIndexLockMode(i, "SideBySide","Locked (Side by Side)");
    }

    private updateIndexLockMode(i: index, newLockMode: Raven.Abstractions.Indexing.IndexLockMode, lockModeStrForTitle: string) {
        if (i.lockMode() !== newLockMode) {
            this.spinners.localLockChanges.push(i.name);

            new saveIndexLockModeCommand([i], newLockMode, this.activeDatabase(), lockModeStrForTitle)
                .execute()
                .done(() => i.lockMode(newLockMode))
                .always(() => this.spinners.localLockChanges.remove(i.name));
        }
    }

    enableIndex(idx: index) {
        eventsCollector.default.reportEvent("indexes", "set-state", "enabled");

        if (idx.canBeEnabled()) {

            this.spinners.localState.push(idx.name);

            new enableIndexCommand(idx.name, this.activeDatabase())
                .execute()
                .done(() => idx.state("Normal"))
                .always(() => this.spinners.localState.remove(idx.name));
                
        }
    }

    disableIndex(idx: index) {
        eventsCollector.default.reportEvent("indexes", "set-state", "disabled");

        if (idx.canBeDisabled()) {
            this.spinners.localState.push(idx.name);

            new disableIndexCommand(idx.name, this.activeDatabase())
                .execute()
                .done(() => {
                    idx.state("Disabled");
                    idx.pausedUntilRestart(false);
                })
                .always(() => this.spinners.localState.remove(idx.name));
        }
    }

    normalPriority(idx: index) {
        eventsCollector.default.reportEvent("index", "priority", "normal");
        this.setIndexPriority(idx, "Normal");
    }

    lowPriority(idx: index) {
        eventsCollector.default.reportEvent("index", "priority", "low");
        this.setIndexPriority(idx, "Low");
    }

    highPriority(idx: index) {
        eventsCollector.default.reportEvent("index", "priority", "high");
        this.setIndexPriority(idx, "High");
    }

    private setIndexPriority(idx: index, newPriority: Raven.Client.Data.Indexes.IndexPriority) {
        const originalPriority = idx.priority();
        if (originalPriority !== newPriority) {
            this.spinners.localPriority.push(idx.name);

            const optionalResumeTask = idx.pausedUntilRestart()
                ? this.resumeIndexingInternal(idx)
                : $.Deferred<void>().resolve();

            optionalResumeTask.done(() => {
                new saveIndexPriorityCommand(idx.name, newPriority, this.activeDatabase())
                    .execute()
                    .done(() => idx.priority(newPriority))
                    .always(() => this.spinners.localPriority.remove(idx.name));
            });
        }
    }

    createNotifications(): Array<changeSubscription> {
        return [
            this.changesContext.currentResourceChangesApi().watchAllIndexes((e) => this.processIndexEvent(e)),
            this.changesContext.currentResourceChangesApi().watchDocsStartingWith(indexReplaceDocument.replaceDocumentPrefix, () => this.processReplaceEvent())
        ];
    }

    private processReplaceEvent() {
        setTimeout(() => this.fetchIndexes(), 10);
    }

    cancelSideBySideIndex(i: index) {
        eventsCollector.default.reportEvent("index", "cancel-side-by-side");
        const cancelSideBySideIndexViewModel = new cancelSideBySizeConfirm([i.name], this.activeDatabase());
        app.showBootstrapDialog(cancelSideBySideIndexViewModel);
        cancelSideBySideIndexViewModel.cancelTask
            .done((closedWithoutDeletion: boolean) => {
                if (!closedWithoutDeletion) {
                    this.removeIndexesFromAllGroups([i]);
                }
            })
            .fail(() => {
                this.removeIndexesFromAllGroups([i]);
                this.fetchIndexes();
            });
    }

    forceSideBySide(idx: index) {
        eventsCollector.default.reportEvent("index", "force-side-by-side");
        new forceIndexReplace(idx.name, this.activeDatabase()).execute();
    }

    unlockSelectedIndexes() {
        this.setLockModeSelectedIndexes("Unlock", "Unlock");
    }

    lockSelectedIndexes() {
        this.setLockModeSelectedIndexes("LockedIgnore", "Lock");
    }

    lockSideBySideSelectedIndexes() {
        this.setLockModeSelectedIndexes("SideBySide", "Lock (Side By Side)");
    }

    lockErrorSelectedIndexes() {
        this.setLockModeSelectedIndexes("LockedError", "Lock (Error)");
    }

    private setLockModeSelectedIndexes(lockModeString: Raven.Abstractions.Indexing.IndexLockMode, lockModeStrForTitle: string) {
        if (this.lockModeCommon() === lockModeString)
            return;

        this.confirmationMessage("Are you sure?", `Do you want to ${lockModeStrForTitle} selected indexes?`)
            .done(can => {
                if (can) {
                    eventsCollector.default.reportEvent("index", "set-lock-mode-selected", lockModeString);

                    this.spinners.globalLockChanges(true);

                    const indexes = this.getSelectedIndexes();

                    new saveIndexLockModeCommand(indexes, lockModeString, this.activeDatabase(),lockModeStrForTitle)
                        .execute()
                        .done(() => indexes.forEach(i => i.lockMode(lockModeString)))
                        .always(() => this.spinners.globalLockChanges(false));
                }
            });
    }

    deleteSelectedIndexes() {
        eventsCollector.default.reportEvent("indexes", "delete-selected");
        this.promptDeleteIndexes(this.getSelectedIndexes());
    }

    startIndexing(): void {
        this.confirmationMessage("Are you sure?", "Do you want to resume indexing?")
            .done(result => {
                if (result.can) {
                    eventsCollector.default.reportEvent("indexes", "resume-all");
                    this.spinners.globalStartStop(true);
                    new toggleIndexingCommand(true, this.activeDatabase())
                        .execute()
                        .done(() => this.indexingEnabled(true))
                        .always(() => {
                            this.spinners.globalStartStop(false);
                            this.fetchIndexes();
                        });
                }
            });
    }

    stopIndexing() {
        this.confirmationMessage("Are you sure?", "Do you want to pause indexing until server restart?")
            .done(result => {
                if (result.can) {
                    eventsCollector.default.reportEvent("indexes", "pause-all");
                    this.spinners.globalStartStop(true);
                    new toggleIndexingCommand(false, this.activeDatabase())
                        .execute()
                        .done(() => this.indexingEnabled(false))
                        .always(() => {
                            this.spinners.globalStartStop(false);
                            this.fetchIndexes();
                        });
                }
            });
    }

    resumeIndexing(idx: index): JQueryPromise<void> {
        eventsCollector.default.reportEvent("indexes", "resume");
        this.spinners.localState.push(idx.name);

        return this.resumeIndexingInternal(idx)
            .always(() => this.spinners.localState.remove(idx.name));
    }

    private resumeIndexingInternal(idx: index): JQueryPromise<void> {
        return new toggleIndexingCommand(true, this.activeDatabase(), { name: [idx.name] })
            .execute()
            .done(() => idx.pausedUntilRestart(false));
    }

    pauseUntilRestart(idx: index) {
        eventsCollector.default.reportEvent("indexes", "pause");
        this.spinners.localState.push(idx.name);

        new toggleIndexingCommand(false, this.activeDatabase(), { name: [idx.name] })
            .execute()
            .done(() => idx.pausedUntilRestart(true))
            .always(() => this.spinners.localState.remove(idx.name));
    }

    toggleSelectAll() {
        eventsCollector.default.reportEvent("indexes", "toggle-select-all");
        const selectedIndexesCount = this.selectedIndexesName().length;

        if (selectedIndexesCount > 0) {
            this.selectedIndexesName([]);
        } else {
            const namesToSelect = [] as Array<string>;

            this.indexGroups().forEach(indexGroup => {
                if (!indexGroup.groupHidden()) {
                    indexGroup.indexes().forEach(index => {
                        if (!index.filteredOut() && !_.includes(namesToSelect, index.name)) {
                            namesToSelect.push(index.name);
                        }
                    });
                }
            });
            this.selectedIndexesName(namesToSelect);
        }
    }
}

export = indexes;
