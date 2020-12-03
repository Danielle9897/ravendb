import viewModelBase = require("viewmodels/viewModelBase");
import spatialQueryMapModel = require("models/database/query/spatialQueryMapModel");
import {Marker} from "leaflet";


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
//
// class geoPointsPerField {
//      geoField = ko.observable<geoPointNames>();
//      geoPoints = ko.observableArray<geoPoint>([]);
//      markerColor: string; // ?
//     
//      constructor(parentPropertyName: string, longitudeFieldName: string, latitudeFieldName: string, geoPoints: geoPoint[]) {
//          this.geoField().parentPropertyName = parentPropertyName;
//          this.geoField().longitudeName = longitudeFieldName;
//          this.geoField().latitudeName = latitudeFieldName;
//         
//          this.geoPoints(geoPoints);
//      }
// }

class spatialQueryMap extends viewModelBase {
    
    //myMap = L.map('mapid', { preferCanvas: true });
    isMyMapInitialized: boolean = false;
    
    //geoData = ko.observableArray<geoPointsPerField>([]);
    geoData = ko.observableArray<spatialQueryMapModel>([]);
    
    // open leaflet map with data....
    // act upon events  
  
    
    constructor(geoData: Array<spatialQueryMapModel>) {
        super();
        
        this.geoData(geoData);
        
    }
    
    compositionComplete() {
        super.compositionComplete();
        
        //initialize the map containter only once... here
        //this.myMap = L.map('mapid', { preferCanvas: true });
        
        this.play();
    }

    // feature group
    play() {
        ///////////////////////
        // define base layers
        ///////////////////////

        const osmLink = `<a href="http://openstreetmap.org">OpenStreetMap</a>`;
        const osmUrl = 'http://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
        const osmAttrib = `&copy; ${osmLink} Contributors`;
        const osmMap = L.tileLayer(osmUrl, {attribution: osmAttrib});

        const otmLink = `<a href="http://opentopomap.org/">OpenTopoMap</a>`;
        const otmUrl = `http://{s}.tile.opentopomap.org/{z}/{x}/{y}.png`;
        const otmAttrib = `&copy; ${otmLink} Contributors`;
        const otmMap = L.tileLayer(otmUrl, {attribution: otmAttrib});

        const baseLayers = {
            "<span style='color: red'>Streets Map</span>": osmMap,
            "Topography Map": otmMap
        };

        ///////////////////////////////////////////////////////////
        // define group layers (layer per geo field from document)
        //////////////////////////////////////////////////////////

        let markersLayers: Marker[][] = [];
        let layerGroups: any[] = [];
                
        let overlaysObj = {};

        this.geoData().forEach((fieldModel) => {
            let markersArray: any[] = [];
            fieldModel.geoPoints().forEach((point) => {
                const pointMarker = L.marker([point.latitude, point.longitude], { title: "pass doc id..."})
                    .bindPopup(point.tooltipContent);
                markersArray.push(pointMarker);
            });
            // const Lg = L.layerGroup(markersArray)
            const Fg = L.featureGroup(markersArray)
            
            layerGroups.push(Fg);
            (overlaysObj as any)[fieldModel.geoField().parentPropertyName] = Fg;
        })

        ///////////////////////
        // define the map
        ///////////////////////

        // decide the central position for the view first... and initial zoom
        const centralLat = 39.61;
        const centralLong = -105.02;
        const initialZoomLevel = 18;
       
        let myMap = L.map('mapid', {preferCanvas: true});

        myMap.setView([centralLat, centralLong], initialZoomLevel);
        this.isMyMapInitialized = true;

        ////////////////////////////
        // add stuff to the map
        ////////////////////////////

        osmMap.addTo(myMap); // base layer
        L.control.layers(baseLayers, overlaysObj).addTo(myMap); // the control

        layerGroups.forEach(x => (x).addTo(myMap));

        let allPoints: any[] = [];
        this.geoData().forEach(x => {
            x.geoPoints().forEach(y => {
                allPoints.push([y.latitude, y.longitude]);
            })
        })

        myMap.fitBounds(allPoints, {padding: [50, 50]});
        //quitmyMap.fitBounds(, {padding: [50, 50]});
    }
    
    // // layer group
    // play() {
    //     ///////////////////////
    //     // define base layers
    //     ///////////////////////
    //
    //     const osmLink = `<a href="http://openstreetmap.org">OpenStreetMap</a>`;
    //     const osmUrl = 'http://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
    //     const osmAttrib = `&copy; ${osmLink} Contributors`;
    //     const osmMap = L.tileLayer(osmUrl, {attribution: osmAttrib});
    //
    //     const otmLink = `<a href="http://opentopomap.org/">OpenTopoMap</a>`;
    //     const otmUrl = `http://{s}.tile.opentopomap.org/{z}/{x}/{y}.png`;
    //     const otmAttrib = `&copy; ${otmLink} Contributors`;
    //     const otmMap = L.tileLayer(otmUrl, {attribution: otmAttrib});
    //
    //     const baseLayers = {
    //         "<span style='color: red'>Streets Map</span>": osmMap,
    //         "Topography Map": otmMap
    //     };
    //
    //     ///////////////////////////////////////////////////////////
    //     // define group layers (layer per geo field from document)
    //     //////////////////////////////////////////////////////////
    //    
    //     let markersLayers: Marker[][] = [];
    //     // let layerGroups: LayerGroup[];
    //     let layerGroups: any[] = [];
    //    
    //     let overlaysObj = {};
    //    
    //     this.geoData().forEach((fieldModel) => {
    //         let markersArray: any[] = [];
    //         fieldModel.geoPoints().forEach((point) => {
    //             const pointMarker = L.marker([point.latitude, point.longitude], { title: "pass doc id..."})
    //                                  .bindPopup(point.tooltipContent);                
    //             markersArray.push(pointMarker);
    //         });            
    //         const Lg = L.layerGroup(markersArray)
    //         layerGroups.push(Lg);
    //         // Object.assign(overlaysObj, {string: `<span style='color: green'>${fieldModel.geoField().parentPropertyName.toString()}</span>`, any: Lg })
    //         //Object.assign(overlaysObj, {string: "test", any: Lg })
    //         (overlaysObj as any)[fieldModel.geoField().parentPropertyName] = Lg;
    //     })
    //
    //     // const littleton = L.marker([39.61, -105.02]).bindPopup('This is Littleton, CO.'),
    //     //     denver    = L.marker([39.74, -104.99]).bindPopup('This is Denver, CO.'),
    //     //     aurora    = L.marker([39.73, -104.8]).bindPopup('This is Aurora, CO.'),
    //     //     golden    = L.marker([39.77, -105.23]).bindPopup('This is Golden, CO.');
    //     //
    //     // const littleton2 = L.marker([39.62, -105.02]).bindPopup('This is Littleton, CO.2'),
    //     //     denver2    = L.marker([39.75, -104.99]).bindPopup('This is Denver, CO.2'),
    //     //     aurora2    = L.marker([39.74, -104.8]).bindPopup('This is Aurora, CO.2'),
    //     //     golden2    = L.marker([39.78, -105.23]).bindPopup('This is Golden, CO.2');
    //
    //    
    //     //const cities1 = L.layerGroup([littleton, denver, aurora, golden]);
    //     //const cities2 = L.layerGroup([littleton2, denver2, aurora2, golden2]);
    //
    //     //let overlaysObj = {};
    //     // layerGroups.forEach((x) => {
    //     //     Object.assign(overlaysObj, {string: `<span style='color: green'>${}</span>`, any: x })
    //     // });
    //     //
    //    
    //     // const overlays = {
    //     //     "<span style='color: green'>places1</span>": cities1,
    //     //     "places2": cities2
    //     // };
    //
    //     ///////////////////////
    //     // define the map
    //     ///////////////////////
    //
    //     // decide the central position for the view first... and initial zoom
    //     const centralLat = 39.61;
    //     const centralLong = -105.02;
    //     const initialZoomLevel = 18;
    //
    //     // let myMap = L.map('mapid', { preferCanvas: true, layers: [osmMap] });
    //     let myMap = L.map('mapid', {preferCanvas: true});
    //
    //     // if (!this.isMyMapInitialized) {
    //    
    //
    //         //myMap = L.map('mapid', {preferCanvas: true});
    //
    //
    //         myMap.setView([centralLat, centralLong], initialZoomLevel);
    //         this.isMyMapInitialized = true;
    //
    //         // L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    //         //     attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    //         // }).addTo(mymap);
    //
    //         ////////////////////////////
    //         // add stuff to the map
    //         ////////////////////////////
    //
    //         osmMap.addTo(myMap); // base layer
    //         L.control.layers(baseLayers, overlaysObj).addTo(myMap); // the control
    //
    //         layerGroups.forEach(x => (x).addTo(myMap));
    //        
    //        
    //        
    //         //cities1.addTo(myMap); // group layer 1 dots
    //         //cities2.addTo(myMap); // group layer 2 dots
    //
    //         // define bounds....todo provide only the corner max min points ....?
    //        
    //         let allPoints: any[] = [];
    //         this.geoData().forEach(x => {
    //             x.geoPoints().forEach(y => {
    //                 allPoints.push([y.latitude, y.longitude]);
    //             })
    //         })
    //        
    //         myMap.fitBounds(allPoints, {padding: [50, 50]});
    //        
    //        
    //        
    //         //myMap.fitBounds([[39.61, -105.02], [39.74, -104.99], [39.73, -104.8], [39.77, -105.23], [40.80, -105.23], [41.80, -105.23]]); //Centers and zooms the map around the bounds, overrides initial zoom definition....
    //         // myMap.fitBounds([[39.61, -105.02], [39.74, -104.99], [39.73, -104.8], [40.80, -105.23]]); //Centers and zooms the map around the bounds, overrides initial zoom definition....
    //
    //
    //         // use overlay , layerGroup for each field set.... 
    //         // see in: https://leanpub.com/leaflet-tips-and-tricks/read  !!!
    //
    //         //plugin for clustering... https://leafletjs.com/plugins.html#clusteringdecluttering
    //
    //         // which point will be the center one to start with ???
    //         // https://stackoverflow.com/questions/18146070/finding-center-and-zoom-level-in-leaflet-given-a-list-of-lat-long-pairs
    //   
    // }
    
    
    // hardcoded
    // play() {
    //     ///////////////////////
    //     // define base layers
    //     ///////////////////////
    //    
    //     const osmLink = `<a href="http://openstreetmap.org">OpenStreetMap</a>`;
    //     const osmUrl = 'http://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
    //     const osmAttrib = `&copy; ${osmLink} Contributors`;
    //     const osmMap = L.tileLayer(osmUrl, {attribution: osmAttrib});
    //    
    //     const otmLink = `<a href="http://opentopomap.org/">OpenTopoMap</a>`;
    //     const otmUrl = `http://{s}.tile.opentopomap.org/{z}/{x}/{y}.png`;
    //     const otmAttrib = `&copy; ${otmLink} Contributors`;
    //     const otmMap = L.tileLayer(otmUrl, {attribution: otmAttrib});
    //
    //     const baseLayers = {
    //         "<span style='color: red'>Streets Map</span>": osmMap,
    //         "Topography Map": otmMap
    //     };
    //
    //     ///////////////////////////////////////////////////////////
    //     // define group layers (layer per geo field from document)
    //     //////////////////////////////////////////////////////////
    //
    //     const littleton = L.marker([39.61, -105.02]).bindPopup('This is Littleton, CO.'),
    //         denver    = L.marker([39.74, -104.99]).bindPopup('This is Denver, CO.'),
    //         aurora    = L.marker([39.73, -104.8]).bindPopup('This is Aurora, CO.'),
    //         golden    = L.marker([39.77, -105.23]).bindPopup('This is Golden, CO.');
    //
    //    
    //     //let a = golden;
    //    
    //     const littleton2 = L.marker([39.62, -105.02]).bindPopup('This is Littleton, CO.2'),
    //         denver2    = L.marker([39.75, -104.99]).bindPopup('This is Denver, CO.2'),
    //         aurora2    = L.marker([39.74, -104.8]).bindPopup('This is Aurora, CO.2'),
    //         golden2    = L.marker([39.78, -105.23]).bindPopup('This is Golden, CO.2');
    //
    //     const cities1 = L.layerGroup([littleton, denver, aurora, golden]);
    //     const cities2 = L.layerGroup([littleton2, denver2, aurora2, golden2]);
    //            
    //     const overlays = {
    //         "<span style='color: green'>places1</span>": cities1,
    //         "places2": cities2
    //     };
    //    
    //     ///////////////////////
    //     // define the map
    //     ///////////////////////
    //    
    //     // decide the central position for the view first... and initial zoom
    //     const centralLat = 39.61;
    //     const centralLong = -105.02;
    //     const initialZoomLevel = 18;
    //    
    //     // let myMap = L.map('mapid', { preferCanvas: true, layers: [osmMap] });
    //     let myMap;
    //    
    //     // if (!this.isMyMapInitialized) {
    //     if (true) {
    //        
    //         myMap = L.map('mapid', {preferCanvas: true});
    //
    //
    //         myMap.setView([centralLat, centralLong], initialZoomLevel);
    //         this.isMyMapInitialized = true;
    //
    //         // L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    //         //     attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    //         // }).addTo(mymap);
    //
    //         ////////////////////////////
    //         // add stuff to the map
    //         ////////////////////////////
    //
    //         osmMap.addTo(myMap); // base layer
    //         L.control.layers(baseLayers, overlays).addTo(myMap); // the control
    //
    //         cities1.addTo(myMap); // group layer 1 dots
    //         cities2.addTo(myMap); // group layer 2 dots
    //
    //         // define bounds....todo provide only the corner max min points ....
    //         myMap.fitBounds([[39.61, -105.02], [39.74, -104.99], [39.73, -104.8], [39.77, -105.23], [40.80, -105.23], [41.80, -105.23]]); //Centers and zooms the map around the bounds, overrides initial zoom definition....
    //         // myMap.fitBounds([[39.61, -105.02], [39.74, -104.99], [39.73, -104.8], [40.80, -105.23]]); //Centers and zooms the map around the bounds, overrides initial zoom definition....
    //
    //
    //         L.marker([51.5, -0.09], {opacity: 0.9, title: 'test title'})
    //             // L.marker([51.5, -0.09], { opacity: 0.8, interactive: true, riseOnHover: true, title: 'test title' })
    //             .addTo(myMap)
    //             .bindPopup('A pretty CSS3 popup.<br> Easily customizable.');
    //         //.openPopup();
    //
    //         const circle = L.circle([51.508, -0.11], {
    //             color: 'red',
    //             fillColor: '#f03',
    //             fillOpacity: 0.5,
    //             radius: 500
    //         }).addTo(myMap);
    //
    //
    //         // var popup = L.popup()
    //         //     .setLatLng([51.5, -0.09])
    //         //     .setContent("I am a standalone popup.")
    //         //     .openOn(mymap);
    //
    //
    //         // use overlay , layerGroup for each field set.... 
    //         // see in: https://leanpub.com/leaflet-tips-and-tricks/read  !!!
    //
    //         //plugin for clustering... https://leafletjs.com/plugins.html#clusteringdecluttering
    //
    //         // which point will be the center one to start with ???
    //         // https://stackoverflow.com/questions/18146070/finding-center-and-zoom-level-in-leaflet-given-a-list-of-lat-long-pairs
    //     }
    // }
}

export = spatialQueryMap;
