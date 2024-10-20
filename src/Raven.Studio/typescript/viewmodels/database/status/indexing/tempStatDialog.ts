import dialog = require("plugins/dialog");
import dialogViewModelBase = require("viewmodels/dialogViewModelBase");
import aceEditorBindingHandler = require("common/bindingHelpers/aceEditorBindingHandler");

class tempStatDialog extends dialogViewModelBase {

    json = ko.observable("");

    constructor(private obj: any, replacer: (key: string, value: string) => any = null) {
        super(null);
        aceEditorBindingHandler.install();
        this.json(JSON.stringify(obj, replacer, 4));
    }

    close() {
        dialog.close(this);
    }
}

export = tempStatDialog; 
