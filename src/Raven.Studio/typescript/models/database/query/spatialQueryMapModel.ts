

// interface geoPointNames {
//     parentPropertyName: string;
//     longitudeName: string;
//     latitudeName: string;
// }
//
// interface geoPoint {
//     longitude: number; // or string ?
//     latitude: number;
//     tooltipContent: string; 
// }

class geoPointNameInfo {
    parentPropertyName: string;
    longitudeName: string;
    latitudeName: string;
    
    constructor(n1: string, n2: string, n3: string) {
        this.parentPropertyName = n1;
        this.longitudeName = n2;
        this.latitudeName = n3;
    } 
}

class spatialQueryMapModel {
    
     geoField = ko.observable<geoPointNameInfo>();
     geoPoints = ko.observableArray<geoPoint>([]);
     
     markerColor: string; // ?
     show: boolean;

     constructor(parentPropertyName: string, longitudeFieldName: string, latitudeFieldName: string, geoPoints: geoPoint[]) {
         
         this.geoField(new geoPointNameInfo(parentPropertyName, longitudeFieldName, latitudeFieldName));

         this.geoPoints(geoPoints);
         
         //this.markerColor = color;
     }
}

export = spatialQueryMapModel;

//{
//
//     geoData = ko.observableArray<geoPointsPerField>([]);    
//    
//     constructor(geoData: geoPointsPerField[]) {
//             
//         this.geoData(geoData);
//     }
// }
