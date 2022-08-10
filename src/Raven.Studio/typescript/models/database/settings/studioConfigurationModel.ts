/// <reference path="../../../../typings/tsd.d.ts"/>

import jsonUtil = require("common/jsonUtil");

interface globalStudioConfigurationOptions extends Raven.Client.ServerWide.Operations.Configuration.ServerWideStudioConfiguration {
    SendUsageStats: boolean;
    CollapseDocsWhenOpening: boolean;
    HugeDocumentSize: number;
}

class studioConfigurationModel {

    static readonly environments: Array<Raven.Client.Documents.Operations.Configuration.StudioConfiguration.StudioEnvironment> = ["None", "Development", "Testing", "Production"]; 
    
    environment = ko.observable<Raven.Client.Documents.Operations.Configuration.StudioConfiguration.StudioEnvironment>();
    sendUsageStats = ko.observable<boolean>(false);
    disabled = ko.observable<boolean>();
    replicationFactor = ko.observable<number>(null);
    collapseDocsWhenOpening = ko.observable<boolean>();
    hugeDocumentSize = ko.observable<number>(null);

    dirtyFlag: () => DirtyFlag;
    validationGroup: KnockoutValidationGroup;
    
    constructor(dto: globalStudioConfigurationOptions) {
        this.initValidation();
        
        this.environment(dto.Environment);
        this.disabled(dto.Disabled);
        this.sendUsageStats(dto.SendUsageStats);
        this.replicationFactor(dto.ReplicationFactor);
        this.collapseDocsWhenOpening(dto.CollapseDocsWhenOpening);
        this.hugeDocumentSize(dto.HugeDocumentSize);

        this.dirtyFlag = new ko.DirtyFlag([
            this.environment,
            this.sendUsageStats,
            this.replicationFactor,
            this.collapseDocsWhenOpening,
            this.hugeDocumentSize,
        ], false, jsonUtil.newLineNormalizingHashFunction);
    }
    
    private initValidation() {
        this.validationGroup = ko.validatedObservable({
            environment: this.environment
        });
    }
    
    toRemoteDto(): Raven.Client.ServerWide.Operations.Configuration.ServerWideStudioConfiguration {
        return {
            Environment: this.environment(),
            Disabled: this.disabled(),
            ReplicationFactor: this.replicationFactor()
        }
    }
}

export = studioConfigurationModel;
