var studioWwwrootPrefix = "../../src/Raven.Studio/wwwroot/";

var require = {
    paths: {
        'jquery': studioWwwrootPrefix + 'lib/jquery/dist/jquery',
        'chai': studioWwwrootPrefix + 'lib/chai/chai',
        'Squire': studioWwwrootPrefix + 'lib/Squire.js/src/Squire',
        'utils': 'js/utils',
        "moment": studioWwwrootPrefix + "lib/moment/moment",
        'ace': studioWwwrootPrefix + 'Content/ace',
        "d3": studioWwwrootPrefix + "lib/d3/d3",
        "rbush": studioWwwrootPrefix + "Content/rbush/rbush",
        "quickselect": studioWwwrootPrefix + "Content/rbush/quickselect",

        'endpoints': studioWwwrootPrefix + 'App/endpoints',
        'configuration': studioWwwrootPrefix + 'App/configuration',
        'text': studioWwwrootPrefix + 'lib/requirejs-text/text',
        'viewmodels': studioWwwrootPrefix + 'App/viewmodels/',
        'models': studioWwwrootPrefix + 'App/models/',
        'common': studioWwwrootPrefix + 'App/common/',
        'commands': studioWwwrootPrefix + 'App/commands/',
        'widgets': studioWwwrootPrefix + 'App/widgets/',
        'durandal': studioWwwrootPrefix + 'lib/Durandal/js',
        'plugins': studioWwwrootPrefix + 'lib/Durandal/js/plugins/',
        'src/Raven.Studio/typescript': studioWwwrootPrefix + "App/",
        'src/Raven.Studio/wwwroot': studioWwwrootPrefix,
        
        'mocks': 'js/mocks'
    },
    map: {
        '*': {
            'forge': '../../src/Raven.Studio/wwwroot/lib/forge/js/forge',
        }

    }
};