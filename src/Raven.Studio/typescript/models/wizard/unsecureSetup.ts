/// <reference path="../../../typings/tsd.d.ts"/>
import ipEntry = require("models/wizard/ipEntry");
import nodeInfo = require("models/wizard/nodeInfo");

class unsecureSetup {

    //port = ko.observable<string>(); // TODO ask Omer...
    
    //tcpPort = ko.observable<string>(); // TODO ask Omer... should remove from here ?
    
    //ip = ko.observable<ipEntry>(); // TODO ask Omer...
    
    // unsafeNetworkConfirm = ko.observable<boolean>(false);
    // localNodeTag = ko.observable<string>("A");
    
    // bootstrapCluster = ko.observable<boolean>(false);
    
    //startNodeAsPassive = ko.observable<boolean>(false);
    
    // validationGroup: KnockoutValidationGroup;
    //shouldDisplayUnsafeModeWarning: KnockoutComputed<boolean>;
    
    constructor() {
        //this.initObservables();
        this.initValidation();
    }
    
    // private initObservables() {
    //     // show waring if at least one is not local.. // gen msg: ip on nodes... is not local
    //     this.shouldDisplayUnsafeModeWarning = ko.pureComputed(() => {
    //         if (!this.ip()) {
    //             return false;
    //         }
    //         return !this.ip().isLocalNetwork();
    //     });
    // }
    
    private initValidation() {
        const currentHttpPort = window.location.port || "80";
        
        // moved to nodeInfo.ts
        // this.port.extend({
        //     number: true,
        //     notEqual: {
        //         params: currentHttpPort,
        //         message: "Port is in use by the Setup Wizard"
        //     }
        // });
        //
        // this.tcpPort.extend({
        //     number: true,
        //     notEqual: {
        //         params: currentHttpPort,
        //         message: "Port is in use by the Setup Wizard"
        //     },
        //     validation: [
        //         {
        //             validator: (val: string) => !val || val !== this.port(),
        //             message: "Please use different ports for HTTP and TCP bindings"
        //         }
        //     ]
        // });

        // nodeInfo.setupNodeTagValidation(this.localNodeTag, {
        //     // onlyIf: () => this.bootstrapCluster()
        //     onlyIf: () => !this.startNodeAsPassive()
        // });
        //
        // this.unsafeNetworkConfirm.extend({
        //     validation: [
        //         {
        //             validator: () => {
        //                 return !this.shouldDisplayUnsafeModeWarning() || this.unsafeNetworkConfirm();
        //             },
        //             message: "Confirmation is required"
        //         }
        //     ]
        // });

        // this.validationGroup = ko.validatedObservable({
        //     port: this.port, 
        //     unsafeNetworkConfirm: this.unsafeNetworkConfirm,
        //     tcpPort: this.tcpPort
        // })
    }
}

export = unsecureSetup;
