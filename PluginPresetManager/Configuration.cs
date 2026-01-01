using System;
using Dalamud.Configuration;
using Newtonsoft.Json;

namespace PluginPresetManager;

public enum NotificationMode
{
    /// <summary>No notifications</summary>
    None = 0,

    /// <summary>Show toast notifications only</summary>
    Toast = 1,

    /// <summary>Show chat messages only</summary>
    Chat = 2,
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;

    [JsonProperty]
    public Guid? LastAppliedPresetId { get; set; }

    [JsonProperty]
    public Guid? DefaultPresetId { get; set; }

    [JsonProperty]
    public int DelayBetweenCommands { get; set; } = 50;

    [JsonProperty]
    public int PluginStateCheckInterval { get; set; } = 1000;

    [JsonProperty]
    public NotificationMode NotificationMode { get; set; } = NotificationMode.Toast;

    public bool UseExperimentalPersistence { get; set; } = false;

    public ulong LastSelectedCharacterId { get; set; } = 0;

    [JsonProperty]
    private bool? ShowNotifications { get; set; }

    [JsonProperty]
    private bool? VerboseNotifications { get; set; }

    public void Migrate()
    {
        if (Version < 2)
        {
            if (ShowNotifications.HasValue)
            {
                if (!ShowNotifications.Value)
                {
                    NotificationMode = NotificationMode.None;
                }
                else if (VerboseNotifications.HasValue && VerboseNotifications.Value)
                {
                    NotificationMode = NotificationMode.Chat;
                }
                else
                {
                    NotificationMode = NotificationMode.Toast;
                }
            }

            ShowNotifications = null;
            VerboseNotifications = null;

            Version = 2;
        }

        if (Version < 3)
        {
            if (PluginStateCheckInterval == 250)
            {
                PluginStateCheckInterval = 1000;
            }

            Version = 3;
        }

        if (Version < 4)
        {
            Version = 4;
        }
    }
}
