using System;
using System.Collections.Generic;

namespace PluginPresetManager.Models;

/// <summary>
/// Shared data that applies across all characters.
/// Shared presets and always-on plugins are kept separate from per-character data.
/// </summary>
[Serializable]
public class SharedData
{
    /// <summary>
    /// Presets that are available to all characters.
    /// </summary>
    public List<Preset> Presets { get; set; } = new();

    /// <summary>
    /// Plugins that should always be enabled for all characters.
    /// </summary>
    public HashSet<string> AlwaysOn { get; set; } = new();
}
