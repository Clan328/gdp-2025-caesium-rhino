using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Rhino.Geometry;
using TilesData.Json;

namespace TilesData;

public static class Helpers
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
        if (!Cond) throw new ArgumentException(Message, Argument);
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

    /// <summary>
    /// Calculate screen space error.
    /// </summary>
    /// <param name="geometricError">The geometric error of the tile/tileset.</param>
    /// <param name="screenHeight">The height of the screen in pixel.</param>
    /// <param name="tileDistance">The distance from the camera to the tile. Must be non-zero.</param>
    /// <param name="fovy">The field of view in the y-direction in radians. Must be non-zero.</param>
    /// <returns></returns>
    public static double ScreenSpaceError(
        double geometricError, double screenHeight, double tileDistance, double fovy
    )
    {
        return geometricError * screenHeight / (tileDistance * 2 * Math.Tan(fovy / 2));
    }
}

/// <summary>
/// A decoded `data:` URI. Exactly one of `String` and `Bytes` will be set
/// </summary>
/// <param name="Uri"></param>
/// <param name="MediaType"></param>
/// <param name="String">The decoded value, if it is a string</param>
/// <param name="Bytes">The decoded value, if it is non-string data</param>
public partial record class DataUri(
    Uri Uri,
    string? MediaType,
    string? String,
    byte[]? Bytes
)
{
    public static DataUri? Create(Uri uri)
    {
        if (uri.Scheme != "data") return null;

        Match match = DataUriRegex().Match(uri.PathAndQuery)
            ?? throw new ArgumentException("Malformed data URI");

        var mediaType = match.Groups["MediaType"]?.ToString();
        var isBase64 = match.Groups["base64"].ToString() != "";
        var body = match.Groups["body"]?.ToString()
            ?? throw new ArgumentException("Malformed data URI");

        if (isBase64)
        {
            var bytes = Convert.FromBase64String(body);
            return new DataUri(
                uri,
                mediaType,
                String: null,
                Bytes: bytes
            );
        }
        else
        {
            return new DataUri(
                uri,
                mediaType,
                String: Uri.UnescapeDataString(body),
                Bytes: null
            );
        }
    }

    public static DataUri? Create(string uri)
    {
        return Create(new Uri(uri));
    }

    [GeneratedRegex(@"^(?<MediaType>[^;,]+(;[^;,]*=[^;,]*)*)?(?<base64>;base64)?,(?<body>.*)$")]
    private static partial Regex DataUriRegex();
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
    public static TileBoundingBox FromArray(List<double> values, Transform transform)
    {
        Helpers.Require(values.Count == 12, "Expected list of length 12", nameof(values));

        var Center = transform * new Point3d(values[0], values[1], values[2]);
        var X = transform * new Vector3d(values[3], values[4], values[5]);
        var Y = transform * new Vector3d(values[6], values[7], values[8]);
        var Z = transform * new Vector3d(values[9], values[10], values[11]);

        return new TileBoundingBox(Center, X, Y, Z);
    }

    public static bool IsInBox(TileBoundingBox box, Point3d point)
    {
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
    public static BoundingSphere FromArray(List<double> values, Transform transform)
    {
        Helpers.Require(values.Count == 4, "Expected list of length 4", nameof(values));

        var Center = transform * new Point3d(values[0], values[1], values[2]);
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
    public BoundingVolume(Json.BoundingVolume? volume, Transform transform) : this(
        volume?.Box is null ? null : TileBoundingBox.FromArray(volume.Box, transform),
        volume?.Region is null ? null : BoundingRegion.FromArray(volume.Region),
        volume?.Sphere is null ? null : BoundingSphere.FromArray(volume.Sphere, transform)
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
/// <param name="Transform">The transform to the coordinate space given by the
/// user (see Tileset.Deserialize) from the coordinate space of this content.
/// GLTF and 3d Tiles use different coordinate systems, so this should
/// be transformed before applying to the GLTF tile.</param>
public record class Content(
    BoundingVolume BoundingVolume,
    Uri Uri,
    MetadataEntity? Group,
    Transform Transform
)
{
    public static Content FromJson(Json.Content content, TileParseContext ctx)
    {
        var uri = new Uri(ctx.BaseUri, content.Uri);
        var groupId = content.Group ?? 0;
        var group = groupId < ctx.Groups.Count
            ? ctx.Groups[(int)groupId]
            : null;

        return new Content(
            new BoundingVolume(content.BoundingVolume, ctx.Transform),
            uri,
            group,
            ctx.Transform
        );
    }
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
    Uri BaseUri,
    List<MetadataEntity> Groups
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

        var ctx1 = new TileParseContext(Refine, GlobalTransform, ctx.BaseUri, ctx.Groups);

        List<Content> Contents;
        if (tile.Contents is not null)
        {
            Contents = tile.Contents.ConvertAll(content => Content.FromJson(content, ctx));
        }
        else if (tile.Content is not null)
        {
            Contents = Helpers.Singleton(Content.FromJson(tile.Content, ctx));
        }
        else
        {
            Contents = new List<Content>();
        }

        var Children = tile.Children is null
            ? new List<Tile>()
            : tile.Children.ConvertAll(child => FromJson(child, ctx1));

        return new Tile(
            new BoundingVolume(tile.BoundingVolume, GlobalTransform),
            new BoundingVolume(tile.ViewerRequestVolume, GlobalTransform),
            tile.GeometricError,
            tile.Refine ?? ctx.Refine,
            LocalTransform,
            GlobalTransform,
            Contents,
            Children
        );
    }
}

/// <summary>
/// A Tileset, containing a `Tile`s which may reference external `Tileset`s.
/// </summary>
/// <param name="Asset"></param>
/// <param name="GeometricError"></param>
/// <param name="Root"></param>
/// <param name="Groups"></param>
public record class Tileset(
    Asset Asset,
    double GeometricError,
    Tile Root,
    List<MetadataEntity> Groups
)
{
    static Tileset FromJson(Json.Tileset tileset, Uri uri, Transform transform)
    {
        var refine = tileset.Root.Refine ?? Refine.ADD;
        var groups = tileset.Groups ?? new List<MetadataEntity>();
        var ctx = new TileParseContext(refine, transform, uri, groups);
        var root = Tile.FromJson(tileset.Root, ctx);

        return new Tileset(
            tileset.Asset,
            tileset.GeometricError,
            root,
            groups
        );
    }

    /// <summary>
    /// Deserialise a Tileset.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="uri"></param>
    /// <param name="transform">The affine transformation applied to all bounding boxes in this
    /// Tileset. This can be used to convert from 3d Tiles coordinate space (right-handed Z-up) to
    /// Rhino3d's.</param>
    /// <returns></returns>
    /// <exception cref="JsonException"></exception>
    public static Tileset Deserialize(ReadOnlySpan<byte> data, Uri uri, Transform transform)
    {
        var raw = Json.Tileset.FromJson(data)
            ?? throw new JsonException("JSON deserialisation returned null");
        return FromJson(raw, uri, transform);
    }

    /// <summary>
    /// Deserialise a Tileset.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="uri"></param>
    /// <param name="transform">The affine transformation applied to all bounding boxes in this
    /// Tileset. This can be used to convert from 3d Tiles coordinate space (right-handed Z-up) to
    /// Rhino3d's.</param>
    /// <returns></returns>
    /// <exception cref="JsonException"></exception>
    public static Tileset Deserialize(string data, Uri uri, Transform transform)
    {
        var raw = Json.Tileset.FromJson(data)
            ?? throw new JsonException("JSON deserialisation returned null");
        return FromJson(raw, uri, transform);
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
