/// <reference path="../../../../typings/tsd.d.ts"/>
import replicationCertificateModel = require("models/database/tasks/replicationCertificateModel");
import prefixPathModel = require("models/database/tasks/prefixPathModel");
import replicationAccessBaseModel = require("models/database/tasks/replicationAccessBaseModel");
import messagePublisher = require("common/messagePublisher");

class replicationAccessSinkModel extends replicationAccessBaseModel {

    selectedFileName = ko.observable<string>();
    selectedFileCertificate = ko.observable<string>();
    selectedFilePassphrase = ko.observable<string>();
    
    certificateExtracted = ko.observable<boolean>(false);

    certificateSourceText: KnockoutComputed<string>;
    isServerCertificate = ko.observable<boolean>(false);
    
    serverCertificate = ko.observable<replicationCertificateModel>();
    clusterNodesNumber: number;
    
    validationGroup: KnockoutValidationGroup;
    
    constructor(accessName: string, 
                certificate: replicationCertificateModel,
                serverCertificate: replicationCertificateModel,
                hubToSink: prefixPathModel[], 
                sinkToHub: prefixPathModel[],
                clusterNodesCount: number) {
        super(accessName, certificate, hubToSink, sinkToHub, false);
        
        this.serverCertificate(serverCertificate);
        this.clusterNodesNumber = clusterNodesCount;
        
        this.certificateSourceText = ko.pureComputed(() => {
            return this.isServerCertificate() ? "Use the server certificate" : "Provide your own certificate";
        });
        
        
        this.initValidation();
    }
    
    initValidation() {
        super.initValidation();

        this.certificateExtracted.extend({
            validation: [
                {
                    validator: () => !this.selectedFileName() || this.certificateExtracted(),
                    message: "Could not extract certificate from this file"
                },
                {
                    validator: () => this.certificate(),
                    message: "Certificate is required"
                }
            ]
        });
        
        this.validationGroup = ko.validatedObservable({
            certificate: this.certificate,
            certificateExtracted: this.certificateExtracted,
            replicationAccessName: this.replicationAccessName
        });
    }

    onCertificateSelected(certAsBase64: string, fileName: string) {
        this.selectedFileCertificate(certAsBase64);
        this.tryReadCertificate();
    }

    tryReadCertificate(): boolean {
        const certificate = this.selectedFileCertificate();
        const password = this.selectedFilePassphrase() || undefined;

        if (certificate) {
            try {
                this.certificate(replicationCertificateModel.fromPkcs12(certificate, password));
                this.selectedFileCertificate(null);
                this.selectedFilePassphrase("");
                this.certificateExtracted(true);
                return true;
            } catch ($e) {
                messagePublisher.reportError("Unable to extract certificate from file", $e);
                this.certificateExtracted(false);
                return false;
            }
        }
    }

    useServerCertificate(useServerCertificate: boolean) {
        if (!useServerCertificate) {
            this.isServerCertificate(false);
            this.certificate(null);
            return;
        }

        this.isServerCertificate(true);
        this.certificate(this.serverCertificate());
        
        // return new getCertificatesCommand()
        //     .execute()
        //     .done(certificatesInfo => {
        //
        //         const serverCertificate = certificatesInfo.Certificates.find(cert => cert.Name === "Server Certificate");
        //         const certModel = new replicationCertificateModel(serverCertificate.Certificate);
        //         this.certificate(certModel);
        //
        //         this.isServerCertificate(true);
        //     });
    }

    static clone(itemToClone: replicationAccessSinkModel): replicationAccessSinkModel {
        return new replicationAccessSinkModel(
            itemToClone.replicationAccessName(),
            itemToClone.certificate(),
            itemToClone.serverCertificate(),
            itemToClone.hubToSinkPrefixes(),
            itemToClone.sinkToHubPrefixes(),
            itemToClone.clusterNodesNumber
        );
    }
}

export = replicationAccessSinkModel;
