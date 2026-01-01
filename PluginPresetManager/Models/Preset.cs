using System;
using System.Collections.Generic;

namespace PluginPresetManager.Models;

[Serializable]
public class Preset
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public HashSet<string> Plugins { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime LastModified { get; set; } = DateTime.Now;
}
