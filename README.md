# Getting started with LoadTiles plugin
Open the repo directory in VS Code, then use the "Run and Debug" feature (look for it on the sidebar, then look for "Rhino 8 - netcore" in the run and debug configurations).
It should open an instance of Rhino with the plugin loaded.

In Rhino's command line (which should be positioned near the top of the window), type "Fetch". If the plugin was loaded correctly it should start autocompleting this function name as you type it.

When prompted, enter your Cesium Ion access token - which you can configure [here](https://ion.cesium.com/tokens).

Wait for a message on the Rhino command line that says "Import succeeded".

## LoadTiles/sampletileset.json
This is a sample JSON response from Google Maps 3D tiles - to test (and save on API calls!)

## LoadTiles/LoadTilesCommand.cs
This is where most of the work on the plugin side of the project currently lies.
TODO (Critical):
- Load more than one GLB tile at a time