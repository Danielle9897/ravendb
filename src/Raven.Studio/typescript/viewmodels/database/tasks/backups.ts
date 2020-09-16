import app = require("durandal/app");
import appUrl = require("common/appUrl");
import router = require("plugins/router");
import viewModelBase = require("viewmodels/viewModelBase");
import database = require("models/resources/database");
import databaseInfo = require("models/resources/info/databaseInfo");
import ongoingTasksCommand = require("commands/database/tasks/getOngoingTasksCommand");
import ongoingTaskReplicationHubDefinitionListModel = require("models/database/tasks/ongoingTaskReplicationHubDefinitionListModel");
import ongoingTaskBackupListModel = require("models/database/tasks/ongoingTaskBackupListModel");
import clusterTopologyManager = require("common/shell/clusterTopologyManager");
import ongoingTaskModel = require("models/database/tasks/ongoingTaskModel");
import deleteOngoingTaskCommand = require("commands/database/tasks/deleteOngoingTaskCommand");
import toggleOngoingTaskCommand = require("commands/database/tasks/toggleOngoingTaskCommand");
import databaseGroupGraph = require("models/database/dbGroup/databaseGroupGraph");
import getDatabaseCommand = require("commands/resources/getDatabaseCommand");
import ongoingTaskListModel = require("models/database/tasks/ongoingTaskListModel");
import accessManager = require("common/shell/accessManager");
import eventsCollector = require("common/eventsCollector");
import getManualBackupCommand = require("commands/database/tasks/getManualBackupCommand");
import generalUtils = require("common/generalUtils");

// type TasksNamesInUI = "External Replication" | "RavenDB ETL" | "SQL ETL" | "Backup" | "Subscription" | "Replication Hub" | "Replication Sink";
type TasksNamesInUI = "Backup"; // todo - is needed ?

class recentManualBackupInfo {
    backupType = ko.observable<string>();
    isEncrypted = ko.observable<boolean>();
    nodeTag = ko.observable<string>();

    lastFullBackup = ko.observable<string>();
    lastFullBackupHumanized: KnockoutComputed<string>;
    backupDestinationsHumanized: KnockoutComputed<string>;

    constructor(dto: Raven.Client.Documents.Operations.Backups.PeriodicBackupStatus) {
        this.backupType(dto.BackupType);
        this.isEncrypted(true); // todo - when Egor fixes...
        this.nodeTag(dto.NodeTag);
        this.nodeTag("A");  // todo - when Egor fixes...

        this.lastFullBackup(dto.LastFullBackup);
        this.lastFullBackupHumanized = ko.pureComputed(() => {
            const lastFullBackup = dto.LastFullBackup;
            if (!lastFullBackup) {
                return "Never backed up";
            }

            return generalUtils.formatDurationByDate(moment.utc(lastFullBackup), true);
        });
        
        this.backupDestinationsHumanized = ko.pureComputed(() => {
            let destinations: Array<string> = [];

            if (dto.LocalBackup) {
                destinations.push("Local");
            }
            if (!dto.UploadToS3.Skipped) {
                destinations.push("S3");
            }
            if (!dto.UploadToGlacier.Skipped) {
                destinations.push("Glacier");
            }
            if (!dto.UploadToGlacier.Skipped) {
                destinations.push("Glacier");
            }
            if (!dto.UploadToAzure.Skipped) {
                destinations.push("Azure");
            }
            if (!dto.UploadToGoogleCloud.Skipped) {
                destinations.push("Google Cloud");
            }
            if (!dto.UploadToFtp.Skipped) {
                destinations.push("Ftp");
            }

            return destinations.length ? destinations.join(', ') : "No destinations defined";
        });
    }
}

class backups extends viewModelBase {
    
    private clusterManager = clusterTopologyManager.default;
    myNodeTag = ko.observable<string>();

    private graph = new databaseGroupGraph();
    private watchedBackups = new Map<number, number>();
    
    periodicBackupTasks = ko.observableArray<ongoingTaskBackupListModel>();
    recentManualBackup = ko.observable<recentManualBackupInfo>();
   
    showBackupSection = this.createShowSectionComputed(this.periodicBackupTasks, 'Backup'); // todo - Backup ???
    
    tasksTypesOrderForUI = ["Backup"] as Array<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskType>; // todo ???
    backupsOnly = true;
    
    existingNodes = ko.observableArray<string>();
    selectedNode = ko.observable<string>();

    canNavigateToServerWideBackupTasks: KnockoutComputed<boolean>;
    serverWideBackupUrl: string;
    ongoingTasksUrl: string;
    
    constructor() {
        super();
        this.bindToCurrentInstance("confirmRemoveOngoingTask", "confirmEnableOngoingTask", "confirmDisableOngoingTask",
                                   "toggleDetails", "addNewPeriodicBackupTask");
        this.initObservables();
    }

    private initObservables() {
        this.myNodeTag(this.clusterManager.localNodeTag());        
        this.serverWideBackupUrl = appUrl.forServerWideBackupList();
        this.ongoingTasksUrl = appUrl.forOngoingTasks(this.activeDatabase());
        this.canNavigateToServerWideBackupTasks = accessManager.default.clusterAdminOrClusterNode;
    }

    activate(args: any): JQueryPromise<any> {
        super.activate(args);
        
        return $.when<any>(this.fetchDatabaseInfo(), this.fetchOngoingTasks(), this.fetchManualBackup());
    }

    attached() {
        super.attached();

        this.addNotification(this.changesContext.serverNotifications()
            .watchClusterTopologyChanges(() => this.refresh()));
        
        this.addNotification(this.changesContext.serverNotifications()
            .watchDatabaseChange(this.activeDatabase().name, () => this.refresh()));
        
        this.addNotification(this.changesContext.serverNotifications().watchReconnect(() => this.refresh()));
        
        this.updateUrl(appUrl.forBackups(this.activeDatabase()));
        
        this.selectedNode("All nodes"); 
    }

    compositionComplete(): void {
        super.compositionComplete();

        this.registerDisposableHandler($(document), "fullscreenchange", () => {
            $("body").toggleClass("fullscreen", $(document).fullScreen());
            this.graph.onResize();
        });
        
        this.graph.init($("#databaseGroupGraphContainer"));
    }   

    createResponsibleNodeUrl(task: ongoingTaskListModel) {
        return ko.pureComputed(() => {
            const node = task.responsibleNode();
            const db = this.activeDatabase();
            
            if (node && db) {
                return node.NodeUrl + appUrl.forOngoingTasks(db);
            }
            
            return "#";
        });
    }

    private createShowSectionComputed(tasksContainer: KnockoutObservableArray<{ responsibleNode: KnockoutObservable<Raven.Client.ServerWide.Operations.NodeId> }>, taskType: TasksNamesInUI) {
        return ko.pureComputed(() =>  {
            const hasAnyTask = tasksContainer().length > 0;
            // const matchesSelectTaskType = this.selectedTaskType() === taskType || this.selectedTaskType() === "All tasks";
            const matchesSelectTaskType = true; // todo ???

            let nodeMatch = true;
            if (this.selectedNode() !== "All nodes") {
                nodeMatch = !!tasksContainer().find(x => x.responsibleNode() && x.responsibleNode().NodeTag === this.selectedNode());
            }

            return hasAnyTask && matchesSelectTaskType && nodeMatch;
        });
    }
    
    private refresh() {
        // todo fetch recent manual....
        return $.when<any>(this.fetchDatabaseInfo(), this.fetchOngoingTasks());
    }
    
    private fetchDatabaseInfo() {
        return new getDatabaseCommand(this.activeDatabase().name)
            .execute()
            .done(dbInfo => {
                this.graph.onDatabaseInfoChanged(dbInfo);
            });
    }

    private fetchOngoingTasks(): JQueryPromise<Raven.Server.Web.System.OngoingTasksResult> {
        const db = this.activeDatabase();
        return new ongoingTasksCommand(db)
            .execute()
            .done((info) => {
                this.processTasksResult(info);
                this.graph.onTasksChanged(info);
            });
    }

    private fetchManualBackup(): JQueryPromise<Raven.Client.Documents.Operations.Backups.GetPeriodicBackupStatusOperationResult> {
        const db = this.activeDatabase().name;
        return new getManualBackupCommand(db)
            .execute()
            .done((manualBackupInfo) => {
                this.processManualBackupResult(manualBackupInfo);
            });
    }
    
    private watchBackupCompletion(task: ongoingTaskBackupListModel) {
        if (!this.watchedBackups.has(task.taskId)) {
            let intervalId = setInterval(() => {
            task.refreshBackupInfo(false)
                .done(result => {
                    if (!result.OnGoingBackup) {
                        clearInterval(intervalId);
                        intervalId = 0;
                        this.watchedBackups.delete(task.taskId);
                    }
                })
            }, 3000);
            this.watchedBackups.set(task.taskId, intervalId);
            
            this.registerDisposable({
                dispose: () => {
                    if (intervalId) {
                        clearInterval(intervalId);
                        intervalId = 0;
                        this.watchedBackups.delete(task.taskId);
                    }
                }
            });    
        }
    }
    
    toggleDetails(item: ongoingTaskListModel) {
        item.toggleDetails();      
    }
    
    private processManualBackupResult(dto: Raven.Client.Documents.Operations.Backups.GetPeriodicBackupStatusOperationResult) {        
       this.recentManualBackup(dto.Status ? new recentManualBackupInfo(dto.Status) : null);
    }
    
    private processTasksResult(result: Raven.Server.Web.System.OngoingTasksResult) {
        const oldTasks = [
            ...this.periodicBackupTasks()
            ] as Array<{ taskId: number }>;

        const oldTaskIds = oldTasks.map(x => x.taskId);
        
        const newTaskIds = result.OngoingTasksList.map(x => x.TaskId);
        newTaskIds.push(...result.PullReplications.map(x => x.TaskId));

        const toDeleteIds = _.without(oldTaskIds, ...newTaskIds);

        const groupedTasks = _.groupBy(result.OngoingTasksList, x => x.TaskType);
       
        this.mergeTasks(this.periodicBackupTasks, 
            groupedTasks['Backup' as Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskType], 
            toDeleteIds,
            (dto: Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskBackup) => new ongoingTaskBackupListModel(dto, task => this.watchBackupCompletion(task)));
        
        // Sort backup tasks 
        const groupedBackupTasks = _.groupBy(this.periodicBackupTasks(), x => x.isServerWide());
        const serverWideBackupTasks = groupedBackupTasks.true;
        const ongoingBackupTasks = groupedBackupTasks.false;

        if (ongoingBackupTasks) {
            this.periodicBackupTasks(serverWideBackupTasks ? ongoingBackupTasks.concat(serverWideBackupTasks) : ongoingBackupTasks);            
        } else if (serverWideBackupTasks) {
            this.periodicBackupTasks(serverWideBackupTasks);
        }           
        
        //const taskTypes = Object.keys(groupedTasks);       

        // this.existingTaskTypes(this.tasksTypesOrderForUI.filter(x => _.includes(taskTypes, x))
        //     .map((taskType: Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskType) => {
        //         switch (taskType) {
        //             case "RavenEtl":
        //                 return "RavenDB ETL" as TasksNamesInUI;
        //             case "Replication":
        //                 return "External Replication" as TasksNamesInUI;
        //             case "SqlEtl":
        //                 return "SQL ETL" as TasksNamesInUI;
        //             case "PullReplicationAsHub":
        //                 return "Replication Hub" as TasksNamesInUI;
        //             case "PullReplicationAsSink":
        //                 return "Replication Sink" as TasksNamesInUI;
        //             default:
        //                 return taskType;
        //         }
        // }));
        
        this.existingNodes(_.uniq(result
            .OngoingTasksList
            .map(x => x.ResponsibleNode.NodeTag)
            .filter(x => x))
            .sort());
    }       
    
    private mergeTasks<T extends ongoingTaskListModel>(container: KnockoutObservableArray<T>, 
                                                       incomingData: Array<Raven.Client.Documents.Operations.OngoingTasks.OngoingTask>, 
                                                       toDelete: Array<number>,
                                                       ctr: (dto: Raven.Client.Documents.Operations.OngoingTasks.OngoingTask) => T) {
        // remove old tasks
        container()
            .filter(x => _.includes(toDelete, x.taskId))
            .forEach(task => container.remove(task));
        
        (incomingData || []).forEach(item => {
            const existingItem = container().find(x => x.taskId === item.TaskId);
            if (existingItem) {
                existingItem.update(item);
            } else {
                const newItem = ctr(item);
                const insertIdx = _.sortedIndexBy(container(), newItem, x => x.taskName().toLocaleLowerCase());
                container.splice(insertIdx, 0, newItem);
            }
        });
    }

    manageDatabaseGroupUrl(dbInfo: databaseInfo): string {
        return appUrl.forManageDatabaseGroup(dbInfo); // todo - what is this for ???
    }

    confirmEnableOngoingTask(model: ongoingTaskModel) {
        const db = this.activeDatabase();

        this.confirmationMessage("Enable Task", "You're enabling task of type: " + model.taskType(), {
            buttons: ["Cancel", "Enable"]
        })
            .done(result => {
                if (result.can) {
                    new toggleOngoingTaskCommand(db, model.taskType(), model.taskId, model.taskName(), false)
                        .execute()
                        .done(() => model.taskState('Enabled'))
                        .always(() => this.fetchOngoingTasks());
                }
            });
    }

    confirmDisableOngoingTask(model: ongoingTaskModel | ongoingTaskReplicationHubDefinitionListModel) {
        const db = this.activeDatabase();

        this.confirmationMessage("Disable Task", "You're disabling task of type: " + model.taskType(), {
            buttons: ["Cancel", "Disable"]
        })
            .done(result => {
                if (result.can) {
                    new toggleOngoingTaskCommand(db, model.taskType(), model.taskId, model.taskName(), true)
                        .execute()
                        .done(() => model.taskState('Disabled'))
                        .always(() => this.fetchOngoingTasks());
                }
            });
    }

    confirmRemoveOngoingTask(model: ongoingTaskModel) {
        const db = this.activeDatabase();
        
        this.confirmationMessage("Delete Task", "You're deleting task of type: " + model.taskType(), {
            buttons: ["Cancel", "Delete"]
        })
            .done(result => {
                if (result.can) {
                    this.deleteOngoingTask(db, model);
                }
            });
    }

    private deleteOngoingTask(db: database, model: ongoingTaskModel) {
        new deleteOngoingTaskCommand(db, model.taskType(), model.taskId, model.taskName())
            .execute()
            .done(() => this.fetchOngoingTasks());
    }
    
    addNewPeriodicBackupTask() {
        eventsCollector.default.reportEvent("PeriodicBackup", "new");
        const url = appUrl.forEditPeriodicBackupTask(this.activeDatabase());
        router.navigate(url); 
    }

    createManualBackup() {
        // add spinner to button... and disablt the button 
        // disable the restore button
        // call new ep 
        // upon done - get info again....fetch results... ???
    }

    setSelectedNode(node: string) {
        this.selectedNode(node);
    }

    navigateToRestoreDatabase() {
        const url = appUrl.forDatabases(null, true);
        router.navigate(url);
    }

    refreshManualBackupInfo() {
        this.fetchManualBackup();
    }
}

export = backups;
