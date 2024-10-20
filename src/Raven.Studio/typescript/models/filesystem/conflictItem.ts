/// <reference path="../../../typings/tsd.d.ts"/>

import conflictHistory = require("models/filesystem/conflictHistory");

class conflictItem {

    fileName: string;
    remoteServerUrl: string;
    resolveUsingRemote = false;
    status: string;
    remoteHistory: conflictHistory[];
    currentHistory: conflictHistory[];

    constructor(fileName: string, remoteServerUrl: string, resolveUsingRemote: boolean) {
        this.fileName = fileName;
        this.remoteServerUrl = remoteServerUrl;
        this.remoteHistory = [];
        this.currentHistory = [];
        this.resolveUsingRemote = resolveUsingRemote;
        this.status = this.resolveUsingRemote ? "Scheduled resolution using remote version" : "Not resolved";
    }

    getDocumentPropertyNames(): Array<string> {
        return ["id", "fileName", "remoteServerUrl", "status"];
    }

    getId() {
        return this.fileName;
    }

    getUrl() {
        return this.fileName;
    }

    static fromConflictItemDto(dto: filesystemConflictItemDto) : conflictItem {
        var item = new conflictItem(dto.FileName, dto.RemoteServerUrl, dto.ResolveUsingRemote);
        if (dto.RemoteHistory != null) {
            const remoteHistory = dto.RemoteHistory.map(x => new conflictHistory(x.Version, x.ServerId));
            item.remoteHistory.push(...remoteHistory);
        }
        if (dto.CurrentHistory != null) {
            const currentHistory = dto.CurrentHistory.map(x => new conflictHistory(x.Version, x.ServerId))
            item.currentHistory.push(...currentHistory);
        }
        return item;
    }

}

export = conflictItem;
