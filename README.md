# Getting started with LoadTiles plugin
  Debug Mode:
  Open the repo directory in VS Code, then use the "Run and Debug" feature (look for it on the sidebar, then look for "Rhino 8 - netcore" in the run and debug configurations).
  It should open an instance of Rhino with the plugin loaded.
  
  In Rhino's command line (which should be positioned near the top of the window), type "Fetch". 
  If the plugin was loaded correctly it should start autocompleting this function name as you type it. 
  This should open a tab in your browser, prompting you to log-in and grant permission to fetch the user's access token.
  
  Note: you will need a Cesium Ion access token, which you can configure manually [here](https://ion.cesium.com/tokens).
  
  When prompted, select the tileset you would like to load from, then enter the longitude and latitude of where you want to load.
  You may wish to specify an altitude, but if left blank, altitude will autoselect to place tiles as close to initial plane as possible.
  You should also specify a radius (in metres) of tiles to load from your origin point. This is, at default, 200; larger values may result in much slower loading times.

  //TODO: Instructions on using masking tools. Instructions on compiling plugin for Windows/Mac.


## LoadTiles/sampletileset.json
This is a sample JSON response from Google Maps 3D tiles - to test (and save on API calls!)

## LoadTiles/LoadTilesCommand.cs
This is where most of the work on the plugin side of the project currently lies.

TODO:
- Selectively load the descendants of a tile. You can find an implementation of a traversal program [here](https://github.com/CesiumGS/cesium/blob/5eaa2280f495d8f300d9e1f0497118c97aec54c8/packages/engine/Source/Scene/Cesium3DTilesetBaseTraversal.js).
