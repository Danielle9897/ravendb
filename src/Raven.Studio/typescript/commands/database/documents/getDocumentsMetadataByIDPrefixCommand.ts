import commandBase = require("commands/commandBase");
import database = require("models/resources/database");

class getDocumentsMetadataByIDPrefixCommand extends commandBase {

    constructor(private prefix:string,private resultsAmount: number, private db: database) {
        super();
    }

    execute(): JQueryPromise<queryResultDto<documentMetadataDto>> {
        var url = '/docs';//TODO: use endpoints
        var args = {
            'startsWith': this.prefix,
            'exclude': <string> null,
            'start': 0,
            'pageSize': this.resultsAmount,
            'metadata-only': true
        };
        return this.query<any>(url, args, this.db, x => x.Results);
    }
}

export = getDocumentsMetadataByIDPrefixCommand;
