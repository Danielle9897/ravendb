/// <reference path="../../typings/tsd.d.ts" />

import alertType = require("common/alertType");
import genUtils = require("common/generalUtils");

class alertArgs {
    id: string;
    private detailsObject: any;
    private parsedErrorInfo: any;

    buttonName: string = null;
    onClickAction: () => any = null;

    constructor(public type: alertType, public title: string, public details: string = "", public httpStatusText: string = "", public displayInRecentErrors: boolean = true) {
        this.id = "alert_" + genUtils.hashCode(title + details);
    }

    setupButtton(buttonName: string, onClickAction: () => any) {
        this.buttonName = buttonName;
        this.onClickAction = onClickAction;
    }

    get errorMessage(): string {
        const error = this.errorInfo;
        if (error && error.message) {
            return error.message;
        }

        return null;
    }

    get errorInfo(): { message: string; stackTrace: string; url: string; } {
        if (this.parsedErrorInfo) {
            return this.parsedErrorInfo;
        }

        if (this.type !== alertType.danger && this.type !== alertType.warning) {
            return null;
        }

        // See if we can tease out an error message from the details string.
        const detailsObj = this.getDetailsObject();
        if (detailsObj) {
            const error: string = detailsObj.Error;
            if (error && typeof error === "string") {
                const indexOfStackTrace = error.indexOf("\r\n");

                if (indexOfStackTrace !== -1) {
                    this.parsedErrorInfo = {
                        message: detailsObj.Message?detailsObj.Message:error.substr(0, indexOfStackTrace),
                        stackTrace: detailsObj.Message?error:error.substr(indexOfStackTrace + "\r\n".length),
                        url: detailsObj.Url || ""
                    };
                } else {
                    this.parsedErrorInfo = {
                        message: detailsObj.Message?detailsObj.Message:error,
                        stackTracke: error,
                        url: detailsObj.Url
                    }
                }
            }
        }

        return this.parsedErrorInfo;
    }

    getDetailsObject(): any {
        if (this.detailsObject) {
            return this.detailsObject;
        }

        if (this.details) {
            try {
                this.detailsObject = JSON.parse(this.details);
            } catch (error) {
                return null;
            }
        }

        return this.detailsObject;
    }
}

export = alertArgs;
