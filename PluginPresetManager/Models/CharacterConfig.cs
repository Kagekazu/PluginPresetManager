using System;

namespace PluginPresetManager.Models;

[Serializable]
public class CharacterConfig
{
    public ulong ContentId { get; set; }

    public Guid? DefaultPresetId { get; set; }

    public Guid? LastAppliedPresetId { get; set; }

    public NotificationMode NotificationMode { get; set; } = NotificationMode.Toast;
}
