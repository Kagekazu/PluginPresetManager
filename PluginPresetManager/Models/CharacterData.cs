using System;
using System.Collections.Generic;

namespace PluginPresetManager.Models;

[Serializable]
public class CharacterData
{
    public ulong ContentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string World { get; set; } = string.Empty;

    public DateTime LastSeen { get; set; } = DateTime.Now;

    public List<Preset> Presets { get; set; } = new();

    public HashSet<string> AlwaysOn { get; set; } = new();

    public string? DefaultPreset { get; set; }

    public bool UseAlwaysOnAsDefault { get; set; } = false;

    public bool ApplyDefaultOnLogin { get; set; } = false;

    public string? LastAppliedPreset { get; set; }

    public bool LastAppliedWasAlwaysOn { get; set; } = false;

    public NotificationMode NotificationMode { get; set; } = NotificationMode.Toast;

    public string DisplayName => string.IsNullOrEmpty(World)
        ? Name
        : $"{Name} @ {World}";

    public string FileName
    {
        get
        {
            var name = Name.Replace(" ", "_");
            var world = World.Replace(" ", "_");
            return string.IsNullOrEmpty(world) ? name : $"{name}_{world}";
        }
    }
}
