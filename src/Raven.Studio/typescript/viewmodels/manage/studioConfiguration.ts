import viewModelBase = require("viewmodels/viewModelBase");
import studioConfigurationModel = require("models/database/settings/studioConfigurationModel");
import studioSettings = require("common/settings/studioSettings");
import globalSettings = require("common/settings/globalSettings");
import jsonUtil = require("common/jsonUtil");
import document = require("models/database/documents/document");

class studioConfiguration extends viewModelBase {

    spinners = {
        save: ko.observable<boolean>(false)
    };

    model: studioConfigurationModel;

    static environments = studioConfigurationModel.environments;
    
    static hugeSize = document.hugeSizeInBytesDefault;

    activate(args: any) {
        super.activate(args);
     
        return studioSettings.default.globalSettings(true)
            .done((settings: globalSettings) => {
                this.model = new studioConfigurationModel({
                    Environment: settings.environment.getValue(),
                    Disabled: settings.disabled.getValue(),
                    ReplicationFactor: settings.replicationFactor.getValue(),
                    SendUsageStats: settings.sendUsageStats.getValue(),
                    CollapseDocsWhenOpening: settings.collapseDocsWhenOpening.getValue(),
                    HugeDocumentSize: settings.hugeDocumentSize.getValue()
                });

                this.dirtyFlag = new ko.DirtyFlag([
                    this.model.dirtyFlag().isDirty
                ], false, jsonUtil.newLineNormalizingHashFunction);
            });
    }

    compositionComplete() {
        super.compositionComplete();
        $('.studio-configuration [data-toggle="tooltip"]').tooltip();
    }
    
    saveConfiguration() {
        this.spinners.save(true);
        
        studioSettings.default.globalSettings()
            .done(settings => {
                const model = this.model;
                
                settings.environment.setValueLazy(model.environment());
                settings.sendUsageStats.setValueLazy(model.sendUsageStats());
                settings.replicationFactor.setValueLazy(model.replicationFactor());
                settings.collapseDocsWhenOpening.setValue(model.collapseDocsWhenOpening());
                settings.hugeDocumentSize.setValue(model.hugeDocumentSize());
                
                settings.save()
                    .done(() => this.model.dirtyFlag().reset())
                    .always(() => this.spinners.save(false));
            })
    }
}

export = studioConfiguration;
