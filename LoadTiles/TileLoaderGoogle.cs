using System;
using System.Text.Json;
using System.Web;
using Rhino.Geometry;
using TilesData;

namespace LoadTiles
{
    public class TileLoaderGoogle : TileLoader
    {
        private string? rootUrl, key, session;
        protected override string RootUrl => rootUrl ?? throw new InvalidOperationException("Root URL not set.");
        protected override string AssetId => "2275207";
        public TileLoaderGoogle()
        {
        }

        protected override void InitApiParameters(string cesiumIonApiKey)
        {
            // Fetch endpoint data from Cesium Ion API
            using JsonDocument doc = CallIonEndpoint(cesiumIonApiKey);
            // Extract link to Google Maps API (which comes with the key)
            string urlWithKey = doc.RootElement
                                   .GetProperty("options")
                                   .GetProperty("url")
                                   .ToString();
            // Extract key (which is a query parameter) from the URL
            key = HttpUtility.ParseQueryString(new Uri(urlWithKey).Query)["key"] ?? throw new InvalidOperationException("Key not found.");
            string baseUrl = urlWithKey.Split("?")[0];
            // We make a call to the Google Maps API to get the session ID
            string response = FetchStringFromAPI(client, $"{baseUrl}?key={key}");
            Tileset tileset = Tileset.Deserialize(response, new Uri(baseUrl, UriKind.Absolute), Transform.Identity);
            // We dive down several layers straight away - to the only follow-up API link that appears in the call to the root
            // This link included in the JSON response is special - it contains a session token, which must be included in all further API calls (to non-root nodes)
            string nextPathWithSession = tileset.Root.Children[0].Children[0].Contents[0].Uri.ToString();
            // Manipulate the URL to extract the session token
            var parts = nextPathWithSession.Split("?");
            rootUrl = parts[0];
            var queryParams = HttpUtility.ParseQueryString(parts.Length > 1 ? parts[1] : "");
            session = queryParams["session"] ?? throw new InvalidOperationException("Session token not found.");
        }

        protected override string FormUrl(string url)
        {
            return $"{url}?key={key}&session={session}";
        }
    }
}