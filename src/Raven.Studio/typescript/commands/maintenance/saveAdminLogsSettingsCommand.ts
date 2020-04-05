import commandBase = require("commands/commandBase");
import endpoints = require("endpoints");

class saveAdminLogsSettingsCommand extends commandBase {
    
    constructor(private logMode: Sparrow.Logging.LogMode, private retentionTime: string, private retentionSize: Sparrow.Size, private compress: boolean) {
        super();
    }
    
    execute(): JQueryPromise<void> {
        const url = endpoints.global.adminLogs.adminLogsConfiguration;
        const payload = {
            Mode: this.logMode,
            RetentionTime: this.retentionTime,
            RetentionSize: this.retentionSize,
            Compress: this.compress
        };

        return this.post<void>(url, JSON.stringify(payload), null, { dataType: undefined })
            .done(() => this.reportSuccess("Admin log files Log Mode has been set to .... todo...."))
            .fail((response: JQueryXHR) => this.reportError("Failed to set admin log file Log Mode", response.responseText, response.statusText));
    }
}

export = saveAdminLogsSettingsCommand;
