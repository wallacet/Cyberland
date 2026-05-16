using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyberland.Engine.RuntimeScenes.Serialization;

/// <summary>
/// Root DTO for runtime scene JSON files (see <see cref="SceneDocument.CurrentSchemaVersion"/>).
/// </summary>
public sealed class SceneDocument
{
    /// <summary>Latest schema version written by the engine serializer.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>Integer schema version; older files run migrators before spawn.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>Entities to spawn in load order.</summary>
    [JsonPropertyName("entities")]
    public List<SceneEntityDto> Entities { get; set; } = new();
}

/// <summary>
/// One entity entry in a scene document.
/// </summary>
public sealed class SceneEntityDto
{
    /// <summary>Optional stable GUID string for <see cref="Cyberland.Engine.Scene.LogicalActorId"/>.</summary>
    [JsonPropertyName("logicalId")]
    public string? LogicalId { get; set; }

    /// <summary>Component payloads.</summary>
    [JsonPropertyName("components")]
    public List<SceneComponentDto> Components { get; set; } = new();
}

/// <summary>
/// One component payload in a scene entity.
/// </summary>
public sealed class SceneComponentDto
{
    /// <summary>Stable type id (e.g. <c>cyberland.engine/transform</c>).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>Component-specific JSON object.</summary>
    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}
