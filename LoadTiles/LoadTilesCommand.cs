using Rhino;
using Rhino.Commands;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using System;
using System.IO;
using TilesData;
using Rhino.Geometry;
using System.Linq;

namespace LoadTiles
{
    public class LoadTilesCommand : Command
    {
        private readonly HttpClient _cesiumClient, _gmapsClient;
        public double latitude, longitude, altitude; // We store the last inputted values so that we can write them to file when saving.
        public bool locationInputted = false;
        private string key, session, url;  // For calls to the *Google Maps 3D Tiles* API, not Cesium
        public LoadTilesCommand()
        {
            // Initialise Http Clients
            _cesiumClient = new HttpClient(){
                BaseAddress = new Uri("https://api.cesium.com"),
            };
            _gmapsClient = new HttpClient(){
                BaseAddress = new Uri("https://tile.googleapis.com"),
            };

            // Initialise Google Maps 3D Tiles API parameters
            DotNetEnv.Env.TraversePath().Load();
            key = DotNetEnv.Env.GetString("GMAPS_KEY");
            session = DotNetEnv.Env.GetString("GMAPS_SESSION");
            url = DotNetEnv.Env.GetString("GMAPS_URL");
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "Fetch";

        /// <summary>
        /// Makes an API call to retrieve a JSON object
        /// </summary>
        /// <param name="client">Http client to make the API call</param>
        /// <param name="url">Link to the API</param>
        /// <returns>JsonDocument representing the JSON returned</returns>
        private JsonDocument fetchJsonFromAPI(HttpClient client, string url) {
            return JsonDocument.Parse(fetchStringFromAPI(client, url));
        }

        /// <summary>
        /// Makes an API call to retrieve a GLB file
        /// </summary>
        /// <param name="client">Http client to make the API call</param>
        /// <param name="url">Link to the API</param>
        /// <returns>Byte array representing the GLB file</returns>
        private byte[] fetchGLBFromAPI(HttpClient client, string url) {
            return client.GetByteArrayAsync(url).Result;
        }

        /// <summary>
        /// Makes an API call to retrieve a string
        /// </summary>
        /// <param name="client">Http client to make the API call</param>
        /// <param name="url">Link to the API</param>
        /// <returns>Response from the API as a string</returns>
        private string fetchStringFromAPI(HttpClient client, string url) {
            return client.GetStringAsync(url).Result;
        }

        /// <summary>
        /// Retrieves the Google Maps 3D tiles asset from Cesium Ion.
        /// </summary>
        /// <param name="token">Cesium Ion authorisation token</param>
        /// <returns>The key for accessing Google Maps API</returns>
        private string GetGMapsKeyFromCesium(string token) {
            HttpClient client = _cesiumClient;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            string googleMapsAssetID = "2275207";  // I'm not sure if this is the same ID for everyone's Cesium Ion account.
            string url = $"/v1/assets/{googleMapsAssetID}/endpoint";
            // Fetch data
            using JsonDocument doc = fetchJsonFromAPI(client, url);
            // Extract link to Google Maps API (which comes with the key)
            string urlWithKey = doc.RootElement
                                   .GetProperty("options")
                                   .GetProperty("url")
                                   .ToString();
            // Extract key (which is a query parameter) from the URL
            string key = HttpUtility.ParseQueryString(new Uri(urlWithKey).Query)["key"];
            return key;
        }

        /// <summary>
        /// Calls the root of Google Maps API to fetch the session token.
        /// </summary>
        /// <param name="key">Google Maps API Key</param>
        /// <returns>( Session token, Link to next API to call )</returns>
        private (string, string) GetGMapsSessionToken(string key) {
            HttpClient client = _gmapsClient;
            // API call, to the root
            string response = fetchStringFromAPI(client, $"/v1/3dtiles/root.json?key={key}");
            Tileset tileset = Tileset.Deserialize(response, new Uri(client.BaseAddress+"/v1/3dtiles/root.json", UriKind.Absolute), Transform.Identity); // TODO: Change transform
            // We dive down several layers straight away - to the only follow-up API link that appears in the call to the root
            // This link included in the JSON response is special - it contains a session token, which must be included in all further API calls (to non-root nodes)
            string nextPathWithSession = tileset.Root.Children[0].Children[0].Contents[0].Uri.ToString();
            // The following lines simply manipulate the URL to extract the session token
            var parts = nextPathWithSession.Split("?");
            string url = parts[0];
            var queryParams = HttpUtility.ParseQueryString(parts.Length > 1 ? parts[1] : "");
            string session = queryParams["session"];
            return (session, url);
        }

        /// <summary>
        /// Navigates the asset tree to find the parent tile which contains, as descendants, all the tiles around the specified location we might want to load.
        /// </summary>
        /// <param name="point">Location that the tile should contain</param>
        /// <param name="geometricError">Upper bound of geometric error of parent tile</param>
        /// <returns>Tile at specified location</returns>
        private Tile FetchTile(Point3d point, double geometricErrorLimit) {
            HttpClient client = _gmapsClient;
            string response = fetchStringFromAPI(client, $"{url}?key={key}&session={session}");
            Tileset tileset = Tileset.Deserialize(response, new Uri(url, UriKind.Absolute), Transform.Identity); // TODO: Change transform
            Tile tile = tileset.Root;
            bool done = false;
            while (!done) {
                // Recursively traverses down the tree of assets to find children at finer levels of detail
                tile = tileset.Root;
                while (!done && tile.Children != null && tile.Children.Count > 0) {
                    // The bounding boxes of multiple children may overlap.
                    // We choose the child tile whose centre is closest to the target point.
                    Tile? nextTile = null;
                    double closest = double.MaxValue;

                    foreach (Tile childTile in tile.Children) {
                        TileBoundingBox bound = childTile.BoundingVolume.Box;
                        if (TileBoundingBox.IsInBox(bound, point)) {
                            double distance = point.DistanceTo(bound.Center);
                            if (distance < closest) {
                                nextTile = childTile;
                                closest = distance;
                            } 
                        }
                    }
                    // We are done if either of these are true
                    //   - nextTile == null implying our target point is not in the bounds of any child tile
                    //   - nextTile.GeometricError < geometricErrorLimit implying we have found a tile at the sufficient level of detail
                    if (nextTile == null || nextTile.GeometricError < geometricErrorLimit) done = true;
                    if (nextTile != null) tile = nextTile;
                }
                if (!done) {
                    // Make a further API call, to fetch tiles at a finer level of detail
                    url = tile.Contents[0].Uri.ToString();
                    response = fetchStringFromAPI(client, $"{url}?key={key}&session={session}");
                    tileset = Tileset.Deserialize(response, new Uri(url, UriKind.Absolute), Transform.Identity); // TODO: Change transform
                }
                
            }
            return tile;
        }

        /// <summary>
        /// Loads and imports the GLB associated with the given tile.
        /// </summary>
        /// <param name="doc">Active document</param>
        /// <param name="tile">Tile to load</param>
        /// <returns>Whether the import was successful.</returns>
        private bool LoadGLB(RhinoDoc doc, Tile tile, Point3d targetPoint) {
            string glbLink = tile.Contents[0].Uri.ToString();
            // Check that the link is indeed a GLB
            string[] linkparts = glbLink.Split('.');
            if (linkparts[linkparts.Length - 1] != "glb") return false;

            byte[] glbBytes = fetchGLBFromAPI(_gmapsClient, $"{glbLink}?key={key}&session={session}");

            string tempPath = Path.Combine(Path.GetTempPath(), "temp.glb");
            File.WriteAllBytes(tempPath, glbBytes);
            // Console.WriteLine("loaded glb");
            Console.WriteLine(Helper.GroundDistance(tile.BoundingVolume.Box.Center, targetPoint));
            // Read GLB from temp file
            return doc.Import(tempPath);
        }

        /// <summary>
        /// Loads the data at the subtree rooted at the given tile.
        /// </summary>
        /// <param name="doc">Active document</param>
        /// <param name="tile">Root tile</param>
        private void LoadTile(RhinoDoc doc, Tile tile, Point3d targetPoint) {
            var before = doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject);

            RhinoApp.WriteLine("Loading...");
            LoadTile2(doc, tile, targetPoint);

            double scaleFactor = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);
            Vector3d X = tile.BoundingVolume.Box.X;
            Vector3d Y = tile.BoundingVolume.Box.Y;
            Vector3d Z = tile.BoundingVolume.Box.Z;

            // Calculates transformation needed to move to Rhino3d's origin
            Transform toOrigin = Transform.Translation(-targetPoint.X*scaleFactor, -targetPoint.Y*scaleFactor, -targetPoint.Z*scaleFactor);
            Transform rotate = Transform.Rotation(X/X.Length, Y/Y.Length, Z/Z.Length, new Vector3d(0,0,1), new Vector3d(0,-1,0), new Vector3d(1,0,0));
            Transform moveRot = rotate * toOrigin;

            // Finds all objects imported from 3d tile
            var after = doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject);
            var imported = after.Except(before);

            // Applies the transformation to all loaded tiles
            foreach (var obj in imported) {
                doc.Objects.Transform(obj, moveRot, true);
            }

            doc.Views.ActiveView.ActiveViewport.ZoomExtents();
            RhinoApp.WriteLine("Loaded.");
        }

        private void LoadTile2(RhinoDoc doc, Tile tile, Point3d targetPoint) {
            // Tiles closer to the target point should be rendered at a lower geometric error, i.e. greater detail
            // Still, this is currently set rather arbitrarily, and should be adjusted to suit the application
            double threshold = Math.Min(20, Helper.GroundDistance(tile.BoundingVolume.Box.Center, targetPoint)*0.02);

            if (tile.Children == null || tile.Children.Count == 0 || tile.GeometricError < threshold) {
                if (tile.Contents.Count == 0) return;  // Apparently some tiles are contentless??
                string link = tile.Contents[0].Uri.ToString();
                string[] linkparts = link.Split('.');
                
                if (linkparts[linkparts.Length - 1] == "glb") {  // Content of this tile is a GLB file
                    bool pointWithinTile = TileBoundingBox.IsInBox(tile.BoundingVolume.Box, targetPoint);
                    double distance = pointWithinTile ? 0 : Helper.GroundDistance(tile.BoundingVolume.Box.Center, targetPoint);
                    // Only load tile if it falls within a certain radius around the target point
                    double maxRenderDistance = 200;
                    if (distance < maxRenderDistance) LoadGLB(doc, tile, targetPoint);
                }
                else {   // Content of this tile is a JSON link (make a further API call)
                    string response = fetchStringFromAPI(_gmapsClient, $"{link}?key={key}&session={session}");
                    Tile nextTile = Tileset.Deserialize(response, new Uri(link, UriKind.Absolute), Transform.Identity).Root;  // TODO: Change transform
                    LoadTile2(doc, nextTile, targetPoint);
                }
            }
            else {
                if (tile.Refine == Refine.ADD) LoadGLB(doc, tile, targetPoint);
                foreach (Tile childTile in tile.Children) {
                    LoadTile2(doc, childTile, targetPoint);
                }
            }
        }

        /// <summary>
        /// Handles the user running the command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("Fetching...");

            /* FOR DEVELOPMENT PURPOSES
             * In order to save on API calls since Cesium Ion has a usage limit,
             * it is recommended that you copy the console output of the key, session and url
             * from the code block below, then save these values for your own subsequent API calls.
             * To do so, create a .env file in the repo directory, and fill in the values in this format:
             * GMAPS_KEY="something"
             * GMAPS_SESSION="something"
             * GMAPS_URL="something"
             */
            bool valuesNotInitialised = key == null || session == null || url == null;

            GDPDialog dialog = new();
            if (this.locationInputted) {
                dialog.prefillData(this.latitude, this.longitude);
            }
            DialogResult result = dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow); 

            if (result == null) {
                RhinoApp.WriteLine("User canceled input.");
                return Result.Cancel;
            }

            if (valuesNotInitialised) {
                string tok = result.apiKey;
                key = GetGMapsKeyFromCesium(tok);
                (session, url) = GetGMapsSessionToken(key);
                
                Console.WriteLine(key);
                Console.WriteLine(session);
                Console.WriteLine(url);
            }

            // Configure location to render
            this.latitude = result.latitude;
            this.longitude = result.longitude;
            this.locationInputted = true;
            this.altitude = 0;  // TODO: Check if this is actually important and should be user-specified
            double geometricErrorLimit = 100;  // Chosen arbitrarily, but should fetch a tile at a decent zoom level

            Point3d targetPoint = Helper.LatLonToEPSG4978(this.latitude, this.longitude, this.altitude);
            Tile tile = FetchTile(targetPoint, geometricErrorLimit);
            LoadTile(doc, tile, targetPoint);
            return Result.Success;
        }
    }
}
