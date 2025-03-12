using System.Text.Json;
using System.Text.Json.Serialization;
using Rhino.Geometry;

namespace TilesData;

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
    /// Create a list containing a single item.
    /// </summary>
    public static List<T> Singleton<T>(T item)
    {
        return new List<T>(1)
        {
            item
        };
    }

    /// <summary>
    /// Convert a matrix stored in column major order to a Transform
    /// </summary>
    public static Transform ColumnMajor(List<double> data)
    {
        Require(data.Count == 16, "Expected list of length 16", nameof(data));

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
    public static TileBoundingBox FromArray(List<double> values)
    {
        Helpers.Require(values.Count == 12, "Expected list of length 12", nameof(values));

        var Center = new Point3d(values[0], values[1], values[2]);
        var X = new Vector3d(values[3], values[4], values[5]);
        var Y = new Vector3d(values[6], values[7], values[8]);
        var Z = new Vector3d(values[9], values[10], values[11]);

        return new TileBoundingBox(Center, X, Y, Z);
    }

    public static bool IsInBox(TileBoundingBox box, Point3d point) {
        return box.AsBox().Contains(point);
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
    public static BoundingRegion FromArray(List<double> values)
    {
        Helpers.Require(values.Count == 6, "Expected list of length 6", nameof(values));

        return new BoundingRegion(values[0], values[1], values[2], values[3], values[4], values[5]);
    }
}

public record class BoundingSphere(Point3d Center, double Radius)
{
    public static BoundingSphere FromArray(List<double> values)
    {
        Helpers.Require(values.Count == 4, "Expected list of length 4", nameof(values));

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
/// The bounding volumne for a tile or content.
///
/// For tiles, all geometry in the tile or its descendents is contained in all non-null bounding shapes.
/// For content, this only contains the geometry of this content - this can be used to cull of-screen content.
/// </summary>
public record class BoundingVolume(TileBoundingBox? Box, BoundingRegion? Region, BoundingSphere? Sphere)
{
    public BoundingVolume(Json.BoundingVolume? volume) : this(
        volume?.Box is null ? null : TileBoundingBox.FromArray(volume.Box),
        volume?.Region is null ? null : BoundingRegion.FromArray(volume.Region),
        volume?.Sphere is null ? null : BoundingSphere.FromArray(volume.Sphere)
    )
    { }

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

    public BoundingVolume(TileBoundingBox Box) : this(Box, null, null) { }
    public BoundingVolume(BoundingRegion Region) : this(null, Region, null) { }
    public BoundingVolume(BoundingSphere Sphere) : this(null, null, Sphere) { }
}

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

/// <summary>
/// Content of a tile.
/// </summary>
/// <param name="BoundingVolume">This is never null. Check if this is missing using the BoundingVolume.IsSet method.</param>
/// <param name="Uri"></param>
/// <param name="Group"></param>
public record class Content(
    BoundingVolume BoundingVolume,
    Uri Uri,
    uint Group = 0
)
{
    // TODO: resolve uri
    public Content(Json.Content content) : this(
        new BoundingVolume(content.BoundingVolume),
        content.Uri,
        content.Group ?? 0
    )
    { }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubdivisionScheme
{
    QUADTREE,
    OCTREE,
}

public record class TileParseContext(
    Refine Refine,
    Transform Transform,
    Uri BaseUri
);

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
/// <param name="LocalTransform">The transform of the content and bounding boxes of this tile, relative to parent</param>
/// <param name="GlobalTransform">The transform of the content and bounding boxes relative to the tileset</param>
/// <param name="Contents">The geometry stored in this tile. This may be empty.</param>
/// <param name="Children">The children of this tile. These can generally be used to improve the level of detail.</param>
public record class Tile(
    BoundingVolume BoundingVolume,
    BoundingVolume? ViewerRequestVolume,
    double GeometricError,
    Refine Refine,
    Transform LocalTransform,
    Transform GlobalTransform,
    List<Content> Contents,
    List<Tile> Children
)
{
    public static Tile FromJson(Json.Tile tile, TileParseContext ctx)
    {
        var LocalTransform = tile.Transform is null ? Transform.Identity : Helpers.ColumnMajor(tile.Transform);
        var GlobalTransform = ctx.Transform * LocalTransform;
        var Refine = tile.Refine ?? ctx.Refine;

        List<Content> Contents;
        if (tile.Contents is not null)
        {
            Contents = tile.Contents.ConvertAll(content => new Content(content));
        }
        else if (tile.Content is not null)
        {
            Contents = Helpers.Singleton(new Content(tile.Content));
        }
        else
        {
            Contents = new List<Content>();
        }

        var ctx1 = new TileParseContext(Refine, GlobalTransform, ctx.BaseUri);

        var Children = tile.Children is null
            ? new List<Tile>()
            : tile.Children.ConvertAll(child => FromJson(child, ctx1));

        return new Tile(
            new BoundingVolume(tile.BoundingVolume),
            new BoundingVolume(tile.ViewerRequestVolume),
            tile.GeometricError,
            tile.Refine ?? ctx.Refine,
            LocalTransform,
            GlobalTransform,
            Contents,
            Children
        );
    }
}

public record class Tileset(
    double GeometricError,
    Tile Root
)
{
    public static Tileset FromJson(Json.Tileset tileset, Uri uri)
    {
        var refine = tileset.Root.Refine ?? Refine.ADD;
        var ctx = new TileParseContext(refine, Transform.Identity, uri);
        var root = Tile.FromJson(tileset.Root, ctx);

        return new Tileset(tileset.GeometricError, root);
    }

    public static Tileset Deserialize(ReadOnlySpan<byte> data, Uri uri)
    {
        var raw = Json.Tileset.FromJson(data)
            ?? throw new JsonException("JSON deserialisation returned null");
        return FromJson(raw, uri);
    }

    public static Tileset Deserialize(string data, Uri uri)
    {
        var raw = Json.Tileset.FromJson(data)
            ?? throw new JsonException("JSON deserialisation returned null");
        return FromJson(raw, uri);
    }
}


/*
TODO: port these to new representation

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
*/
