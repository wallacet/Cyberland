using System.Text.Json;
using System.Text.Json.Serialization;
using Silk.NET.Input;

namespace Cyberland.Engine.Input;

/// <summary>
/// Runtime-editable map from action ids to one or more physical input bindings.
/// </summary>
public sealed class InputBindings
{
    private readonly Dictionary<string, List<InputBinding>> _map = new(StringComparer.Ordinal);

    /// <summary>Enumerates all action ids currently present in the map.</summary>
    public IEnumerable<string> ActionIds => _map.Keys;

    /// <summary>Replaces all bindings for <paramref name="actionId"/>.</summary>
    public void SetBindings(string actionId, IEnumerable<InputBinding> bindings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(bindings);
        _map[actionId] = bindings.ToList();
    }

    /// <summary>Adds one binding to <paramref name="actionId"/>.</summary>
    public void AddBinding(string actionId, InputBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (!_map.TryGetValue(actionId, out var list))
        {
            list = new List<InputBinding>();
            _map[actionId] = list;
        }

        list.Add(binding);
    }

    /// <summary>Removes all bindings for <paramref name="actionId"/>.</summary>
    public bool ClearBindings(string actionId) => _map.Remove(actionId);

    /// <summary>Removes a specific binding from <paramref name="actionId"/>.</summary>
    public bool RemoveBinding(string actionId, InputBinding binding)
    {
        if (!_map.TryGetValue(actionId, out var list))
            return false;
        return list.Remove(binding);
    }

    /// <summary>Clears the entire binding table.</summary>
    public void Clear() => _map.Clear();

    /// <summary>Returns a snapshot list of bindings for <paramref name="actionId"/>.</summary>
    public bool TryGetBindings(string actionId, out IReadOnlyList<InputBinding> bindings)
    {
        if (_map.TryGetValue(actionId, out var list))
        {
            bindings = list.ToArray();
            return true;
        }

        bindings = Array.Empty<InputBinding>();
        return false;
    }

    /// <summary>
    /// Loads host-wide defaults. Demo mods add gameplay bindings via
    /// <c>Cyberland.Engine.Modding.ModLoadContext.AddDefaultInputBinding</c> in <c>IMod.OnLoad</c>.
    /// </summary>
    public void LoadDefaults()
    {
        _map.Clear();

        AddBinding("cyberland.common/quit", new InputBinding(InputControl.Keyboard(Key.Q)));
        AddBinding("cyberland.common/menu", new InputBinding(InputControl.Keyboard(Key.Escape)));
        AddBinding("cyberland.common/start", new InputBinding(InputControl.Keyboard(Key.Enter)));
        AddBinding("cyberland.common/start", new InputBinding(InputControl.Keyboard(Key.KeypadEnter)));
        AddBinding("cyberland.common/start", new InputBinding(InputControl.Keyboard(Key.R)));
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
        var payload = await JsonSerializer.DeserializeAsync<InputBindingsFile>(fs, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (payload?.Bindings is null)
        {
            LoadDefaults();
            return;
        }

        _map.Clear();
        foreach (var (actionId, rawBindings) in payload.Bindings)
        {
            if (string.IsNullOrWhiteSpace(actionId) || rawBindings is null)
                continue;

            foreach (var raw in rawBindings)
            {
                if (raw?.Control is null || !InputControl.TryParse(raw.Control, out var control))
                    continue;
                AddBinding(actionId, new InputBinding(control, raw.Scale));
            }
        }
    }

    /// <summary>Persists bindings as versioned JSON with stable control strings.</summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        var payload = new InputBindingsFile
        {
            Version = 1,
            Bindings = _map.ToDictionary(
                static kv => kv.Key,
                static kv => kv.Value.Select(static b => new BindingDto
                {
                    Control = b.Control.ToPersistedString(),
                    Scale = b.Scale
                }).ToArray(),
                StringComparer.Ordinal)
        };

        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, payload, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private sealed class InputBindingsFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("bindings")]
        public Dictionary<string, BindingDto[]>? Bindings { get; set; }
    }

    private sealed class BindingDto
    {
        [JsonPropertyName("control")]
        public string? Control { get; set; }

        [JsonPropertyName("scale")]
        public float Scale { get; set; } = 1f;
    }
}
