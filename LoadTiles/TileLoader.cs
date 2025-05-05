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
using Rhino.Geometry.Intersect;
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

        // Functions that individual TileLoaders could implement
        protected virtual void OnFetchGLB(byte[] glbBytes) {}
        protected virtual void OnTilesLoaded(RhinoDoc doc, List<RhinoObject> newObjects) {}

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
            OnFetchGLB(glbBytes); // Process the GLB file (e.g. extract copyright information)
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
            
            OnTilesLoaded(doc, newObjects); // Possibly do something once tiles have been loaded

            // Delete the temporary file
            string tempPath = Path.Combine(Path.GetTempPath(), "temp.glb");
            if (File.Exists(tempPath)) {
                try {
                    File.Delete(tempPath);
                } catch (IOException e) {
                    Console.WriteLine($"Error deleting temporary file: {e.Message}");
                }
            }

            return newObjects;
        }

        /// <summary>
        /// Translates the loaded tiles to the origin in Rhino3d.
        /// </summary>
        /// <param name="doc">Active document</param>
        /// <param name="objects">Imported objects</param>
        /// <param name="targetPoint">Point to translate to origin</param>
        /// <param name="lat">Corresponding latitude of targetPoint</param>
        /// <param name="lon">Corresponding longitude of targetPoint</param>
        /// <param name="bringToOrigin">Whether to ignore specified altitude and shift objects along Z axis to origin</param>
        /// <returns>List of GUIDs of the transformed objects</returns>
        public static List<Guid> TranslateLoadedTiles(RhinoDoc doc, IEnumerable<RhinoObject> objects, Point3d targetPoint, double lat, double lon, bool bringToOrigin) {
            double scaleFactor = RhinoMath.UnitScale(UnitSystem.Meters, RhinoDoc.ActiveDoc.ModelUnitSystem);
            targetPoint *= scaleFactor; // Convert the target point to the model units of the active document.
            
            // Calculate the three orthogonal vectors for rotation
            Vector3d up = new(targetPoint);
            up.Unitize();
            Point3d pointToNorth = lat <= 89 ?
                Helper.LatLonToEPSG4978(lat + 1, lon, 0) :
                Helper.LatLonToEPSG4978(179 - lat, lon + 180, 0);
            Vector3d pointToNorthVec = new Vector3d(pointToNorth);
            Vector3d north = pointToNorthVec - up * pointToNorthVec * up;
            north.Unitize();
            Vector3d east = Vector3d.CrossProduct(north, up);

            if (bringToOrigin)
            {
                Ray3d upFromOrigin = new Ray3d(new Point3d(0, 0, 0), up);
                // Find the first intersection point from the origin to the geometry
                // This determines which point to bring to the origin
                List<GeometryBase> geometries = new();
                foreach (RhinoObject obj in objects) {
                    GeometryBase geometry = obj.Geometry;
                    // Brep.CreateFromMesh is very costly, hence we check which objects
                    //   have their bounding box (which is cheap to calculate) intersecting with the ray.
                    if (geometry is Mesh mesh) {
                        // Rhino.Intersect only intersects rays with meshes, so we convert the bounding box to a mesh
                        Mesh boundingMesh = Mesh.CreateFromBox(mesh.GetBoundingBox(false), 1, 1, 1);
                        if (Intersection.MeshRay(boundingMesh, upFromOrigin) >= 0) {
                            geometries.Add(Brep.CreateFromMesh(mesh, false));
                        } 
                    }
                }
                Point3d[] intersectionPoints = Intersection.RayShoot(upFromOrigin, geometries, 1);
                if (intersectionPoints.Length > 0) targetPoint = intersectionPoints[0];
                else RhinoApp.WriteLine("WARN: No intersection found with the ray from the origin to the geometry. Using the original target point.");
            }

            // Calculates transformation needed to move to Rhino3d's origin
            Transform toOrigin = Transform.Translation(-targetPoint.X, -targetPoint.Y, -targetPoint.Z);
            Transform rotate = Transform.Rotation(east, north, up, new Vector3d(1,0,0), new Vector3d(0,1,0), new Vector3d(0,0,1));
            Transform moveRot = rotate * toOrigin;
            
            // Apply the transformation to all loaded objects
            List<Guid> transformedObjects = new List<Guid>();
            foreach (RhinoObject obj in objects) {
                Guid guid = doc.Objects.Transform(obj, moveRot, true);
                transformedObjects.Add(guid);
            }
            return transformedObjects;
        }
    }
}