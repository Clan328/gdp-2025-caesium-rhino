using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using System;
using System.IO;
using TilesData;
using Rhino.Geometry;

namespace LoadTiles
{
    public class LoadTilesCommand : Command
    {
        private readonly HttpClient _cesiumClient, _gmapsClient;
        public LoadTilesCommand()
        {
            // Initialise Http Clients
            _cesiumClient = new HttpClient(){
                BaseAddress = new Uri("https://api.cesium.com"),
            };
            _gmapsClient = new HttpClient(){
                BaseAddress = new Uri("https://tile.googleapis.com"),
            };
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
            Tileset tileset = Tileset.Deserialize(response, new Uri("/v1/3dtiles/root.json", UriKind.Relative));
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
        /// Navigates the asset tree to find the tile at a sufficient level of detail at the specified location.
        /// </summary>
        /// <param name="point">Location that the tile should match</param>
        /// <param name="key">Google Maps API key</param>
        /// <param name="session">Google Maps API session token</param>
        /// <param name="url">Relative URL of the base API call</param>
        /// <param name="geometricError">Upper bound of geometric error of target tile</param>
        /// <returns>Tile at specified location</returns>
        private Tile FetchTile(Point3d point, string key, string session, string url, double geometricErrorLimit) {
            HttpClient client = _gmapsClient;
            string response = fetchStringFromAPI(client, $"{url}?key={key}&session={session}");
            Tileset tileset = Tileset.Deserialize(response, new Uri(url, UriKind.Relative));
            Tile tile = tileset.Root;
            bool done = false;
            while (!done) {
                // Recursively traverses down the tree of assets to find children at finer levels of detail
                tile = tileset.Root;
                while (!done && tile.Children != null && tile.Children.Count > 0) {
                    // The bounding boxes of multiple children may overlap.
                    // We choose the child tile whose centre is closest to the target point.
                    Tile nextTile = null;
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
                    //   - nextTile.GeometricError < geometricError implying we have found a tile at the sufficient level of detail
                    if (nextTile == null || nextTile.GeometricError < geometricErrorLimit) done = true;
                    if (nextTile != null) tile = nextTile;
                }
                if (!done) {
                    // Make a further API call, to fetch tiles at a finer level of detail
                    url = tile.Contents[0].Uri.ToString();
                    response = fetchStringFromAPI(client, $"{url}?key={key}&session={session}");
                    tileset = Tileset.Deserialize(response, new Uri(url, UriKind.Relative));
                }
                
            }
            return tile;
        }

        /// <summary>
        /// Fetch a GLB file from the API, and imports it into the Rhino window.
        /// </summary>
        /// <param name="doc">Active document</param>
        /// <param name="key">Google Maps API key</param>
        /// <param name="session">Google Maps API session token</param>
        /// <param name="url">Relative path of API (this should be a .glb link)</param>
        private void FetchAndLoadGLB(RhinoDoc doc, string key, string session, string url) {
            byte[] glbBytes = fetchGLBFromAPI(_gmapsClient, $"{url}?key={key}&session={session}");

            string tempPath = Path.Combine(Path.GetTempPath(), "temp.glb");
            File.WriteAllBytes(tempPath, glbBytes);

            // Read GLB from temp file
            bool importSuccess = doc.Import(tempPath);
            // Zooms out so that the rendered tile fits in view.
            doc.Views.ActiveView.ActiveViewport.ZoomExtents();
            RhinoApp.WriteLine(importSuccess ? "Import succeeded." : "Import failed.");
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
             * If you do so, set `valuesNotInitialised` to false so the initial call to Cesium Ion doesn't
             * occur again. Be careful not to commit your API key to the repo.
             */
            string key = "";
            string session = "";
            string url = "";
            bool valuesNotInitialised = true;

            if (valuesNotInitialised) {
                string tok = "";
                using (GetString getStringAction = new GetString()) {  
                    getStringAction.SetCommandPrompt("Type Cesium Ion access token here");
                    getStringAction.GetLiteralString();
                    tok = getStringAction.StringResult();
                    RhinoApp.WriteLine("Key received: {0}", tok);
                }
                key = GetGMapsKeyFromCesium(tok);
                (session, url) = GetGMapsSessionToken(key);
                Console.WriteLine(key);
                Console.WriteLine(session);
                Console.WriteLine(url);
            }

            // Configure location to render
            // TODO: Have the user input this from the GUI.
            double lat = 51.500791;
            double lon = -0.119939;
            double altitude = 60;  // Chosen arbitrarily
            double geometricErrorLimit = 100;  // Chosen arbitrarily, but should fetch a tile at a decent zoom level

            Tile tile = FetchTile(Helper.LatLonToEPSG4978(lat, lon, altitude), key, session, url, geometricErrorLimit);
            url = tile.Contents[0].Uri.ToString();
            FetchAndLoadGLB(doc, key, session, url);
            return Result.Success;
        }
    }
}
