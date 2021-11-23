import commandBase = require("commands/commandBase");
import endpoints = require("endpoints");
import database = require("models/resources/database");

class getEnvironmentStorageReportCommand extends commandBase {

    constructor(private db: database, private name: string, private type: string, private withDetails: boolean) {
        super();
    }

    execute(): JQueryPromise<detailedStorageReportItemDto> {
        const args = {
            name: this.name,
            type: this.type,
            details: this.withDetails
        };
        const url = endpoints.databases.storage.debugStorageEnvironmentReport + this.urlEncodeArgs(args);

        return this.query<detailedStorageReportItemDto>(url, null, this.db)
            .fail((response: JQueryXHR) => this.reportError("Failed to load detailed storage report", response.responseText, response.statusText));
    }
}

export = getEnvironmentStorageReportCommand;



// import commandBase = require("commands/commandBase");
// import endpoints = require("endpoints");
// import database = require("models/resources/database");
//
// class getEnvironmentStorageReportCommand extends commandBase {
//
//     constructor(private db: database, private name: string, private type: string) {
//         super();
//     }
//
//     execute(): JQueryPromise<detailedStorageReportItemDto> {
//         const args = {
//             name: this.name,
//             type: this.type,
//             details: true
//         };
//         const url = endpoints.databases.storage.debugStorageEnvironmentReport + this.urlEncodeArgs(args);
//        
//         return this.query<detailedStorageReportItemDto>(url, null, this.db)
//             .fail((response: JQueryXHR) => this.reportError("Failed to load detailed storage report", response.responseText, response.statusText));
//     }
// }
//
// export = getEnvironmentStorageReportCommand;
