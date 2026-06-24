namespace Cyberland.Engine.Assets;

/// <summary>Outcome of a non-throwing texture load attempt.</summary>
public enum TextureLoadStatus
{
    /// <summary>File decoded and registered successfully.</summary>
    Ok,

    /// <summary>VFS path does not exist.</summary>
    NotFound,

    /// <summary>Bytes present but ImageSharp could not decode them.</summary>
    DecodeFailed,

    /// <summary>Decode succeeded but <see cref="Rendering.IRenderer.RegisterTextureRgba"/> failed.</summary>
    GpuRegistrationFailed
}

/// <summary>Result of <see cref="AssetManager.TryLoadTexture"/>.</summary>
/// <param name="Id">Registered texture id, or <see cref="Rendering.IRenderer.MissingTextureId"/> on failure.</param>
/// <param name="Status">Failure category when <paramref name="Id"/> is the missing-texture slot.</param>
public readonly record struct TextureLoadResult(TextureId Id, TextureLoadStatus Status);
