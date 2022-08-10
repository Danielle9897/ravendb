import database = require("models/resources/database");
import appUrl = require("common/appUrl");

class downloader {
    $downloadFrame = $("#downloadFrame");

    download(db: database, url: string) {

       // this.post<queryResultDto<documentDto>>(endpoints.databases.document.docs, JSON.stringify(payload), this.db);
       // url =  endpoints.databases.document.docs
        
        const dbUrl = appUrl.forDatabaseQuery(db);
        this.$downloadFrame.attr("src", dbUrl + url);
    }

    reset() {
        this.$downloadFrame.attr("src", "");
    }
}

export = downloader
