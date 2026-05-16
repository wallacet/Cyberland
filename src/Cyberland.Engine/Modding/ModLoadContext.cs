using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;
using System.Threading;
using System.Threading.Tasks;

namespace Cyberland.Engine.Modding;

/// <summary>
/// Everything your <see cref="IMod.OnLoadAsync"/> needs to plug into the running game: ECS world, system scheduler, virtual files, locale, and host devices.
/// </summary>
/// <remarks>
/// Prefer registering work through this type instead of static globals so load order and tests stay predictable.
/// <para>
/// <b>Baked MSDF atlases:</b> see remarks on <see cref="LoadBakedMsdfAtlas"/> and <see cref="LoadBakedMsdfAtlasAsync"/> — the async path must not be
/// <c>await</c>ed from <see cref="IMod.OnLoadAsync"/> while the host still blocks inside <see cref="ModLoader.LoadAll"/> (deadlock vs render-thread drain).
/// </para>
/// </remarks>
public sealed class ModLoadContext
{
    /// <summary>Constructs a context (normally only the <see cref="ModLoader"/> calls this).</summary>
    /// <param name="manifest">This mod’s parsed <c>manifest.json</c>.</param>
    /// <param name="modDirectory">Absolute path to this mod’s folder.</param>
    /// <param name="vfs">Layered virtual file system (may already include earlier mods).</param>
    /// <param name="localizedContent">Merged strings + localized asset resolution (see <see cref="LocalizedContent"/>).</param>
    /// <param name="world">Shared ECS world.</param>
    /// <param name="scheduler">Where this mod registers systems.</param>
    /// <param name="host">Renderer, input, and optional stores from the host.</param>
    public ModLoadContext(
        ModManifest manifest,
        string modDirectory,
        VirtualFileSystem vfs,
        ILocalizedContent localizedContent,
        World world,
        SystemScheduler scheduler,
        GameHostServices host)
    {
        Manifest = manifest;
        ModDirectory = modDirectory;
        VirtualFileSystem = vfs;
        LocalizedContent = localizedContent;
        Localization = localizedContent.Strings;
        World = world;
        Scheduler = scheduler;
        Host = host;
    }

    /// <summary>Metadata deserialized from this mod’s <c>manifest.json</c>.</summary>
    public ModManifest Manifest { get; }
    /// <summary>Absolute path to this mod’s root folder on disk.</summary>
    public string ModDirectory { get; }
    /// <summary>Layered virtual file system; earlier mods may already have mounted content.</summary>
    public VirtualFileSystem VirtualFileSystem { get; }
    /// <summary>Localized strings and media (merge string tables via <see cref="ILocalizedContent.MergeStringTableAsync"/> or <see cref="ILocalizedContent.MergeStringTable"/>).</summary>
    public ILocalizedContent LocalizedContent { get; }

    /// <summary>Same as <see cref="ILocalizedContent.Strings"/>; merged across mods.</summary>
    public LocalizationManager Localization { get; }
    /// <summary>Shared ECS world for entities and components.</summary>
    public World World { get; }
    /// <summary>Register <see cref="Core.Ecs.ISystem"/> / <see cref="Core.Ecs.IParallelSystem"/> implementations (chunk query from <see cref="Core.Ecs.IEcsQuerySource.QuerySpec"/>).</summary>
    public SystemScheduler Scheduler { get; }

    /// <summary>Renderer, input, optional tilemap/particle stores — assigned by the host before <see cref="IMod.OnLoadAsync"/>.</summary>
    public GameHostServices Host { get; }

    /// <summary>
    /// Optional runtime scene stack (additive worlds). Null until the host calls <see cref="GameHostServices.InitializeRuntimeScenes"/>.
    /// </summary>
    public ISceneRuntime? Scenes => Host.Scenes;

    /// <summary>Mounts <see cref="ModManifest.ContentRoot"/> under this mod's folder.</summary>
    /// <remarks>
    /// <see cref="ModLoader"/> already mounts that path during discovery; calling this is optional and becomes a no-op when it matches the last VFS root (see <see cref="Assets.VirtualFileSystem.Mount"/>).
    /// </remarks>
    public void MountDefaultContent() =>
        VirtualFileSystem.Mount(Path.Combine(ModDirectory, Manifest.ContentRoot));

    /// <summary>Mounts an extra folder under your mod directory (e.g. <c>"Optional/ExtraAssets"</c>).</summary>
    /// <param name="relativeToModFolder">Path relative to <see cref="ModDirectory"/>.</param>
    public void MountContentSubfolder(string relativeToModFolder) =>
        VirtualFileSystem.Mount(Path.Combine(ModDirectory, relativeToModFolder));

    /// <summary>
    /// Blocks a relative path so it does not resolve even if an earlier mod provided it (see <see cref="VirtualFileSystem.BlockPath"/>).
    /// </summary>
    public void HideContentPath(string relativePath) =>
        VirtualFileSystem.BlockPath(relativePath);

    /// <summary>Registers or replaces a serial (single-threaded) ECS system under <paramref name="logicalId"/>.</summary>
    /// <param name="logicalId">Stable id for this system (enable/disable, diagnostics).</param>
    /// <param name="system">Serial system instance.</param>
    /// <param name="enabled">Initial enabled flag; use <see cref="SetSystemEnabled"/> to toggle later.</param>
    public void RegisterSerial(string logicalId, ISystem system, bool enabled = true) =>
        Scheduler.RegisterSerial(logicalId, system, enabled);

    /// <summary>Registers or replaces a parallel ECS system under <paramref name="logicalId"/>.</summary>
    /// <param name="logicalId">Stable id for this system (enable/disable, diagnostics).</param>
    /// <param name="system">Parallel system instance.</param>
    /// <param name="enabled">Initial enabled flag; use <see cref="SetSystemEnabled"/> to toggle later.</param>
    public void RegisterParallel(string logicalId, IParallelSystem system, bool enabled = true) =>
        Scheduler.RegisterParallel(logicalId, system, enabled);

    /// <summary>Registers a singleton ECS system: one entity matching <see cref="Core.Ecs.IEcsQuerySource.QuerySpec"/> (see <see cref="Core.Ecs.ISingletonSystem"/>).</summary>
    public void RegisterSingleton(string logicalId, ISingletonSystem system, bool enabled = true) =>
        Scheduler.RegisterSingleton(logicalId, system, enabled);

    /// <summary>Enables or disables a registered system without removing it (see <see cref="SystemScheduler.SetEnabled"/>).</summary>
    public bool SetSystemEnabled(string logicalId, bool enabled) => Scheduler.SetEnabled(logicalId, enabled);

    /// <summary>Removes a system registered under <paramref name="logicalId"/>.</summary>
    public bool TryUnregister(string logicalId) => Scheduler.TryUnregister(logicalId);

    /// <summary>Removes a localization key merged from an earlier mod (see <see cref="LocalizationManager.TryRemoveKey"/>).</summary>
    public bool TryRemoveLocalizationKey(string key) => Localization.TryRemoveKey(key);

    private string ComposeProgressKey(string phaseKey) =>
        $"mod:{Manifest.Id}:{phaseKey}";

    /// <summary>
    /// Starts a weighted load phase scoped to this mod for host loading UI aggregation.
    /// </summary>
    public IDisposable BeginLoadPhase(string phaseKey, float weight = 1f, string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseKey);
        return Host.StartupProgress.BeginPhase(
            ComposeProgressKey(phaseKey),
            weight,
            label ?? phaseKey,
            Manifest.Id);
    }

    /// <summary>
    /// Reports monotonic progress in <c>[0,1]</c> for a phase started by <see cref="BeginLoadPhase"/>.
    /// </summary>
    public void ReportLoadProgress(string phaseKey, float value01, string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseKey);
        Host.StartupProgress.ReportPhaseProgress(
            ComposeProgressKey(phaseKey),
            value01,
            label ?? phaseKey,
            Manifest.Id);
    }

    /// <summary>
    /// Adds a default mapping for a gameplay <paramref name="actionId"/> before gameplay scheduler updates start. The host loads
    /// <c>input-bindings.json</c> before it invokes <see cref="IMod.OnLoadAsync"/>, so user files replace the seed first; this call then
    /// adds (or appends) bindings the mod requires.
    /// </summary>
    /// <param name="actionId">Action id for <c>IInputService</c> (e.g. <c>HasActionPressedThisFrame</c>, <c>ReadAxis</c>).</param>
    /// <param name="binding">One physical control (repeat for multiple keys).</param>
    public void AddDefaultInputBinding(string actionId, InputBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(binding);
        if (Host.Input is null)
        {
            throw new InvalidOperationException(
                "Host.Input is not initialized. Add default input bindings only from IMod.OnLoadAsync after the host wires input.");
        }

        Host.Input.Bindings.AddBinding(actionId, binding);
    }

    /// <summary>
    /// Loads a pre-baked MSDF atlas manifest from a virtual path and seeds glyph cache entries for this process.
    /// The path may target mod content (<c>Content/...</c>) or an engine-shipped builtin virtual path from
    /// <see cref="BuiltinFonts.BakedAtlasManifestPath"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the <strong>synchronous</strong> path: decode + GPU upload run on the <strong>calling thread</strong> via
    /// <see cref="Cyberland.Engine.Rendering.Text.BakedMsdfAtlasLoader.LoadFromPath"/>. During normal host startup, <see cref="IMod.OnLoadAsync"/> runs after the Vulkan
    /// renderer exists, so calling this from <c>OnLoadAsync</c> is usually valid when you need the atlas before gameplay starts.
    /// </para>
    /// <para>
    /// Use this when you must block in <c>OnLoadAsync</c> until the atlas is usable (glyph cache seeded before gameplay starts).
    /// To overlap CPU decode with other startup work instead, call <see cref="LoadBakedMsdfAtlasAsync"/> <strong>without</strong> awaiting
    /// (see that method’s remarks — awaiting it from <c>OnLoadAsync</c> deadlocks).
    /// </para>
    /// </remarks>
    public bool LoadBakedMsdfAtlas(string manifestPath)
    {
        var assets = new AssetManager(VirtualFileSystem);
        using var phase = BeginLoadPhase($"msdf:{manifestPath}", 0.05f, $"Loading atlas {manifestPath}");
        var result = Host.BakedMsdfAtlasLoader.LoadFromPath(
            assets,
            Host.Renderer,
            Host.TextGlyphCache,
            manifestPath,
            onProgress: p => ReportLoadProgress($"msdf:{manifestPath}", p));
        if (!result.Loaded)
        {
            Console.WriteLine($"Mod baked atlas load failed | manifest={manifestPath} reason={result.Message}");
            return false;
        }

        Console.WriteLine($"Mod baked atlas load | manifest={manifestPath} glyphs={result.GlyphCount} pages={result.PageCount}");
        return true;
    }

    /// <summary>
    /// Starts asynchronous atlas decode for <paramref name="manifestPath"/> and schedules GPU upload on the render-thread drain.
    /// The returned task completes after upload and cache seeding finish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Mod <see cref="IMod.OnLoadAsync"/> / startup safety:</strong> the returned <see cref="Task{TResult}"/> completes only after
    /// <see cref="Cyberland.Engine.Rendering.Text.BakedMsdfAtlasLoader.DrainPendingUploads"/> runs on the render thread (see <see cref="Cyberland.Engine.GameApplication"/> draw path).
    /// <see cref="ModLoader.LoadAll"/> invokes <c>OnLoadAsync(...).GetAwaiter().GetResult()</c> synchronously while startup is inside mod load, so
    /// <strong>awaiting</strong> this method from <c>OnLoadAsync</c> deadlocks: the load thread waits for GPU drain, but drain never runs until load returns.
    /// </para>
    /// <para>
    /// <strong>Safe pattern from <c>OnLoadAsync</c>:</strong> kick off work without awaiting — e.g. <c>_ = context.LoadBakedMsdfAtlasAsync(path);</c> — same as shipped demos.
    /// Await is fine from code that runs after the render loop is pumping (e.g. a later async scene setup that does not block <see cref="ModLoader.LoadAll"/>).
    /// </para>
    /// </remarks>
    public async Task<bool> LoadBakedMsdfAtlasAsync(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        var assets = new AssetManager(VirtualFileSystem);
        var result = await Host.BakedMsdfAtlasLoader
            .LoadFromPathAsync(assets, Host.TextGlyphCache, manifestPath, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Loaded)
        {
            Console.WriteLine($"Mod baked atlas load failed | manifest={manifestPath} reason={result.Message}");
            return false;
        }

        Console.WriteLine($"Mod baked atlas load | manifest={manifestPath} glyphs={result.GlyphCount} pages={result.PageCount}");
        return true;
    }
}
