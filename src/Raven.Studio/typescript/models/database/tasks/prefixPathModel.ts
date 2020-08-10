import jsonUtil = require("common/jsonUtil");

class prefixPathModel {
    path = ko.observable<string>();
    validationGroup: KnockoutValidationGroup;

    //dirtyFlag: () => DirtyFlag; // is needed ?

    constructor(prefixPath: string) {

        this.path(prefixPath);
        this.initValidation();

        // i care about the dirty of the lists, not the path itself...
        
        // this.dirtyFlag = new ko.DirtyFlag([
        //     this.path,
        // ], false, jsonUtil.newLineNormalizingHashFunction);
    }

    initValidation() {
        this.path.extend({
            validation: [
                {
                    validator: () => {
                        if (this.path()) {
                            const pathLength = this.path().length;
                            const lastChar = this.path().charAt(pathLength - 1);
                            const prevChar = this.path().charAt(pathLength - 2);
                            return pathLength === 1 || lastChar != '*' || prevChar === '/' || prevChar === '-'
                        }
                        
                        return true;
                    },
                    message: "When using '*' as the last character, the previous character must be '/' or '-'"
                }
            ]
        });

        this.validationGroup = ko.validatedObservable({
            path: this.path
        });
    }
}

export = prefixPathModel;
