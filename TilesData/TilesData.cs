namespace TilesData;

using System.Text.Json;
using System.Text.Json.Serialization;
using Rhino.Geometry;

static class Helpers
{
    /// <summary>
    /// Assert some condition. If the condition is false, then raise an ArgumentException
    /// </summary>
    /// <param name="Cond">The condition to require</param>
    /// <param name="Message">The message to include in the exception</param>
    /// <param name="Argument">The argument being inspected</param>
    /// <exception cref="ArgumentException">Raised if the condition fails</exception>
    public static void Require(bool Cond, string Message, string Argument)
    {
        if (!Cond)
        {
            throw new ArgumentException(Message, Argument);
        }
    }

    /// <summary>
    /// Convert a matrix stored in column major order to a Transform
    /// </summary>
    public static Transform ColumnMajor(double[] data)
    {
        return new Transform
        {
            M00 = data[0],
            M10 = data[1],
            M20 = data[2],
            M30 = data[3],
            M01 = data[4],
            M11 = data[5],
            M21 = data[6],
            M31 = data[7],
            M02 = data[8],
            M12 = data[9],
            M22 = data[10],
            M32 = data[11],
            M03 = data[12],
            M13 = data[13],
            M23 = data[14],
            M33 = data[15],
        };
    }

    public static Transform Inverse(Transform transform)
    {
        if (!transform.TryGetInverse(out var inverse))
        {
            throw new ArgumentException("Attempt to invert non-invertible Transform", nameof(transform));
        }
        return inverse;
    }
}

/// <summary>
/// https://github.com/CesiumGS/3d-tiles/tree/main/specification#box <br />
/// A bounding box that is not necessarily aligned to the X, Y or Z axis.
/// This is represented by Rhino3d's Box rather than BoundingBox.
/// </summary>
/// <param name="Center">The center point of the box</param>
/// <param name="X">The x-axis of the box</param>
/// <param name="Y">The y-axis of the box</param>
/// <param name="Z">The z-axis of the box</param>
public record class TileBoundingBox(Point3d Center, Vector3d X, Vector3d Y, Vector3d Z)
{
    [JsonConstructor]
    public TileBoundingBox(double[] values) : this(FromArray(values)) { }

    public static TileBoundingBox FromArray(double[] values)
    {
        Helpers.Require(values.Length == 12, "Expected array of length 12", nameof(values));

        var Center = new Point3d(values[0], values[1], values[2]);
        var X = new Vector3d(values[3], values[4], values[5]);
        var Y = new Vector3d(values[6], values[7], values[8]);
        var Z = new Vector3d(values[9], values[10], values[11]);

        return new TileBoundingBox(Center, X, Y, Z);
    }

    public Box AsBox()
    {
        var plane = new Plane(Center, X, Y);
        var xHalfSize = X.Length; var yHalfSize = Y.Length; var zHalfSize = Z.Length;
        var xSize = new Interval(-xHalfSize, xHalfSize);
        var ySize = new Interval(-yHalfSize, yHalfSize);
        var zSize = new Interval(-zHalfSize, zHalfSize);
        return new Box(plane, xSize, ySize, zSize);
    }
}

/// <summary>
/// A bounding region represented with minimum and maximum longitute and latitude (in radians)
/// and height (metres above WGS 84 Ellipsoid: https://epsg.org/ellipsoid_7030/WGS-84.html)
/// </summary>
public record class BoundingRegion(double West, double South, double East, double North, double MinHeight, double MaxHeight)
{
    [JsonConstructor]
    public BoundingRegion(double[] values) : this(FromArray(values)) { }

    public static BoundingRegion FromArray(double[] values)
    {
        Helpers.Require(values.Length == 6, "Expected array of length 6", nameof(values));

        return new BoundingRegion(values[0], values[1], values[2], values[3], values[4], values[5]);
    }
}

public record class BoundingSphere(Point3d Center, double Radius)
{
    [JsonConstructor]
    public BoundingSphere(double[] values) : this(FromArray(values)) { }

    public static BoundingSphere FromArray(double[] values)
    {
        Helpers.Require(values.Length == 4, "Expected array of length 4", nameof(values));

        var Center = new Point3d(values[0], values[1], values[2]);
        var Radius = values[3];

        return new BoundingSphere(Center, Radius);
    }

    public Sphere AsSphere()
    {
        return new Sphere(Center, Radius);
    }
}

/// <summary>
/// The bounding volumne for a tile. All geometry in the tile is contained in all non-null bounding shapes.
/// At least 1 bounding shape must be defined. To ensure at least one bounding shape is defined, you should use BoundingVolumne.NewChecked
/// </summary>
public record class BoundingVolume(TileBoundingBox? Box, BoundingRegion? Region, BoundingSphere? Sphere)
{
    /// <summary>
    /// Check at least one bounding shape is set
    /// </summary>
    public bool IsSet
    {
        get
        {
            return Box is not null || Region is not null || Sphere is not null;
        }
    }

    public static BoundingVolume From(TileBoundingBox Box)
    {
        return new BoundingVolume(Box, null, null);
    }
    public static BoundingVolume From(BoundingRegion Region)
    {
        return new BoundingVolume(null, Region, null);
    }
    public static BoundingVolume From(BoundingSphere Sphere)
    {
        return new BoundingVolume(null, null, Sphere);
    }
}

// TODO: support properties
/// <summary>
/// https://github.com/CesiumGS/3d-tiles/blob/main/specification/schema/metadataEntity.schema.json <br />
/// Metadata about a Tile or Content.
/// </summary>
/// <param name="Class">The name of the class</param>
public record class Metadata(string Class);

/// <summary>
/// https://github.com/CesiumGS/3d-tiles/tree/main/specification#core-refinement
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Refine
{
    /// <summary>
    /// Add geometry from children and keep the parent's geometry
    /// </summary>
    ADD,

    /// <summary>
    /// Replace the parent's geometry with the children's
    /// </summary>
    REPLACE,
}

public record class Content(BoundingVolume? BoundingVolume, Uri Uri, uint? Group);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubdivisionScheme
{
    QUADTREE,
    OCTREE,
}

// TODO: implement implicit tiling
/// <summary>
/// A tile that might contain geometry and/or sub-tiles
/// </summary>
/// <param name="BoundingVolume">The bounds of this tile - all geometry for this tile
/// and all sub-tiles is contained in this</param>
/// <param name="ViewerRequestVolume">Geometry in this tile or sub-tiles should only be
/// rendered when the camera is within this volume</param>
/// <param name="GeometricError">The error in metres of this tiles simplified geometry</param>
/// <param name="Refine">The scheme used when refining a tile with its more detailed children</param>
/// <param name="Transform">The transform of the content and bounding boxes of this tile, relative to parent</param>
/// <param name="Contents">The geometry stored in this tile. This may be empty.</param>
/// <param name="Children">The children of this tile. These can generally be used to improve the level of detail.</param>
public record class Tile(
    BoundingVolume BoundingVolume,
    BoundingVolume? ViewerRequestVolume,
    double GeometricError,
    Refine? Refine,
    Transform Transform,
    Content[] Contents,
    Tile[] Children
)
{
    [JsonConstructor]
    public Tile(
        BoundingVolume boundingVolume,
        BoundingVolume? viewerRequestVolume,
        double geometricError,
        Refine? refine,
        Transform? transform,
        Content? content,
        Content[]? contents,
        Tile[]? children
    )
        : this(
            boundingVolume,
            viewerRequestVolume,
            geometricError,
            refine,
            transform ?? Transform.Identity,
            content is null ? (contents ?? Array.Empty<Content>()) : new Content[] { content },
            children ?? Array.Empty<Tile>()
        )
    {
        Helpers.Require(geometricError >= 0, "geometricError must be non-negative", nameof(geometricError));
    }
}

public record class Tileset(
    double GeometricError,
    Tile Root
)
{
    /// <summary>
    /// Default JsonSerializerOptions for deserialising Tiles3D json.
    /// </summary>
    public readonly static JsonSerializerOptions JsonSerializerOptions
        = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };



    public static Tileset? FromJson(string data)
    {
        throw new NotImplementedException();
    }
};

/// <summary>
/// Class which provides context for processing tiles.
/// </summary>
/// <param name="BaseUri">The base URI, used to resolve relative URLs</param>
/// <param name="Transform">The current transform from this tiles local space to global space</param>
/// <param name="InverseTransform">The inverse of Transform</param>
public record class TileContext(
    Uri BaseUri,
    Transform Transform,
    Transform InverseTransform
)
{
    // TODO: Find out what this should be
    // TODO: should this be set based on model location
    /// <summary>
    /// The transform to convert from 3D Tiles space to Rhino3d space.
    /// </summary>
    internal static Transform BaseTransform = Transform.Identity;

    public TileContext(Uri BaseUri) : this(BaseUri, BaseTransform, Helpers.Inverse(BaseTransform)) { }

    /// <summary>
    /// Create a new context for a tile.
    /// </summary>
    public TileContext EnterTile(Tile tile)
    {
        return new TileContext(BaseUri, Transform * tile.Transform, Helpers.Inverse(tile.Transform) * InverseTransform);
    }

    private double ScreenSpaceError(double geometricError, BoundingVolume boundingVolume, uint screenHeight, double fovy, Point3d camera)
    {
        camera = InverseTransform * camera;

        double tileDistance;
        if (boundingVolume.Sphere is not null)
        {
            var centerDist = boundingVolume.Sphere.Center.DistanceTo(camera);
            tileDistance = Math.Max(centerDist - boundingVolume.Sphere.Radius, 0);
        }
        else if (boundingVolume.Box is not null)
        {
            var closest = boundingVolume.Box.AsBox().ClosestPoint(camera);
            tileDistance = closest.DistanceTo(camera);
        }
        else
        {
            // TODO: make this work
            throw new NotImplementedException("BoundingRegion support has not been added yet");
        }

        return geometricError * screenHeight / (tileDistance * 2 * Math.Tan(fovy / 2));

    }

    /// <summary>
    /// Compute the screen space error for this tile.
    /// </summary>
    /// <param name="screenHeight">The height of the screen in pixels</param>
    /// <param name="fovy">The field-of-view angle in radians in the y direction</param>
    /// <param name="camera">The location of the camera</param>
    /// <returns></returns>
    public double ScreenSpaceError(Tile tile, uint screenHeight, double fovy, Point3d camera)
    {
        return ScreenSpaceError(tile.GeometricError, tile.BoundingVolume, screenHeight, fovy, camera);
    }

    public double ScreenSpaceError(Tileset tileset, uint screenHeight, double fovy, Point3d camera)
    {
        return ScreenSpaceError(tileset.GeometricError, tileset.Root.BoundingVolume, screenHeight, fovy, camera);
    }
}
