# Getting started with LoadTiles plugin
Open the repo directory in VS Code, then use the "Run and Debug" feature (look for it on the sidebar, then look for "Rhino 8 - netcore" in the run and debug configurations).
It should open an instance of Rhino with the plugin loaded.

In Rhino's command line (which should be positioned near the top of the window), type "Fetch". If the plugin was loaded correctly it should start autocompleting this function name as you type it.

You will need a Cesium Ion access token, which you can configure [here](https://ion.cesium.com/tokens).

When prompted, enter the longitude and latitude of where you want to load. Currently the option entered for model does not affect the source for the tiles.

For convenience, you should create a `.env` file in this root directory storing your API query parameters for Google Maps' API. More details in the comment in the `RunCommand` method in `LoadTiles/LoadTilesCommand.cs`.

## LoadTiles/sampletileset.json
This is a sample JSON response from Google Maps 3D tiles - to test (and save on API calls!)

## LoadTiles/LoadTilesCommand.cs
This is where most of the work on the plugin side of the project currently lies.

## /dover.3dm
This is a sample model file containing a single object, and preloaded coordinates. We can use this for testing with the masking feature, because the terrain is actually interesting.

TODO:
- Selectively load the descendants of a tile. You can find an implementation of a traversal program [here](https://github.com/CesiumGS/cesium/blob/5eaa2280f495d8f300d9e1f0497118c97aec54c8/packages/engine/Source/Scene/Cesium3DTilesetBaseTraversal.js).
