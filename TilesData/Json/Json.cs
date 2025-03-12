// This file contains a direct translation of the 3d Tiles JSON schema.
// This simplifies JSON deserialization, since C#'s JSON libray
// is not at all optimised for the case of just deserialisation
// without needing to do serialization
namespace TilesData.Json;

using System.Text.Json;
using TilesData;

public record class Asset(
    string Version,
    string? TilesetVersion
);

public record class BoundingVolume(
    List<double>? Box,
    List<double>? Region,
    List<double>? Sphere
);

public record class Content(
    BoundingVolume? BoundingVolume,
    Uri Uri,
    MetadataEntity Metadata,
    uint? Group
);

public record class MetadataEntity(
    string Class,
    Dictionary<string, JsonElement>? Properties
);

public record class Subtrees(
    Uri Uri
);

public record class ImplicitTiling(
    SubdivisionScheme SubdivisionScheme,
    uint SubtreeLevels,
    uint AvailableLevels,
    Subtrees Subtrees
);

public record class Tile(
    BoundingVolume BoundingVolume,
    BoundingVolume? ViewerRequestVolume,
    double GeometricError,
    Refine? Refine,
    List<double>? Transform,
    Content? Content,
    List<Content>? Contents,
    MetadataEntity? Metadata,
    ImplicitTiling? ImplicitTiling,
    List<Tile>? Children
);

public record class Tileset(
    Asset Asset,
    // schema, schemaUri
    // statistics
    List<MetadataEntity>? Groups,
    double GeometricError,
    Tile Root,
    List<string>? ExtensionsUsed,
    List<string>? ExtensionsRequired
)
{
    /// <summary>
    /// Default JsonSerializerOptions for deserialising Tiles3D json.
    /// </summary>
    public readonly static JsonSerializerOptions JsonSerializerOptions
        = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IncludeFields = true
        };

    public static Tileset? FromJson(ReadOnlySpan<byte> data)
    {
        return JsonSerializer.Deserialize<Tileset>(data, JsonSerializerOptions);
    }

    public static Tileset? FromJson(string data)
    {
        return JsonSerializer.Deserialize<Tileset>(data, JsonSerializerOptions);
    }
}

