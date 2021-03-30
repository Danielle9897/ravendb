/// <reference path="../../../../typings/tsd.d.ts"/>
import activeDatabaseTracker = require("common/shell/activeDatabaseTracker");
import abstractOngoingTaskEtlListModel = require("models/database/tasks/abstractOngoingTaskEtlListModel");
import appUrl = require("common/appUrl");

class ongoingTaskOlapEtlListModel extends abstractOngoingTaskEtlListModel {
    
    connectionStringDefined = ko.observable<boolean>();
    // destinationDescription: KnockoutComputed<string>; // todo, get info from server about destination names (i.e. local, S3, etc.)
    
    constructor(dto: Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskOlapEtlListView) {
        super();

        this.update(dto);
        this.initializeObservables();

        this.connectionStringsUrl = appUrl.forConnectionStrings(activeDatabaseTracker.default.database(), "olap", this.connectionStringName());
        console.log("url === " + this.connectionStringsUrl);
    }

    initializeObservables() {
        super.initializeObservables();

        const urls = appUrl.forCurrentDatabase();
        this.editUrl = urls.editOlapEtl(this.taskId);
        
        // todo..
        // this.destinationDescription = ko.pureComputed(() => {
        //     if (this.connectionStringDefined()) {
        //         return "todo...";
        //     } 
        //     return null;
        // })
    }

    update(dto: Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskOlapEtlListView) {
        super.update(dto);

        this.connectionStringName(dto.ConnectionStringName);
        this.connectionStringDefined(dto.ConnectionStringDefined);
    }
}

export = ongoingTaskOlapEtlListModel;
