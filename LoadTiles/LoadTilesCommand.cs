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
using System.Collections.Generic;
using Rhino.DocObjects;

namespace LoadTiles
{
    public class LoadTilesCommand : Command
    {
        private readonly HttpClient _cesiumClient, _gmapsClient;
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
        /// Translates the loaded tiles to the origin in Rhino3d.
        /// </summary>
        /// <param name="doc">Active document</param>
        /// <param name="objects">Imported objects</param>
        /// <param name="targetPoint">Point to translate to origin</param>
        /// <param name="lat">Corresponding latitude of targetPoint</param>
        /// <param name="lon">Corresponding longitude of targetPoint</param>
        private void TranslateLoadedTiles(RhinoDoc doc, IEnumerable<RhinoObject> objects, Point3d targetPoint, double lat, double lon) {
            double scaleFactor = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);
            
            // Calculate the three orthogonal vectors for rotation
            Vector3d up = new Vector3d(targetPoint);
            up.Unitize();
            Point3d pointToNorth = lat <= 89 ?
                Helper.LatLonToEPSG4978(lat + 1, lon, 0) :
                Helper.LatLonToEPSG4978(179 - lat, lon + 180, 0);
            Vector3d pointToNorthVec = new Vector3d(pointToNorth);
            Vector3d north = pointToNorthVec - up * pointToNorthVec * up;
            north.Unitize();
            Vector3d east = Vector3d.CrossProduct(north, up);

            // Calculates transformation needed to move to Rhino3d's origin
            Transform toOrigin = Transform.Translation(-targetPoint.X*scaleFactor, -targetPoint.Y*scaleFactor, -targetPoint.Z*scaleFactor);
            Transform rotate = Transform.Rotation(east, north, up, new Vector3d(1,0,0), new Vector3d(0,1,0), new Vector3d(0,0,1));
            Transform moveRot = rotate * toOrigin;

            // Applies the transformation to all loaded tiles
            foreach (RhinoObject obj in objects) {
                doc.Objects.Transform(obj, moveRot, true);
            }
        }

        /// <summary>
        /// Loads tiles from the Google Maps API, in a radius around the specified location.
        /// </summary>
        /// <param name="doc">Active document</param>
        /// <param name="targetPoint">Point3d object corresponding to specified location</param>
        /// <param name="renderDistance">Radius (metres) around the point to load. Formally this loads all tiles 
        /// whose bounding box's closest edge to the point is within this distance.</param>
        /// <returns>List of Rhino objects that were loaded</returns>
        private IEnumerable<RhinoObject> LoadTiles(RhinoDoc doc, Point3d targetPoint, double renderDistance) {
            RhinoApp.WriteLine("Loading tiles...");

            // Call the API to get the root tile
            HttpClient client = _gmapsClient;
            string response = fetchStringFromAPI(client, $"{url}?key={key}&session={session}");
            Tileset tileset = Tileset.Deserialize(response, new Uri(url, UriKind.Absolute), Transform.Identity);
            Tile tile = tileset.Root;

            var before = doc.Objects.GetObjectList(ObjectType.AnyObject);

            // Recursively load tiles from API
            TraverseTileTree(doc, tile, targetPoint, renderDistance);

            // Finds all objects imported from 3d tile
            var after = doc.Objects.GetObjectList(ObjectType.AnyObject);
            return after.Except(before);
        }

        /// <summary>
        /// Check if the tile is sufficiently detailed.
        /// This determines whether the tile should be loaded, or if it should be refined instead.
        /// <br/>
        /// This is currently based on the distance between the tile and the target point, and the geometric error of the tile.
        /// <br/>
        /// TODO: Make this based on SSE (Screen Space Error) instead, which requires distance between tile and camera
        /// </summary>
        /// <param name="tile">Tile to check</param>
        /// <param name="targetPoint">Point3d object to check distance to</param>
        /// <returns>Whether the tile should be rendered instead of refined.</returns>
        private bool IsSufficientlyDetailed(Tile tile, Point3d targetPoint) {
            double distance = Helper.PointDistanceToTile(targetPoint, tile);
            double geometricError = tile.GeometricError;
            double threshold = Math.Min(20, distance*0.02);  // TODO: Refine this, or make this a user-specified value?
            return geometricError < threshold;
        }

        /// <summary>
        /// Recursively traverses the tile tree using DFS, loading tiles as necessary.
        /// </summary>
        /// <param name="doc">Active document</param>
        /// <param name="tile">Tile to load and/or refine</param>
        /// <param name="targetPoint"></param>
        /// <param name="renderDistance"></param>
        private void TraverseTileTree(RhinoDoc doc, Tile tile, Point3d targetPoint, double renderDistance) {
            bool tileHasContent = tile.Contents.Count > 0;

            List<Tile> childrenWithinRenderDistance = tile.Children.FindAll(child => Helper.PointDistanceToTile(targetPoint, child) <= renderDistance);
            bool hasChildrenWithinDistance = childrenWithinRenderDistance.Count > 0;

            bool isSufficientlyDetailed = IsSufficientlyDetailed(tile, targetPoint);

            // Should the tile itself be loaded?
            bool shouldLoadTile = tileHasContent
                && (isSufficientlyDetailed || tile.Refine == Refine.ADD || !hasChildrenWithinDistance);

            // Should the GLB of the tile be loaded, if it exists? This weeds out tiles that are too far away,
            //   by checking if the tile itself is 'close' to the point but none of its children are
            bool shouldLoadGLB = shouldLoadTile
                && !(tile.Children.Count > 0 && !hasChildrenWithinDistance);

            // Should the tile be traversed further?
            bool shouldTraverse = hasChildrenWithinDistance && !isSufficientlyDetailed;

            if (shouldLoadTile) {
                string link = tile.Contents[0].Uri.ToString();
                string[] linkparts = link.Split('.');
                if (linkparts[linkparts.Length - 1] == "glb") {  // Content of this tile is a GLB file
                    if (shouldLoadGLB) LoadGLB(doc, tile, targetPoint);
                }
                else {   // Content of this tile is a JSON link (make a further API call)
                    string response = fetchStringFromAPI(_gmapsClient, $"{link}?key={key}&session={session}");
                    Tile nextTile = Tileset.Deserialize(response, new Uri(link, UriKind.Absolute), Transform.Identity).Root;  // TODO: Change transform
                    TraverseTileTree(doc, nextTile, targetPoint, renderDistance);
                }
            }
            if (shouldTraverse) {
                foreach (Tile childTile in childrenWithinRenderDistance) {
                    TraverseTileTree(doc, childTile, targetPoint, renderDistance);
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
            double lat = result.latitude;
            double lon = result.longitude;
            double altitude = 0;  // TODO: Make this user-specified - it affects whether the tiles are loaded above/below the XY-plane
            double renderDistance = 200;  // Radius around target point to load. TODO: Make this user-specified

            Point3d targetPoint = Helper.LatLonToEPSG4978(lat, lon, altitude);
            // Load tiles
            IEnumerable<RhinoObject> objects = LoadTiles(doc, targetPoint, renderDistance);
            // Move tiles to origin
            TranslateLoadedTiles(doc, objects, targetPoint, lat, lon);

            doc.Views.ActiveView.ActiveViewport.ZoomExtents();
            RhinoApp.WriteLine("Tiles loaded.");
            return Result.Success;
        }
    }
}
