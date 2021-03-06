import viewModelBase = require("viewmodels/viewModelBase");
import getSubscriptionCommand = require("commands/database/subscriptions/getSubscriptionCommand");
import setSubscriptionCommand = require("commands/database/subscriptions/setSubscriptionAckEtagCommand");
import Subscription = require("models/database/subscription/subscription");
import eventsCollector = require("common/eventsCollector");

class SubscriptionsTask extends viewModelBase {

    data = ko.observableArray<Subscription>();

    activate(args: any) {
        super.activate(args);

        this.updateHelpLink("23PHKW");
        this.activeDatabase.subscribe(() => this.fetchSubscriptions());
        return this.fetchSubscriptions();
    }

    fetchSubscriptions(): JQueryPromise<Array<Subscription>> {
        var db = this.activeDatabase();
        if (db) {
            return new getSubscriptionCommand(db)
                .execute()
                .done((results: Array<Subscription>) => this.data(results));
        }
        return null;
    }

    setSubscriptionEtag(subscription: Subscription) {
        eventsCollector.default.reportEvent("subscription", "set-etag");

        var db = this.activeDatabase();
        
        if (db) {
            subscription.isChangeInProgress(true);
            var newAckEtag = subscription.newAckEtag();
            return new setSubscriptionCommand(db, subscription.subscriptionId, newAckEtag)
                .execute()
                .done(() => subscription.ackEtag(newAckEtag))
                .always(() => subscription.isChangeInProgress(false));
        }
        return null;
    }

}

export = SubscriptionsTask;
