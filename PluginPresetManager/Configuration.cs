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
    public int Version { get; set; } = 2;

    public Guid? LastAppliedPresetId { get; set; }

    public Guid? DefaultPresetId { get; set; }

    public int DelayBetweenCommands { get; set; } = 50;
    
    public int PluginStateCheckInterval { get; set; } = 250;
    
    public NotificationMode NotificationMode { get; set; } = NotificationMode.Toast;
    
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
    }
}
