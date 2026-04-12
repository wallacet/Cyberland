using System.Text.Json;
using Silk.NET.Input;

namespace Cyberland.Engine.Input;

/// <summary>
/// Maps action ids to Silk key codes; persisted as JSON for user overrides.
/// </summary>
public sealed class KeyBindingStore
{
    private readonly Dictionary<string, Key> _map = new(StringComparer.Ordinal);

    /// <summary>Assigns or replaces the key for an action id (in-memory until <see cref="SaveAsync"/>).</summary>
    public void Set(string actionId, Key key) => _map[actionId] = key;

    /// <summary>Looks up the physical key for <paramref name="actionId"/>.</summary>
    public bool TryGet(string actionId, out Key key) => _map.TryGetValue(actionId, out key);

    /// <summary>Resets to engine defaults (WASD + Escape).</summary>
    public void LoadDefaults()
    {
        _map.Clear();
        Set("move_up", Key.W);
        Set("move_down", Key.S);
        Set("move_left", Key.A);
        Set("move_right", Key.D);
        Set("menu", Key.Escape);
    }

    /// <summary>Loads JSON from disk or seeds defaults and writes a new file.</summary>
    public async Task LoadOrCreateUserFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            LoadDefaults();
            await SaveAsync(path, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var fs = File.OpenRead(path);
        var data = await JsonSerializer.DeserializeAsync<Dictionary<string, int>>(fs, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (data is null)
        {
            LoadDefaults();
            return;
        }

        _map.Clear();
        foreach (var kv in data)
            _map[kv.Key] = (Key)kv.Value;
    }

    /// <summary>Persists the current map as JSON (integer enum values for <see cref="Key"/>).</summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        var serializable = _map.ToDictionary(static kv => kv.Key, static kv => (int)kv.Value);
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, serializable, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// True if the bound key for <paramref name="actionId"/> is currently held down.
    /// Uses <see cref="IKeyboard.IsKeyPressed"/> (Silk names it “pressed” but it is the <strong>current</strong> key state, not an edge).
    /// </summary>
    public bool IsDown(IKeyboard keyboard, string actionId) =>
        TryGet(actionId, out var key) && keyboard.IsKeyPressed(key);
}
