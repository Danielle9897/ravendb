import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import endpoints = require("endpoints");

class verifyDocumentsIDsCommand extends commandBase {

    static IDsLocalStorage: Array<string> = []; //TODO: it isn't scoped to current database?
    static InvalidIDsLocal: Array<string> = [];

    constructor(private docIDs: string[], private db: database, private queryLocalStorage:boolean, private storeResultsInLocalStorage:boolean) {
        super();

        if (!docIDs) {
            throw new Error("Must specify IDs");
        }

        if (!db) {
            throw new Error("Must specify database");
        }
    }

    execute(): JQueryPromise<Array<string>> {
        
        var verifyResult = $.Deferred<Array<string>>();       
        var verifiedIDs: string[] = [];

        // if required to check with locally stored document ids first, remove known non existing documet ids first and confirm verified ids later
        if (this.queryLocalStorage) {

            if (verifyDocumentsIDsCommand.InvalidIDsLocal.length > 0) {
                _.pullAll(this.docIDs, verifyDocumentsIDsCommand.InvalidIDsLocal);
            }

            if (verifyDocumentsIDsCommand.IDsLocalStorage.length > 0) {
                this.docIDs.forEach(curId => {
                    if (verifyDocumentsIDsCommand.IDsLocalStorage.find(x => x === curId)) {
                        verifiedIDs.push(curId);
                    } 
                });

                _.pullAll(this.docIDs, verifyDocumentsIDsCommand.IDsLocalStorage);
            }
        } 

        if (this.docIDs.length > 0) {
            var url = endpoints.databases.document.docs;
            var postResult = this.post(url + "?metadata-only=true", JSON.stringify(this.docIDs), this.db);
            postResult.fail((xhr: JQueryXHR) => verifyResult.reject(xhr));
            postResult.done((queryResult: queryResultDto<documentDto>) => {
                if (queryResult && queryResult.Results) {
                    queryResult.Results.forEach(curVerifiedID => {
                        verifiedIDs.push(curVerifiedID['@metadata']['@id']);                        
                        if (this.queryLocalStorage) {
                            verifyDocumentsIDsCommand.IDsLocalStorage.push(curVerifiedID['@metadata']['@id']);
                        }
                    });

                    if (this.queryLocalStorage) {
                        _.pullAll(this.docIDs, queryResult.Results.map(curResult => curResult['@metadata']['@id']));
                        verifyDocumentsIDsCommand.InvalidIDsLocal.push(...this.docIDs);
                    }
                }
                verifyResult.resolve(verifiedIDs);
            });
            return verifyResult;
        } else {
            verifyResult.resolve(verifiedIDs);
            return verifyResult;
        }
    }
}

export = verifyDocumentsIDsCommand;
