﻿/// <reference path="../../../../typings/tsd.d.ts"/>
import generalUtils = require("common/generalUtils");
import replicationAccessModel = require("models/database/tasks/replicationAccessModel");

class pullReplicationDefinition {

    taskId: number;
    taskName = ko.observable<string>();
    taskType = ko.observable<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskType>();
    responsibleNode = ko.observable<Raven.Client.ServerWide.Operations.NodeId>();
    
    manualChooseMentor = ko.observable<boolean>();
    mentorNode = ko.observable<string>();
    nodeTag: string = null;
    
    delayReplicationTime = ko.observable<number>();
    showDelayReplication = ko.observable<boolean>();
    humaneDelayDescription: KnockoutComputed<string>;
    
    allowReplicationFromHubToSink = ko.observable<boolean>(true);
    allowReplicationFromSinkToHub = ko.observable<boolean>();
    replicationMode: KnockoutComputed<Raven.Client.Documents.Operations.Replication.PullReplicationMode>;
        
    filteringIsRequired = ko.observable<boolean>();
    
    replicationAccessItems = ko.observableArray<replicationAccessModel>([]);
    
    validationGroupForSave: KnockoutValidationGroup;
    validationGroupForExport: KnockoutValidationGroup;

    constructor(dto: Raven.Client.Documents.Operations.Replication.PullReplicationDefinition) {
        this.update(dto); 
        this.initializeObservables();
        this.initValidation();
    }

    initializeObservables() {
        this.humaneDelayDescription = ko.pureComputed(() => {
            const delayTimeHumane = generalUtils.formatTimeSpan(this.delayReplicationTime() * 1000, true);
            return this.showDelayReplication() && this.delayReplicationTime.isValid() && this.delayReplicationTime() !== 0 ?
                `Documents will be replicated after a delay time of <strong>${delayTimeHumane}</strong>` : "";
        });
        
        this.replicationMode = ko.pureComputed(() => {
            
            if (this.allowReplicationFromHubToSink() && this.allowReplicationFromSinkToHub()) {
                return "HubToSink,SinkToHub" as Raven.Client.Documents.Operations.Replication.PullReplicationMode;
            }

            return (this.allowReplicationFromHubToSink()) ? "HubToSink" :
                    this.allowReplicationFromSinkToHub() ? "SinkToHub" : "None";
        })
    }
    
    update(dto: Raven.Client.Documents.Operations.Replication.PullReplicationDefinition) {
        this.taskName(dto.Name);
        this.taskId = dto.TaskId;

        this.manualChooseMentor(!!dto.MentorNode);
        this.mentorNode(dto.MentorNode);

        const delayTime = generalUtils.timeSpanToSeconds(dto.DelayReplicationFor);
        this.showDelayReplication(dto.DelayReplicationFor != null && delayTime !== 0);
        this.delayReplicationTime(dto.DelayReplicationFor ? delayTime : null);
        
        this.allowReplicationFromHubToSink(dto.Mode.includes("HubToSink"));
        this.allowReplicationFromSinkToHub(dto.Mode.includes("SinkToHub"));
        
        this.filteringIsRequired(dto.FilteringIsRequired);
    }

    toDto(taskId: number): Raven.Client.Documents.Operations.Replication.PullReplicationDefinition {
        return {
            Name: this.taskName(),
            MentorNode: this.manualChooseMentor() ? this.mentorNode() : undefined,
            TaskId: taskId,
            DelayReplicationFor: this.showDelayReplication() ? generalUtils.formatAsTimeSpan(this.delayReplicationTime() * 1000) : null,
            Mode: this.replicationMode(),
            FilteringIsRequired: this.filteringIsRequired()
        } as Raven.Client.Documents.Operations.Replication.PullReplicationDefinition;
    }
    
    initValidation() {
        this.taskName.extend({
            required: true
        });
        
        this.mentorNode.extend({
            required: {
                onlyIf: () => this.manualChooseMentor()
            }
        });
        
        this.delayReplicationTime.extend({
            required: {
                onlyIf: () => this.showDelayReplication()
            },
            min: 0
        });
        
        this.replicationMode.extend({
            validation: [
                {
                    validator: () => this.replicationMode() !== "None",
                    message: "Please select at least one replication mode"
                }
            ]
        })
        
        this.validationGroupForSave = ko.validatedObservable({
            taskName: this.taskName,
            mentorNode: this.mentorNode,
            delayReplicationTime: this.delayReplicationTime,
            replicationMode: this.replicationMode
        });

        this.validationGroupForExport = ko.validatedObservable({
            taskName: this.taskName,
            mentorNode: this.mentorNode,
            delayReplicationTime: this.delayReplicationTime
        });
    }

    static empty(requiresCertificates: boolean): pullReplicationDefinition {
        return new pullReplicationDefinition({            
            Name: "",
            DelayReplicationFor: null,
            Disabled: false,
            MentorNode: null,
            TaskId: null,
            FilteringIsRequired: false,
            Mode: "HubToSink"
        } as Raven.Client.Documents.Operations.Replication.PullReplicationDefinition);
    }
}

export = pullReplicationDefinition;
