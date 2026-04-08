using System.Text.Json;
using Silk.NET.Input;

namespace Cyberland.Engine.Input;

/// <summary>
/// Maps action ids to Silk key codes; persisted as JSON for user overrides.
/// </summary>
public sealed class KeyBindingStore
{
    private readonly Dictionary<string, Key> _map = new(StringComparer.Ordinal);

    public void Set(string actionId, Key key) => _map[actionId] = key;

    public bool TryGet(string actionId, out Key key) => _map.TryGetValue(actionId, out key);

    public void LoadDefaults()
    {
        _map.Clear();
        Set("move_up", Key.W);
        Set("move_down", Key.S);
        Set("move_left", Key.A);
        Set("move_right", Key.D);
        Set("menu", Key.Escape);
    }

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

    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        var serializable = _map.ToDictionary(static kv => kv.Key, static kv => (int)kv.Value);
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, serializable, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public bool IsDown(IKeyboard keyboard, string actionId) =>
        TryGet(actionId, out var key) && keyboard.IsKeyPressed(key);
}
