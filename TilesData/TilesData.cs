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
    /// <exception cref="ArgumentException"></exception>
    public static void Require(bool Cond, string Message, string Argument)
    {
        if (!Cond)
        {
            throw new ArgumentException(Message, Argument);
        }
    }

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

    /// <summary>
    /// Convert (x, y, z) point in right-handed 3-axis coordinate system to Rhino3d's left-handed coordinate system.
    /// </summary>
    public static Point3d Point3dR(double X, double Y, double Z)
    {
        // Something should be negated
        // TODO: check if this is the correct coordinate to negate
        return new Point3d(X, Y, -Z);
    }

    /// <summary>
    /// Convert (x, y, z) vector in right-handed 3-axis coordinate system to Rhino3d's left-handed coordinate system.
    /// </summary>
    public static Vector3d Vector3dR(double X, double Y, double Z)
    {
        return new Vector3d(Point3dR(X, Y, Z));
    }
}

/// <summary>
/// https://github.com/CesiumGS/3d-tiles/tree/main/specification#box <br />
/// A bounding box that is not necessarily aligned to the X, Y or Z axis.
/// This is represented by Rhino3d's Box rather than its BoundingBox.
/// </summary>
/// <param name="Center">The center point of the box</param>
/// <param name="X">The x-axis of the box</param>
/// <param name="Y">The y-axis of the box</param>
/// <param name="Z">The z-axis of the box</param>
record class BoundingBox(Point3d Center, Vector3d X, Vector3d Y, Vector3d Z)
{
    [JsonConstructor]
    public BoundingBox(double[] values) : this(FromArray(values)) { }

    public static BoundingBox FromArray(double[] values)
    {
        Helpers.Require(values.Length == 12, "Expected array of length 12", nameof(values));

        var Center = Helpers.Point3dR(values[0], values[1], values[2]);
        var X = Helpers.Vector3dR(values[3], values[4], values[5]);
        var Y = Helpers.Vector3dR(values[6], values[7], values[8]);
        var Z = Helpers.Vector3dR(values[9], values[10], values[11]);

        return new BoundingBox(Center, X, Y, Z);
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
record class BoundingRegion(double West, double South, double East, double North, double MinHeight, double MaxHeight)
{
    [JsonConstructor]
    public BoundingRegion(double[] values) : this(FromArray(values)) { }

    public static BoundingRegion FromArray(double[] values)
    {
        Helpers.Require(values.Length == 6, "Expected array of length 6", nameof(values));

        return new BoundingRegion(values[0], values[1], values[2], values[3], values[4], values[5]);
    }
}

record class BoundingSphere(Point3d Center, double Radius)
{
    [JsonConstructor]
    public BoundingSphere(double[] values) : this(FromArray(values)) { }

    public static BoundingSphere FromArray(double[] values)
    {
        Helpers.Require(values.Length == 4, "Expected array of length 4", nameof(values));

        var Center = Helpers.Point3dR(values[0], values[1], values[2]);
        var Radius = values[3];

        return new BoundingSphere(Center, Radius);
    }
}

/// <summary>
/// The bounding volumne for a tile. All geometry in the tile is contained in all non-null bounding shapes.
/// At least 1 bounding shape must be defined. To ensure at least one bounding shape is defined, you should use BoundingVolumne.NewChecked
/// </summary>
record class BoundingVolume(BoundingBox? Box, BoundingRegion? Region, BoundingSphere? Sphere)
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

    public static readonly BoundingVolume Empty = new(null, null, null);
}

// TODO: support properties
/// <summary>
/// https://github.com/CesiumGS/3d-tiles/blob/main/specification/schema/metadataEntity.schema.json <br />
/// Metadata about a Tile or Content.
/// </summary>
/// <param name="class">The name of the class</param>
record class Metadata(string @class);

[JsonConverter(typeof(JsonStringEnumConverter))]
enum Refine
{
    ADD,
    REPLACE,
}

record class Content(BoundingVolume? BoundingVolume, string URI, int? Group);

record class Tile(
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

