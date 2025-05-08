# 🦭 SeaLion
 A real-world imagery loader for [Rhino3D](https://www.rhino3d.com/)
 
![A render of the Radcliffe Camera and surrounding buildings, imported from Google's 3D Tiles API](https://i.imgur.com/juXmrB9.png)
## Getting started
This plugin makes use of the [Cesium Ion platform](https://ion.cesium.com/). Make an account on Cesium Ion before using this plugin.

Jump to:
* [Installation](#installation)
* [Loading tiles](#loading-tiles)
* [Masking](#masking)
## Installation 
You can find the .yak file for the plugin on the [Releases page](https://github.com/Z-snails/gdp-2025-caesium-rhino/releases).

To install this plugin on Rhino, type the following on the Rhino command line:
`_-PackageManager`. 
Then, select `Install`, then `Browse`, and select this plugin.

## Loading Tiles
In the Rhino command line, type `SealionFetch`. You will be prompted to authenticate with Cesium Ion, and your credentials will be saved for subsequent uses.

If authentication is successful (and immediately on each subsequent use), a GUI will appear.

### Model
Currently, two tilesets are supported by the plugin, both of which should be included in your Cesium Ion assets by default.
* **Google Photorealistic 3D Tiles** (Recommended): Provides both terrain data and buildings, and includes colour information.
* **Cesium OSM Buildings**: Provides only buildings, no terrain. No colour information included. *Known issue: Individual buildings very far away from the specified location may be loaded.*

### Specifying Location
The tileset will be loaded at the origin in Rhino3D.
* **Latitude**, **Longitude**: Specifies the target location which serves as the centre of the tiles to be loaded. The location itself will be loaded at the origin, barring altitude adjustments (see below).
* **Altitude**: If this is specified, then the location on the globe at the given latitude and longitude, and the specified altitude in metres, will be mapped to the origin. Alternatively, this may be left blank. In this case, the loader will attempt to map the ground level of the loaded tiles to the origin, but this is often imperfect.
* **Radius**: Specifies the radius around the target location which tiles should be loaded. Note that this is a *minimum*; some tiles which fall mostly outside of this radius may still be loaded. Increasing the radius may significantly extend loading time.

### Loading
After the parameters have been specified, click the `Import` button to begin loading tiles. The plugin calls the API of the specified tileset, and recursively traverses the tree of tiles to select the ones to load. Note that this can be a long process; expect to have to wait minutes for the tiles to be ready.

When the tileset has been successfully imported, the active viewport will be automatically zoomed to the extents of the loaded tiles.

### Clearing Up
To delete the loaded tiles, type `SealionDelete` in the command window.

## Masking
This plugin supports masking out parts of the tileset using Rhino objects. 

1. Create an object that overlaps the segment of the tiles to be masked out.
2. Type `SealionMask` in the command window to show the GUI.
3. Click `Add`, then click on the object created in Step 1.
4. You may have to wait some time while masking is being performed.
