﻿import intermediateMenuItem = require("common/shell/menu/intermediateMenuItem");
import leafMenuItem = require("common/shell/menu/leafMenuItem");
import separatorMenuItem = require('common/shell/menu/separatorMenuItem');

export = getTransformersMenuItem;

function getTransformersMenuItem(appUrls: computedAppUrls) {
    let transformersChildren = [
        new leafMenuItem({
            title: 'Transformers',
            route: 'databases/transformers',
            moduleId: 'viewmodels/database/transformers/transformers',
            css: 'icon-list',
            nav: true,
            dynamicHash: appUrls.transformers
        }),
        new leafMenuItem({
            route: 'databases/transformers/edit(/:transformerName)',
            moduleId: 'viewmodels/database/transformers/editTransformer',
            title: 'Edit Transformer',
            css: 'icon-edit',
            nav: false
        })
    ];

    return new intermediateMenuItem("Transformers", transformersChildren, 'icon-etl');
}
