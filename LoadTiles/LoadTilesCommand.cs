using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Web;
using System;
using System.IO;

namespace LoadTiles
{
    public class LoadTilesCommand : Command
    {
        private readonly HttpClient _cesiumClient, _gmapsClient;
        public LoadTilesCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;

            // Initialise Http Clients
            _cesiumClient = new HttpClient(){
                BaseAddress = new Uri("https://api.cesium.com"),
            };
            _gmapsClient = new HttpClient(){
                BaseAddress = new Uri("https://tile.googleapis.com"),
            };
        }

        ///<summary>The only instance of this command.</summary>
        public static LoadTilesCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "Fetch";

        /// <summary>
        /// Makes an API call to retrieve a JSON object
        /// </summary>
        /// <param name="client">Http client to make the API call</param>
        /// <param name="url">Link to the API</param>
        /// <returns>Newtonsoft JObject representing the JSON returned</returns>
        private JObject fetchJsonFromAPI(HttpClient client, string url) {
            string response = client.GetStringAsync(url).Result;
            return JObject.Parse(response);
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
            JObject response = fetchJsonFromAPI(client, url);
            // Extract link to Google Maps API (which comes with the key)
            string urlWithKey = response["options"]?["url"]?.ToString();
            // Extract key (which is a query parameter) from the URL
            string key = HttpUtility.ParseQueryString(new Uri(urlWithKey).Query)["key"];
            return key;
        }

        /// <summary>
        /// Calls Google Maps API and imports the relevant data into the application.
        /// Yes, this should be refactored into multiple procedures once we've figured out how exactly it should do each task.
        /// </summary>
        /// <param name="doc">The active Rhino document</param>
        /// <param name="key">Google Maps API Key</param>
        private void ImportMapsData(RhinoDoc doc, string key) {
            HttpClient client = _gmapsClient;

            // 1st API call, to the root
            JObject root = fetchJsonFromAPI(client, $"/v1/3dtiles/root.json?key={key}");
            // We dive down several layers straight away - to the only follow-up API link that appears in the call to the root
            // This link included in the JSON response is special - it contains a session token, which must be included in all further API calls (to non-root nodes)
            string nextPathWithSession = root["root"]?["children"]?[0]["children"]?[0]["content"]?["uri"]?.ToString();
            // The following lines simply manipulate the URL to extract both the left part (without the session) and the session token separately
            string urlWithSession = client.BaseAddress + nextPathWithSession;
            Uri uriWithSession = new Uri(urlWithSession);
            string session = HttpUtility.ParseQueryString(uriWithSession.Query)["session"];
            string url = uriWithSession.GetLeftPart(UriPartial.Path);

            /* What follows is just test code to find some sample GLB to import and render
             * TODO: Given lat and long, navigate the data, making further API calls as needed, to find the right GLB tile(s)
             */
            JObject obj = fetchJsonFromAPI(client, $"{url}?key={key}&session={session}");  // Pattern for each subsequent API call (notice the session token has to be included)
            
            // Some segment of the globe (uncomment either this or the block below)
            url = obj["root"]?["children"]?[0]["children"]?[0]["content"]?["uri"]?.ToString();
            byte[] glbBytes = fetchGLBFromAPI(client, $"{url}?key={key}&session={session}");

            // Some other segment of the globe (uncomment either this or the block above)
            // url = obj["root"]?["children"]?[3]["children"]?[0]["children"]?[0]["children"]?[0]["children"]?[0]["content"]?["uri"]?.ToString();
            // obj = fetchJsonFromAPI(client, $"{url}?key={key}&session={session}");
            // url = obj["root"]?["children"]?[0]["children"]?[0]["children"]?[0]["children"]?[0]["content"]?["uri"]?.ToString();
            // byte[] glbBytes = fetchGLBFromAPI(client, $"{url}?key={key}&session={session}");

            // Write the fetched GLB to a temp file
            // TODO: Other ways of loading data without saving to a file?
            // TODO: Or, delete file after session ends?
            string tempPath = Path.Combine(Path.GetTempPath(), "temp.glb");
            File.WriteAllBytes(tempPath, glbBytes);

            // Read GLB from temp file
            bool importSuccess = doc.Import(tempPath);
            RhinoApp.WriteLine(importSuccess ? "Import succeeded." : "Import failed.");
            /* Note: At this point if the data is not showing up in the application even though the import was successful,
             * it is possible the data loaded was too large for the viewport.
             * In the Rhino command line, input this:
             *   _Zoom
             * And then select "Extent".
             */
        }

        /// <summary>
        /// Handles the user running the command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("Fetching...");

            bool getTokenFromInput = true;
            string tok = "";  // For dev purposes, you may put your own token here and change the above boolean to false
            if (getTokenFromInput) {
                using (GetString getStringAction = new GetString()) {  
                    getStringAction.SetCommandPrompt("Type Cesium Ion access token here");
                    getStringAction.GetLiteralString();
                    tok = getStringAction.StringResult();
                    RhinoApp.WriteLine("Key received: {0}", tok);
                }
            }
            
            string mapsKey = GetGMapsKeyFromCesium(tok);
            ImportMapsData(doc, mapsKey);
            return Result.Success;
        }
    }
}
