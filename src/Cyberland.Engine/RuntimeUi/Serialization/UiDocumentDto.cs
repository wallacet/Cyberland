using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyberland.Engine.RuntimeUi.Serialization;

/// <summary>Root DTO for runtime UI JSON files (see <see cref="CurrentSchemaVersion"/>).</summary>
public sealed class UiDocumentDto
{
    /// <summary>Latest schema version for UI documents.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Integer schema version; older files run migrators before build.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>Root widget tree node (flat JSON object with <c>type</c> and children).</summary>
    [JsonPropertyName("root")]
    public JsonElement Root { get; set; }
}
