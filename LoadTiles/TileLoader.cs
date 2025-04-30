using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using TilesData;

namespace LoadTiles
{
    public abstract class TileLoader
    {
        // For Cesium Ion 
        private readonly HttpClient cesiumIonClient = new HttpClient 
        { 
            BaseAddress = new Uri("https://api.cesium.com") 
        };
        
        // For the chosen 3D Tiles API
        protected readonly HttpClient client = new HttpClient 
        {            
        };

        /// <summary>
        /// Root URL for the 3D Tiles API, to be called initially for each load.
        /// </summary>
        protected abstract string RootUrl { get; }

        /// <summary>
        /// Cesium Ion asset ID for the specified 3D Tiles API.
        /// </summary>
        protected abstract string AssetId { get; }

        /// <summary>
        /// Initialises the API parameters (URL to root, API key, session token etc.) for the 3D Tiles API.
        /// </summary>
        protected abstract void InitApiParameters(string cesiumIonApiKey);

        protected TileLoader()
        {
        }

        /// <summary>
        /// Makes an API call to retrieve a byte file
        /// </summary>
        /// <param name="client">Http client to make the API call</param>
        /// <param name="url">Link to the API</param>
        /// <returns>Byte array representing the file</returns>
        protected static byte[] FetchFileFromAPI(HttpClient client, string url) 
        {
            HttpResponseMessage response = client.GetAsync(url).Result;
            if (response.Content.Headers.ContentEncoding.Contains("gzip"))
            {   // Decompress the response if it is gzipped
                using var decompressedStream = new GZipStream(response.Content.ReadAsStreamAsync().Result, CompressionMode.Decompress);
                using var memoryStream = new MemoryStream();
                decompressedStream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
            else return response.Content.ReadAsByteArrayAsync().Result;
        }

        /// <summary>
        /// Makes an API call to retrieve a string
        /// </summary>
        /// <param name="client">Http client to make the API call</param>
        /// <param name="url">Link to the API</param>
        /// <returns>Response from the API as a string</returns>
        protected static string FetchStringFromAPI(HttpClient client, string url) 
        {
            HttpResponseMessage response = client.GetAsync(url).Result;
            if (response.Content.Headers.ContentEncoding.Contains("gzip"))
            {   // Decompress the response if it is gzipped
                using var decompressedStream = new GZipStream(response.Content.ReadAsStreamAsync().Result, CompressionMode.Decompress);
                using var reader = new StreamReader(decompressedStream);
                return reader.ReadToEnd();
            }
            else return response.Content.ReadAsStringAsync().Result;
        }

        /// <summary>
        /// Makes an API call to Cesium Ion to get the endpoint for a specific asset
        /// </summary>
        protected JsonDocument CallIonEndpoint(string apiKey) 
        {
            // Set the authorization header with the token
            cesiumIonClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            
            // Make the API call to get the endpoint
            string url = $"/v1/assets/{AssetId}/endpoint";
            string response = FetchStringFromAPI(cesiumIonClient, url);
            
            // Parse the response to JSON format
            return JsonDocument.Parse(response);
        }

        /// <summary>
        /// Forms the URL for any API call to the 3D Tiles API.
        /// <br/>
        /// Some APIs (e.g. Google Maps) append the key and session ID to the URL.
        /// </summary>
        protected virtual string FormUrl(string url) 
        {
            return url;
        }

        /// <summary>
        /// Loads and imports the GLB associated with the given tile.
        /// </summary>
        /// <param name="doc">Active document</param>
        /// <param name="tile">Tile to load</param>
        private bool LoadGLB(RhinoDoc doc, Tile tile, Point3d targetPoint) 
        {
            string glbLink = tile.Contents[0].Uri.ToString();
            // Check that the link is indeed a GLB
            string[] linkparts = glbLink.Split('.');
            if (linkparts[linkparts.Length - 1] != "glb") throw new InvalidOperationException("Link is not a GLB file.");
            // Load the GLB file from the API
            byte[] glbBytes = FetchFileFromAPI(client, FormUrl(glbLink));
            // Store the GLB file in a temporary location
            // This is necessary because the Rhino GLTF reader does not support loading from a byte array
            string tempPath = Path.Combine(Path.GetTempPath(), "temp.glb");
            File.WriteAllBytes(tempPath, glbBytes);
            Console.WriteLine(Helper.PointDistanceToBoundingVolume(targetPoint, tile.BoundingVolume));
            // Read GLB from the temporary file and import it into the Rhino document
            return doc.Import(tempPath);
        }

        private bool LoadB3DM(RhinoDoc doc, Tile tile, Point3d targetPoint) 
        {
            string b3dmLink = tile.Contents[0].Uri.ToString();
            // Check that the link is indeed a B3DM
            string[] linkparts = b3dmLink.Split('.');
            if (linkparts[linkparts.Length - 1] != "b3dm") throw new InvalidOperationException("Link is not a B3DM file.");
            // Load the B3DM file from the API
            byte[] b3dmBytes = FetchFileFromAPI(client, FormUrl(b3dmLink));

            // Extract the GLB data from the B3DM file:
            // 0. Initial size checks
            const long HEADER_SIZE = 28; // B3DM header size
            if (b3dmBytes == null) throw new InvalidDataException("B3DM file is empty.");
            if (b3dmBytes.Length < HEADER_SIZE) throw new InvalidDataException("B3DM file is missing header.");

            using MemoryStream ms = new MemoryStream(b3dmBytes);
            using BinaryReader reader = new BinaryReader(ms, Encoding.ASCII, false);
            // 1. Read Magic ('b3dm')
            char[] magic = reader.ReadChars(4);
            string magicStr = new string(magic);
            if (magicStr != "b3dm") throw new InvalidDataException($"Invalid B3DM magic string. Expected 'b3dm', got '{magicStr}'.");

            // 2. Read Version
            uint version = reader.ReadUInt32();
            if (version != 1) Console.WriteLine($"Warning: B3DM version is {version}, expected 1. Parsing proceeds assuming version 1 layout.");

            // 3. Read Total Byte Length
            uint totalByteLength = reader.ReadUInt32();
            if (totalByteLength != b3dmBytes.Length) throw new InvalidDataException($"B3DM header byteLength ({totalByteLength}) does not match input data length ({b3dmBytes.Length}).");

            // 4. Read Table Lengths
            uint featureTableJsonByteLength = reader.ReadUInt32();
            uint featureTableBinaryByteLength = reader.ReadUInt32();
            uint batchTableJsonByteLength = reader.ReadUInt32();
            uint batchTableBinaryByteLength = reader.ReadUInt32();

            // 5. Calculate GLB/glTF Start Position
            // It starts immediately after the header and all the tables.
            long glbStartPosition = HEADER_SIZE +
                                    featureTableJsonByteLength +
                                    featureTableBinaryByteLength +
                                    batchTableJsonByteLength +
                                    batchTableBinaryByteLength;
            if (glbStartPosition > b3dmBytes.Length) throw new InvalidDataException("Calculated GLB start position exceeds B3DM total length based on table lengths.");

            // 6. Calculate GLB/glTF Length
            long glbLength = totalByteLength - glbStartPosition;
            if (glbLength == 0) return false; // No GLB data to extract

            // 7. Extract the GLB/glTF data
            byte[] glbBytes = new byte[glbLength];
            Array.Copy(b3dmBytes, glbStartPosition, glbBytes, 0, glbLength);

            // Store the GLB file in a temporary location
            string tempPath = Path.Combine(Path.GetTempPath(), "temp.glb");
            File.WriteAllBytes(tempPath, glbBytes);
            Console.WriteLine(Helper.PointDistanceToBoundingVolume(targetPoint, tile.BoundingVolume));
            // Read GLB from the temporary file and import it into the Rhino document
            return doc.Import(tempPath);
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
        private static bool IsSufficientlyDetailed(Tile tile, Point3d targetPoint) 
        {
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
        private void TraverseTileTree(RhinoDoc doc, Tile tile, Point3d targetPoint, double renderDistance) 
        {
            bool tileHasContent = tile.Contents.Count > 0;

            List<Tile> childrenWithinRenderDistance = tile.Children.FindAll(child => Helper.PointDistanceToTile(targetPoint, child) <= renderDistance);
            bool hasChildrenWithinDistance = childrenWithinRenderDistance.Count > 0;

            bool isSufficientlyDetailed = IsSufficientlyDetailed(tile, targetPoint);

            // Should the tile itself be loaded?
            bool shouldLoadTile = tileHasContent
                && (isSufficientlyDetailed || tile.Refine == Refine.ADD || !hasChildrenWithinDistance);

            // Should the objects of the tile be loaded, if it exists? This weeds out tiles that are too far away,
            //   by checking if the tile itself is 'close' to the point but none of its children are
            bool shouldLoadObjects = shouldLoadTile
                && !(tile.Children.Count > 0 && !hasChildrenWithinDistance);

            // Should the tile be traversed further?
            bool shouldTraverse = hasChildrenWithinDistance && !isSufficientlyDetailed;

            if (shouldLoadTile) {
                string link = tile.Contents[0].Uri.ToString();
                string[] linkparts = link.Split('.');
                string filetype = linkparts[linkparts.Length - 1];
                if (filetype == "glb") 
                {   // Content of this tile is a GLB file
                    if (shouldLoadObjects) LoadGLB(doc, tile, targetPoint);
                }
                else if (filetype == "b3dm")
                {   // Content of this tile is a B3DM file
                    if (shouldLoadObjects) LoadB3DM(doc, tile, targetPoint);
                }
                else if (filetype == "json") 
                {   // Content of this tile is a JSON link (make a further API call)
                    string response = FetchStringFromAPI(client, FormUrl(link));
                    Tile nextTile = Tileset.Deserialize(response, new Uri(link, UriKind.Absolute), Transform.Identity).Root;
                    TraverseTileTree(doc, nextTile, targetPoint, renderDistance);
                }
                else
                {
                    throw new NotImplementedException($"File type {filetype} not supported.");
                }
            }
            if (shouldTraverse) {
                foreach (Tile childTile in childrenWithinRenderDistance) {
                    TraverseTileTree(doc, childTile, targetPoint, renderDistance);
                }
            }
        }

        /// <summary>
        /// Loads tiles from the 3D tile API, in a radius around the specified location.
        /// </summary>
        /// <param name="doc">Active document</param>
        /// <param name="existingObjects">List of existing objects in the document</param>
        /// <param name="targetPoint">Point3d object corresponding to specified location</param>
        /// <param name="renderDistance">Radius (metres) around the point to load. Formally this loads all tiles 
        /// whose bounding box's closest edge to the point is within this distance.</param>
        /// <param name="cesiumIonApiKey">API key for Cesium Ion</param>
        /// <returns>List of Rhino objects that were loaded</returns>
        public List<RhinoObject> LoadTiles(RhinoDoc doc, List<Guid> existingObjects, Point3d targetPoint, double renderDistance, string cesiumIonApiKey) 
        {
            RhinoApp.WriteLine("Loading tiles...");

            // Call the API to get the root tile
            InitApiParameters(cesiumIonApiKey);
            string response = FetchStringFromAPI(client, FormUrl(RootUrl));
            Tileset tileset = Tileset.Deserialize(response, new Uri(RootUrl, UriKind.Absolute), Transform.Identity);
            Tile tile = tileset.Root;

            // Recursively load tiles from API
            TraverseTileTree(doc, tile, targetPoint, renderDistance);

            // Finds all imported objects
            List<RhinoObject> newObjects = new();
            foreach (RhinoObject obj in doc.Objects) {
                if (!existingObjects.Contains(obj.Id)) {
                    newObjects.Add(obj);
                }
            }
            return newObjects;
        }
    }
}