import setupStep = require("viewmodels/wizard/setupStep");
import router = require("plugins/router");
import popoverUtils = require("common/popoverUtils");
import ipEntry = require("models/wizard/ipEntry");
import databaseStudioConfigurationModel = require("models/database/settings/databaseStudioConfigurationModel");
import nodeInfo = require("models/wizard/nodeInfo");
import serverSetup = require("models/wizard/serverSetup");

class unsecured extends setupStep {

    static environments = databaseStudioConfigurationModel.environments;
    
    editedNode = ko.observable<nodeInfo>();

    remoteNodeIpOptions = ko.observableArray<string>(['0.0.0.0']);

    shouldDisplayUnsafeModeWarning: KnockoutComputed<boolean>;
    unsafeNetworkConfirm = ko.observable<boolean>(false);
    //unsafeNetworkText: KnockoutComputed<string>;

    validationGroup: KnockoutValidationGroup;
    
    constructor() {
        super();
        this.bindToCurrentInstance("removeNode", "editNode");

        this.initObservables();
        this.initValidation();
    }

    private initObservables() {
        this.shouldDisplayUnsafeModeWarning = ko.pureComputed(() =>
            this.model.nodes().some(node => node.ips().some(ip => ip.ip() && !ip.isLocalNetwork())));
        
        // this.unsafeNetworkText = ko.pureComputed(() => {
        //     return `I understand the risk behind running RavenDB server in an unsecured mode.<br />
        //     The following nodes IPs aren't configured for local network: ${genUtils.escapeHtml(unsafeNodes.join())} <br />
        //     Authentication is off, anyone who can access the server using these IPs will be granted <strong>administrative privileges.</strong>`;
        // })
    }
    
    private initValidation() {
        nodeInfo.setupNodeTagValidation(this.model.localNodeTag, {
            onlyIf: () => !this.model.startNodeAsPassive()
        });

        this.unsafeNetworkConfirm.extend({
            validation: [
                {
                    validator: () => {
                        return !this.shouldDisplayUnsafeModeWarning() || this.unsafeNetworkConfirm();
                    },
                    message: "Confirmation is required"
                }
            ]
        });

        this.validationGroup = ko.validatedObservable({
            unsafeNetworkConfirm: this.unsafeNetworkConfirm
        })
    }
    
    canActivate(): JQueryPromise<canActivateResultDto> {
        const mode = this.model.mode();

        if (mode && mode === "Unsecured") {
            return $.when({ can: true });
        }

        return $.when({ redirect: "#welcome" });
    }

    activate(args: any) {
        super.activate(args);
        
        const initialIp = ipEntry.runningOnDocker ? "" : "127.0.0.1";
        
        this.model.nodes().forEach(x => {
            let ip = x.ips()[0];
            if (!ip) {
                ip = ipEntry.forIp(initialIp, false);
            }
        });
    }
    
    // activate(args: any) {
    //     super.activate(args);
    //     const unsecuredSetup = this.model.unsecureSetup();
    //    
    //     if (!unsecuredSetup.ip()) {
    //         const initialIp = ipEntry.runningOnDocker ? "" : "127.0.0.1";
    //        
    //         unsecuredSetup.ip(ipEntry.forIp(initialIp, false));
    //         unsecuredSetup.ip().validationGroup.errors.showAllMessages(false);
    //     }
    // }
    
    compositionComplete() {
        super.compositionComplete();

        if (this.model.nodes().length) {

            let firstNode = this.model.nodes()[0];

            if (firstNode.ips().length === 0 ) {
                firstNode.ips.push(new ipEntry(true));
            }

            this.editedNode(firstNode);

            this.initTooltips();
        }
        
        popoverUtils.longWithHover($("label[for=serverUrl] .icon-info"), // TODO  where is this used ???
            {
                content: 'The URL which the server should listen to. It can be hostname, ip address or 0.0.0.0:{port}',
            });
        
        this.initTooltips();
    }

    private initTooltips() {
        popoverUtils.longWithHover($("#passive-node"), {
            content: "<small>When the server is restarted this node will be in a Passive state, not part of a Cluster.</small>",
            placement: "bottom",
            html: true
        })

        popoverUtils.longWithHover($("#toggle-passive"), {
            content: "<small>Toggle ON to start this node in a Passive state. <br />Toggle OFF to setup this node in a cluster</small>",
            placement: "top",
            html: true
        })

        popoverUtils.longWithHover($("#http-port-info"), {
            content: "<small>HTTP port used for clients/browser (RavenDB Studio) communication.</small>",
            html: true
        });

        popoverUtils.longWithHover($("#tcp-port-info"), {
            content: "<small>TCP port used by the cluster nodes to communicate with each other.</small>",
            html: true
        })
    }

    back() {
        router.navigate("#welcome");
    }

    save() {
        let isValid = true;
        let focusedOnInvalidNode = false;

        if (!this.isValid(this.validationGroup)) {
            isValid = false;
        }

        const nodes = this.model.nodes();
        nodes.forEach(node => {
            let validNodeConfig = true;
            
            if (!this.isValid(node.validationGroupForUnsecured)) {
                validNodeConfig = false;
            }

            node.ips().forEach(entry => {
                if (!this.isValid(entry.validationGroup)) {
                    validNodeConfig = false;
                }
            });

            if (!validNodeConfig) {
                isValid = false;
                if (!focusedOnInvalidNode) {
                    this.editedNode(node);
                    focusedOnInvalidNode = true;
                }
            }
        });
        
        if (isValid) {
            router.navigate("#finish");
        }
    }

    addNode() {
        const node = new nodeInfo(this.model.hostnameIsNotRequired, this.model.mode);
        this.model.nodes.push(node);
        
        if (this.model.nodes().length > 0) {
            this.model.startNodeAsPassive(false);
        }
        
        this.editedNode(node);
        node.nodeTag(this.findFirstAvailableNodeTag());

        this.updatePorts();
        this.initTooltips();
    }
   
    private findFirstAvailableNodeTag() {
        for (let nodesTagsKey of serverSetup.nodesTags) {
            if (!this.model.nodes().find(x => x.nodeTag() === nodesTagsKey)) {
                return nodesTagsKey;
            }
        }

        return "";
    }

    editNode(node: nodeInfo) {
        this.editedNode(node);
        this.initTooltips();
    }

    removeNode(node: nodeInfo) {
        this.model.nodes.remove(node);
        
        if (this.editedNode() === node) {
            this.editedNode(null);
        }
        
        if (this.model.nodes().length === 1) {
            this.editNode(this.model.nodes()[0]);
        }

        this.updatePorts();
    }

    updatePorts() {
        let idx = 0;
        this.model.nodes().forEach(node => {

            if (idx === 0 && this.model.fixPortNumberOnLocalNode()) {
                node.port(this.model.fixedLocalPort().toString());
            }

            if (idx === 0 && this.model.fixTcpPortNumberOnLocalNode()) {
                node.tcpPort(this.model.fixedTcpPort().toString());
            }

            idx++;
        });
    }
}

export = unsecured;
