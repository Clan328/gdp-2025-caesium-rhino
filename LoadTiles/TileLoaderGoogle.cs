using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Web;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using TilesData;

namespace LoadTiles
{
    public class TileLoaderGoogle : TileLoader
    {
        private string? rootUrl, key, session;
        private HashSet<string> copyrightSet = new();
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
            // Reset copyright set
            copyrightSet.Clear();
            copyrightSet.Add("Google");
        }

        protected override string FormUrl(string url)
        {
            return $"{url}?key={key}&session={session}";
        }

        private static string? GetCopyrightFromGlb(byte[] glbBytes)
        {
            const uint GLB_MAGIC = 0x46546C67; // ASCII 'glTF'
            const uint CHUNK_TYPE_JSON = 0x4E4F534A; // ASCII 'JSON'

            int offset = 0;

            // Read header
            uint magic = BitConverter.ToUInt32(glbBytes, offset); offset += 4;
            if (magic != GLB_MAGIC)
                throw new InvalidOperationException("Not a valid GLB file.");

            offset += 8; // Skip version and length

            // Read first chunk header
            uint chunkLength = BitConverter.ToUInt32(glbBytes, offset); offset += 4;
            uint chunkType = BitConverter.ToUInt32(glbBytes, offset); offset += 4;

            if (chunkType != CHUNK_TYPE_JSON)
                throw new InvalidOperationException("First chunk is not JSON.");

            // Read JSON chunk
            string jsonText = Encoding.UTF8.GetString(glbBytes, offset, (int)chunkLength);
            using var doc = JsonDocument.Parse(jsonText);

            // Extract asset.copyright
            if (doc.RootElement.TryGetProperty("asset", out var asset) &&
                asset.TryGetProperty("copyright", out var copyright))
            {
                return copyright.GetString();
            }
            return null;
        }

        protected override void OnFetchGLB(byte[] glbBytes)
        {
            // Extract copyright information from the GLB file to be collated
            string? copyright = GetCopyrightFromGlb(glbBytes);
            // Expected copyright format is "Google;Data <...>"
            string expectedPrefix = "Google;Data ";
            if (copyright == null || !copyright.StartsWith(expectedPrefix)) return;
            // Add sources to the copyright set
            string[] sources = copyright.Substring(expectedPrefix.Length).Split(",", 
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (string source in sources) copyrightSet.Add(source);
        }

        protected override void OnTilesLoaded(RhinoDoc doc, List<RhinoObject> newObjects)
        {
            // Display copyright information in a separate overlay
            string sources = string.Join(", ", copyrightSet);
            AttributionConduit.Instance.setAttributionText(
                $"Attributions: {sources}"
            );
        }
    }
}