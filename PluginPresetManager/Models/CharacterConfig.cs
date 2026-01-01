using System;

namespace PluginPresetManager.Models;

[Serializable]
public class CharacterConfig
{
    public ulong ContentId { get; set; }

    public Guid? DefaultPresetId { get; set; }

    public Guid? LastAppliedPresetId { get; set; }

    public NotificationMode NotificationMode { get; set; } = NotificationMode.Toast;

    public int DelayBetweenCommands { get; set; } = 50;

    public int PluginStateCheckInterval { get; set; } = 1000;
}
