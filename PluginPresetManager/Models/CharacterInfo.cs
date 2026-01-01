using System;

namespace PluginPresetManager.Models;

[Serializable]
public class CharacterInfo
{
    public ulong ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; } = DateTime.Now;

    public string DisplayName => string.IsNullOrEmpty(World)
        ? Name
        : $"{Name} @ {World}";
}
