import viewModelBase = require("viewmodels/viewModelBase");
import getIndexMergeSuggestionsCommand = require("commands/database/debug/getIndexMergeSuggestionsCommand");
import database = require("models/resources/database");
import appUrl = require("common/appUrl");
import mergedIndexesStorage = require("common/mergedIndexesStorage");
import indexMergeSuggestion = require("models/database/index/indexMergeSuggestion");
import getDatabaseStatsCommand = require("commands/resources/getDatabaseStatsCommand");
import changeSubscription = require('common/changeSubscription');
import moment = require("moment");
import dialog = require("plugins/dialog");
import changesContext = require("common/changesContext");
import optional = require("common/optional");
import deleteIndexesConfirm = require("viewmodels/database/indexes/deleteIndexesConfirm");
import eventsCollector = require("common/eventsCollector");
import genUtils = require("common/generalUtils");

class indexMergeSuggestions extends viewModelBase {
    
    appUrls: computedAppUrls;
    suggestions = ko.observableArray<indexMergeSuggestion>();
    unmergables = ko.observableArray<{ indexName: string; reason: string; }>();
    idleOrAbandonedIndexes = ko.observableArray<indexStatisticsDto>();
    notUsedForLastWeek = ko.observableArray<indexStatisticsDto>();
    
    constructor() {
        super();
        this.appUrls = appUrl.forCurrentDatabase();
    }

    canActivate(args: any) :any {
        var deferred = $.Deferred();
        this.reload()
            .done(() => deferred.resolve({ can: true }))
            .fail(() => deferred.resolve({ redirect: appUrl.forIndexes(this.activeDatabase()) }));

        return deferred;
    }

    private reload() {
        var fetchIndexMergeSuggestionsTask = this.fetchIndexMergeSuggestions();
        var fetchStatsTask = this.fetchStats();
        return $.when(fetchIndexMergeSuggestionsTask, fetchStatsTask);
    }

    createNotifications(): Array<changeSubscription> {
        return [ /* TODO changesContext.currentResourceChangesApi().watchAllIndexes(() => this.fetchIndexMergeSuggestions()) */ ];
    }

    private fetchStats(): JQueryPromise<Raven.Client.Data.DatabaseStatistics> {
        var db = this.activeDatabase();
        if (db) {
            return new getDatabaseStatsCommand(db)
                .execute()
                //TODO: .done((result: databaseStatisticsDto) => this.processStatsResults(result));
        }

        return null;
    }

    private processStatsResults(stats: databaseStatisticsDto) {
        this.idleOrAbandonedIndexes([]);
        this.notUsedForLastWeek([]);
        var now = moment();
        var miliSecondsInWeek = 1000 * 3600 * 24 * 7;
        stats.Indexes.forEach(indexDto => {
            // we are using contains not equals as priority may contains 
            if (indexDto.Priority.includes("Idle") || indexDto.Priority.includes("Abandoned")) {
                this.idleOrAbandonedIndexes.push(indexDto);
            }

            if (indexDto.LastQueryTimestamp) {
                var lastQueryDate = moment(indexDto.LastQueryTimestamp);
                if (lastQueryDate.isValid()) {
                    var agoInMs = now.diff(lastQueryDate);
                    if (agoInMs > miliSecondsInWeek) {
                        (<any>indexDto)["LastQueryTimestampText"] = optional.val(indexDto.LastQueryTimestamp).bind(v => genUtils.toHumanizedDate(v));
                        this.notUsedForLastWeek.push(indexDto);
                    }
                }
            }
        });
    }

    private fetchIndexMergeSuggestions() {
        var deferred = $.Deferred();

        var db = this.activeDatabase();
        /* TODO
        new getIndexMergeSuggestionsCommand(db)
            .execute()
            .done((results: indexMergeSuggestionsDto) => {
                var suggestions = results.Suggestions.map((suggestion: suggestionDto) => new indexMergeSuggestion(suggestion));
                this.suggestions(suggestions);

                var unmergables = Object.keys(results.Unmergables).map((value, index) => {
                    return { indexName: value, reason: results.Unmergables[value] }
                });
                this.unmergables(unmergables);
                deferred.resolve();
            })
            .fail(() => deferred.reject());
        */
        return deferred;
    }

    mergeSuggestionIndex(index: string): number {
        return parseInt(index) + 1;
    }

    mergedIndexUrl(id: string) {
        var db: database = this.activeDatabase();
        var mergedIndexName = mergedIndexesStorage.getMergedIndexName(db, id);

        return this.appUrls.editIndex(mergedIndexName);
    }

    saveMergedIndex(id: string, suggestion: indexMergeSuggestion) {
        eventsCollector.default.reportEvent("index-merge-suggestions", "save-merged");
        var db: database = this.activeDatabase();
        mergedIndexesStorage.saveMergedIndex(db, id, suggestion);

        return true;
    }

    deleteIndexes(index: number) {
        eventsCollector.default.reportEvent("index-merge-suggestions", "delete-indexes");
        var mergeSuggestion = this.suggestions()[index];
        var indexesToDelete = mergeSuggestion.canDelete;
        var db = this.activeDatabase();
        var deleteViewModel = new deleteIndexesConfirm(indexesToDelete, db);
        deleteViewModel.deleteTask.always(() => this.reload());
        dialog.show(deleteViewModel);
    }


    deleteIndex(name: string) {
        eventsCollector.default.reportEvent("index-merge-suggestions", "delete-index");
        var db = this.activeDatabase();
        var deleteViewModel = new deleteIndexesConfirm([name], db);
        deleteViewModel.deleteTask.always(() => this.reload());
        dialog.show(deleteViewModel);
    }

    deleteAllIdleOrAbandoned() {
        eventsCollector.default.reportEvent("index-merge-suggestions", "delete-all-idle-or-abandoned");
        var db = this.activeDatabase();
        var deleteViewModel = new deleteIndexesConfirm(this.idleOrAbandonedIndexes().map(index => index.Name), db, "Delete all idle or abandoned indexes?");
        deleteViewModel.deleteTask.always(() => this.reload());
        dialog.show(deleteViewModel); 
    }

    deleteAllNotUsedForWeek() {
        eventsCollector.default.reportEvent("index-merge-suggestions", "delete-all-not-used-for-week");
        var db = this.activeDatabase();
        var deleteViewModel = new deleteIndexesConfirm(this.notUsedForLastWeek().map(index => index.Name), db, "Delete all indexes not used within last week?");
        deleteViewModel.deleteTask.always(() => this.reload());
        dialog.show(deleteViewModel);
    }
}

export = indexMergeSuggestions; 
