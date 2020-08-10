/// <reference path="../../../../typings/tsd.d.ts"/>
import pullReplicationCertificate = require("models/database/tasks/pullReplicationCertificate");
import prefixPathModel = require("models/database/tasks/prefixPathModel");
import jsonUtil = require("common/jsonUtil");

class replicationAccessModel {

    isNewAccessItem = ko.observable<boolean>();
    
    replicationAccessName = ko.observable<string>();
    certificate = ko.observable<pullReplicationCertificate>();
    
    hubToSinkPrefixes = ko.observableArray<prefixPathModel>([]);
    sinkToHubPrefixes = ko.observableArray<prefixPathModel>([]);
        
    samePrefixesForBothDirections = ko.observable<boolean>(false);

    inputPrefixHubToSink = ko.observable<prefixPathModel>(new prefixPathModel(null));
    inputPrefixSinkToHub = ko.observable<prefixPathModel>(new prefixPathModel(null));

    filteringReplicationText: KnockoutComputed<string>;
    
    certificateWasImported = ko.observable<boolean>(false);
    accessConfigurationWasExported = ko.observable<boolean>(false);
    certificateWasDownloaded = ko.observable<boolean>(false);    
    certificateInfoWasSavedForSinkTask: KnockoutComputed<boolean>

    validationGroupForSaveWithFiltering: KnockoutValidationGroup;
    validationGroupForSaveNoFiltering: KnockoutValidationGroup;
    validationGroupForExportWithFiltering: KnockoutValidationGroup;
    validationGroupForExportNoFiltering: KnockoutValidationGroup;

    dirtyFlag = new ko.DirtyFlag([]);

    constructor(accessName: string, certificate: pullReplicationCertificate, hubToSink: prefixPathModel[], sinkToHub: prefixPathModel[], isNewItem: boolean = true) {
        this.isNewAccessItem(isNewItem);
        
        this.replicationAccessName(accessName);
        this.hubToSinkPrefixes(hubToSink);
        this.sinkToHubPrefixes(sinkToHub);
        this.certificate(certificate);
      
        this.samePrefixesForBothDirections(_.isEqual(hubToSink.map(x => x.path()), sinkToHub.map(x => x.path())));        

        this.initObservables();
        this.initValidation();
    }
    
    initObservables() {
        
        this.certificateInfoWasSavedForSinkTask = ko.pureComputed(() => {
            return this.accessConfigurationWasExported() || this.certificateWasDownloaded();
        })

        this.dirtyFlag = new ko.DirtyFlag([
            this.replicationAccessName,
            this.certificate,
            this.hubToSinkPrefixes,
            this.sinkToHubPrefixes,
            this.samePrefixesForBothDirections
        ], false, jsonUtil.newLineNormalizingHashFunction);
        
        this.filteringReplicationText = ko.pureComputed(() => {                             
            
            const h2s = this.hubToSinkPrefixes().length;
            const s2h = this.sinkToHubPrefixes().length

            let text = "";
            
            if (h2s) {
                text = `Hub to Sink (${h2s} path${h2s > 1 ? "s" : ""})`
            }

            if (h2s && s2h) {
                text += ", ";
            }            
            if (s2h) {
                text += `Sink to Hub (${s2h} path${s2h > 1 ? "s" : ""})`
            }
            
            return text;
        })
    }
    
    initValidation() {
        this.replicationAccessName.extend({
           required: true
        });
        
        this.certificate.extend({
            required: true
        })
        
        this.certificateInfoWasSavedForSinkTask.extend({
            validation: [
                {
                    validator: () => !this.isNewAccessItem() || this.certificateWasImported() || this.certificateInfoWasSavedForSinkTask(),
                    message: "Export the Access Configuration or download the certificate before saving."
                }
            ]
        });
        
        this.hubToSinkPrefixes.extend({
            validation: [
                {
                    validator: () => this.hubToSinkPrefixes().length, 
                    message: "Please add at least one filtering prefix path."
                }
            ]
        })

        this.sinkToHubPrefixes.extend({
            validation: [
                {
                    validator: () => this.samePrefixesForBothDirections() || this.sinkToHubPrefixes().length,  
                    message: "Please add at least one filtering prefix path, or use the Hub to Sink prefixes."
                }
            ]
        })

        this.validationGroupForSaveWithFiltering = ko.validatedObservable({
            replicationAccessName: this.replicationAccessName,
            certificate: this.certificate,
            certificateInfoWasSavedForSinkTask: this.certificateInfoWasSavedForSinkTask,
            hubToSinkPrefixes: this.hubToSinkPrefixes,
            sinkToHubPrefixes: this.sinkToHubPrefixes
        });

        this.validationGroupForSaveNoFiltering = ko.validatedObservable({
            replicationAccessName: this.replicationAccessName,
            certificate: this.certificate,
            certificateInfoWasSavedForSinkTask: this.certificateInfoWasSavedForSinkTask,
        });
        
        this.validationGroupForExportWithFiltering = ko.validatedObservable({
            replicationAccessName: this.replicationAccessName,
            certificate: this.certificate,
            hubToSinkPrefixes: this.hubToSinkPrefixes,
            sinkToHubPrefixes: this.sinkToHubPrefixes
        });
        
        this.validationGroupForExportNoFiltering = ko.validatedObservable({
            replicationAccessName: this.replicationAccessName,
            certificate: this.certificate
        });
    }

    addHubToSinkInputPrefixWithBlink() {
        if (!this.hubToSinkPrefixes().filter(prefix => prefix.path() === this.inputPrefixHubToSink().path()).length) {
            const itemToAdd = new prefixPathModel(this.inputPrefixHubToSink().path());
            this.hubToSinkPrefixes().unshift(itemToAdd);
            this.hubToSinkPrefixes(this.hubToSinkPrefixes());

            this.inputPrefixHubToSink().path(null);
            $("#hubToSink .collection-list li").first().addClass("blink-style");
        }
    }

    addSinkToHubInputPrefixWithBlink() {
        if (!this.sinkToHubPrefixes().filter(prefix => prefix.path() === this.inputPrefixSinkToHub().path()).length) {
            const itemToAdd = new prefixPathModel(this.inputPrefixSinkToHub().path());
            this.sinkToHubPrefixes().unshift(itemToAdd);
            this.sinkToHubPrefixes(this.sinkToHubPrefixes());

            this.inputPrefixSinkToHub().path(null);
            $("#sinkToHub .collection-list li").first().addClass("blink-style");
        }
    }
    
    removePrefixPathHubToSink(pathToRemove: string) {
        const itemsList = this.hubToSinkPrefixes().filter(prefix => prefix.path() !== pathToRemove);        
        this.hubToSinkPrefixes(itemsList);
    }
    
    removePrefixPathSinkToHub(pathToRemove: string) {
        const itemsList = this.sinkToHubPrefixes().filter(prefix => prefix.path() !== pathToRemove);
        this.sinkToHubPrefixes(itemsList);
    }
    
    static empty(): replicationAccessModel {
        return new replicationAccessModel("", null, [], []);
    }
    
    static clone(itemToClone: replicationAccessModel): replicationAccessModel {
        return new replicationAccessModel(
            itemToClone.replicationAccessName(),
            itemToClone.certificate(),
            itemToClone.hubToSinkPrefixes(),
            itemToClone.sinkToHubPrefixes(),
            itemToClone.isNewAccessItem()
        );
    }
    
    toDto(): Raven.Client.Documents.Operations.Replication.ReplicationHubAccess {
        return {
            Name: this.replicationAccessName(),
            CertificateBase64: this.certificate().certificate(),  
            //CertificateBase64: this.certificate().publicKey(), // ???
            AllowedHubToSinkPaths: this.hubToSinkPrefixes().map(prefix => prefix.path()),
            AllowedSinkToHubPaths: this.sinkToHubPrefixes().map(prefix => prefix.path())
        }
    }
}

export = replicationAccessModel;
