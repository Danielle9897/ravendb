﻿/// <reference path="../../../../typings/tsd.d.ts"/>
import appUrl = require("common/appUrl");
import router = require("plugins/router");
import ongoingTaskListModel = require("models/database/tasks/ongoingTaskListModel"); 
import generalUtils = require("common/generalUtils");
import shell = require("viewmodels/shell");
import getServerWideBackupCommand = require("commands/resources/getServerWideBackupCommand");

class ongoingTaskServerWideBackupListModel extends ongoingTaskListModel {    
    editUrl: KnockoutComputed<string>;

    backupType = ko.observable<Raven.Client.Documents.Operations.Backups.BackupType>();
    fullBackupTypeName: KnockoutComputed<string>;
    
    retentionPolicyPeriod = ko.observable<string>();
    retentionPolicyDisabled = ko.observable<boolean>();
    retentionPolicyHumanized: KnockoutComputed<string>;
    
    backupDestinations = ko.observableArray<string>([]);
    backupDestinationsHumanized: KnockoutComputed<string>;
    textClass: KnockoutComputed<string>;
    
    isBackupEncryped = ko.observable<boolean>();

    constructor(dto: Raven.Client.ServerWide.Operations.Configuration.ServerWideBackupConfiguration) {
        super();
        
        this.update(dto);
        this.initializeObservables();
    }

    initializeObservables() {
        super.initializeObservables();

        this.editUrl = ko.pureComputed(() => appUrl.forEditServerWideBackup(this.taskName()));

        this.retentionPolicyHumanized = ko.pureComputed(() => {
            return this.retentionPolicyDisabled() ? "No backups will be removed" : generalUtils.formatTimeSpan(this.retentionPolicyPeriod(), true);
        });
        
        this.backupDestinationsHumanized =  ko.pureComputed(() => {
            return this.backupDestinations().length ? this.backupDestinations().join(', ') : "No destinations defined";
        });

        this.fullBackupTypeName = ko.pureComputed(() => this.getBackupType(this.backupType(), true));
        
        this.textClass = ko.pureComputed(() => this.backupDestinations().length ? "text-details" : "text-warning")
    }

    // update method has 2 params only so that it compiles - due to class inheritance....
    update(dto: Raven.Client.ServerWide.Operations.Configuration.ServerWideBackupConfiguration | Raven.Client.Documents.Operations.OngoingTasks.OngoingTask) {
        super.update({ TaskName: (dto as Raven.Client.ServerWide.Operations.Configuration.ServerWideBackupConfiguration).Name,
                       TaskId:   (dto as Raven.Client.ServerWide.Operations.Configuration.ServerWideBackupConfiguration).TaskId,
                       TaskState:(dto as Raven.Client.ServerWide.Operations.Configuration.ServerWideBackupConfiguration).Disabled ? 'Disabled' : 'Enabled',
                       TaskConnectionStatus: null, // not relevant for this view
                       ResponsibleNode: null,      // not relevant for this view 
                       MentorNode: null,           // not relevant for this view 
                       Error: ""} as Raven.Client.Documents.Operations.OngoingTasks.OngoingTask ); // todo : add error field in models ???

        const serverWideDto = dto as Raven.Client.ServerWide.Operations.Configuration.ServerWideBackupConfiguration;
        
        this.backupType(serverWideDto.BackupType);
        this.backupDestinations(serverWideDto.BackupDestinations);
        
        // Check backward compatibility
        this.retentionPolicyDisabled(serverWideDto.RetentionPolicy ? serverWideDto.RetentionPolicy.Disabled : true);
        this.retentionPolicyPeriod(serverWideDto.RetentionPolicy ? serverWideDto.RetentionPolicy.MinimumBackupAgeToKeep : "0.0:00:00");
        
        this.isBackupEncryped(!!serverWideDto.BackupEncryptionSettings);       
    }

    private getBackupType(backupType: Raven.Client.Documents.Operations.Backups.BackupType, isFull: boolean): string {
        if (!isFull) {
            return "Incremental";
        }

        if (backupType === "Snapshot") {
            return "Snapshot";
        }

        return "Full";
    }

    editTask() {
        router.navigate(this.editUrl());
    }

    toggleDetails() {
        this.showDetails.toggle();

        if (this.showDetails()) {
            this.refreshBackupInfo(true);
        } 
    }

    refreshBackupInfo(reportFailure: boolean) {
        if (shell.showConnectionLost()) {
            // looks like we don't have connection to server, skip index progress update 
            return $.Deferred<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskBackup>().fail();
        }

        return new getServerWideBackupCommand(this.taskName())
            .execute()
            .done((result: Raven.Server.Web.System.ServerWideBackupConfigurationResults) => this.update(result.Results[0]));
    }
}

export = ongoingTaskServerWideBackupListModel;
