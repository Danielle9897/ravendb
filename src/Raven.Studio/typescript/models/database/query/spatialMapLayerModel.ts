
// class geoPointNameInfo {
//     //parentPropertyName: string;
//     latitudeProperty: string;
//     longitudeProperty: string;    
//
//     constructor(latitudePropertyName: string, longitudePropertyName: string) {
//     // constructor(n1: string, n2: string, n3: string) {
//         //this.parentPropertyName = n1;
//         this.latitudeProperty = latitudePropertyName;
//         this.longitudeProperty = longitudePropertyName;        
//     }
// }

// class spatialQueryMapModel {
class spatialMapLayerModel {

    //geoField = ko.observable<geoPointNameInfo>();
    
    latitudeProperty: string;
    longitudeProperty: string;
    
    geoPoints = ko.observableArray<geoPoint>([]);
    
    //show: boolean;

    //constructor(parentPropertyName: string, longitudeFieldName: string, latitudeFieldName: string, geoPoints: geoPoint[]) {
    constructor(latitudePropertyName: string, longitudePropertyName: string, geoPoints: geoPoint[]) {

        //this.geoField(new geoPointNameInfo(parentPropertyName, longitudeFieldName, latitudeFieldName));
        this.latitudeProperty = latitudePropertyName;
        this.longitudeProperty = longitudePropertyName;
        this.geoPoints(geoPoints);
    }
}

export = spatialMapLayerModel;
