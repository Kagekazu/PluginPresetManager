using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.ImGuiNotification;
using PluginPresetManager.Models;

namespace PluginPresetManager;

public class PresetManager
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IChatGui chatGui;
    private readonly INotificationManager notificationManager;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly PresetStorage storage;

    private List<Preset> presets = new();
    private HashSet<string> alwaysOnPlugins = new();

    public PresetManager(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        INotificationManager notificationManager,
        IPluginLog log,
        Configuration config,
        PresetStorage storage)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.chatGui = chatGui;
        this.notificationManager = notificationManager;
        this.log = log;
        this.config = config;
        this.storage = storage;

        LoadFromStorage();
    }

    private void LoadFromStorage()
    {
        presets = storage.LoadAllPresets();
        alwaysOnPlugins = storage.LoadAlwaysOnPlugins();
        log.Info($"Loaded {presets.Count} presets and {alwaysOnPlugins.Count} always-on plugins");
    }
    
    private void ShowNotification(string message, bool isError = false)
    {
        switch (config.NotificationMode)
        {
            case NotificationMode.Toast:
                notificationManager.AddNotification(new Notification
                {
                    Content = message,
                    Type = isError ? NotificationType.Error : NotificationType.Success,
                    Title = "Preset Manager"
                });
                break;
            case NotificationMode.Chat:
                if (isError)
                    chatGui.PrintError($"[Preset] {message}");
                else
                    chatGui.Print($"[Preset] {message}");
                break;
            case NotificationMode.None:
                break;
        }
    }

    public List<Preset> GetAllPresets() => presets;

    public HashSet<string> GetAlwaysOnPlugins() => alwaysOnPlugins;

    public async Task ApplyAlwaysOnOnlyAsync(IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Applying always-on plugins only...");
            log.Info("Starting always-on only mode application");

            var installedPlugins = pluginInterface.InstalledPlugins
                .GroupBy(p => p.InternalName)
                .ToDictionary(g => g.Key, g => g.First());

            var effectiveEnabledSet = new HashSet<string>(alwaysOnPlugins);

            var toDisable = installedPlugins.Values
                .Where(p => p.IsLoaded && !effectiveEnabledSet.Contains(p.InternalName))
                .ToList();

            var toEnable = effectiveEnabledSet
                .Where(name => installedPlugins.ContainsKey(name)
                            && !installedPlugins[name].IsLoaded)
                .ToList();

            var failedDisable = new List<string>();
            var failedEnable = new List<string>();

            progress?.Report($"Disabling {toDisable.Count} plugins...");
            foreach (var plugin in toDisable)
            {
                var cmd = $"/xldisableplugin \"{plugin.Name}\"";
                commandManager.ProcessCommand(cmd);

                var maxWaitMs = 5000;
                var waitedMs = 0;
                var isDisabled = false;
                while (!isDisabled && waitedMs < maxWaitMs)
                {
                    await Task.Delay(config.PluginStateCheckInterval);
                    waitedMs += config.PluginStateCheckInterval;

                    var currentPlugin = pluginInterface.InstalledPlugins
                        .FirstOrDefault(p => p.InternalName == plugin.InternalName);

                    if (currentPlugin != null)
                    {
                        isDisabled = !currentPlugin.IsLoaded;
                    }
                }

                if (!isDisabled)
                {
                    failedDisable.Add(plugin.Name);
                    log.Warning($"Plugin {plugin.Name} did not disable within timeout (waited {waitedMs}ms)");
                }

                await Task.Delay(config.DelayBetweenCommands);
            }

            progress?.Report($"Enabling {toEnable.Count} always-on plugins...");
            foreach (var pluginName in toEnable)
            {
                var plugin = installedPlugins[pluginName];
                var cmd = $"/xlenableplugin \"{plugin.Name}\"";
                commandManager.ProcessCommand(cmd);

                var maxWaitMs = 5000;
                var waitedMs = 0;
                var isEnabled = false;
                while (!isEnabled && waitedMs < maxWaitMs)
                {
                    await Task.Delay(config.PluginStateCheckInterval);
                    waitedMs += config.PluginStateCheckInterval;

                    var currentPlugin = pluginInterface.InstalledPlugins
                        .FirstOrDefault(p => p.InternalName == pluginName);

                    if (currentPlugin != null)
                    {
                        isEnabled = currentPlugin.IsLoaded;
                    }
                }

                if (!isEnabled)
                {
                    failedEnable.Add(plugin.Name);
                    log.Warning($"Plugin {plugin.Name} did not enable within timeout (waited {waitedMs}ms)");
                }

                await Task.Delay(config.DelayBetweenCommands);
            }

            config.LastAppliedPresetId = null;
            pluginInterface.SavePluginConfig(config);

            progress?.Report("Always-on only mode applied!");
            
            var successfulDisable = toDisable.Count - failedDisable.Count;
            var successfulEnable = toEnable.Count - failedEnable.Count;
            
            if (failedDisable.Count > 0 || failedEnable.Count > 0)
            {
                var notificationMessage = $"Applied always-on mode with failures:\n" +
                    $"Enabled: {successfulEnable}/{toEnable.Count}, Disabled: {successfulDisable}/{toDisable.Count}";
                    
                if (failedEnable.Count > 0)
                {
                    notificationMessage += $"\nFailed to enable: {string.Join(", ", failedEnable.Take(3))}";
                    if (failedEnable.Count > 3) notificationMessage += $" and {failedEnable.Count - 3} more";
                }
                if (failedDisable.Count > 0)
                {
                    notificationMessage += $"\nFailed to disable: {string.Join(", ", failedDisable.Take(3))}";
                    if (failedDisable.Count > 3) notificationMessage += $" and {failedDisable.Count - 3} more";
                }
                
                ShowNotification(notificationMessage, true);
                
                log.Warning($"Applied always-on mode with failures - Enabled: {successfulEnable}/{toEnable.Count}, Disabled: {successfulDisable}/{toDisable.Count}");
                if (failedEnable.Count > 0)
                {
                    log.Warning($"Failed to enable plugins: {string.Join(", ", failedEnable)}");
                }
                if (failedDisable.Count > 0)
                {
                    log.Warning($"Failed to disable plugins: {string.Join(", ", failedDisable)}");
                }
            }
            else
            {
                ShowNotification($"Applied always-on only mode ({toEnable.Count} enabled, {toDisable.Count} disabled)");
                log.Info($"Successfully applied always-on only mode: {toEnable.Count} enabled, {toDisable.Count} disabled");
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to apply always-on only mode");
            ShowNotification($"Failed to apply always-on only mode: {ex.Message}", true);
            throw;
        }
    }

    public async Task ApplyPresetAsync(Preset preset, IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Validating preset...");
            log.Info($"Starting preset application: {preset.Name}");
            
            var missingPlugins = GetMissingPlugins(preset);
            if (missingPlugins.Count > 0)
            {
                log.Warning($"Preset {preset.Name} has {missingPlugins.Count} missing plugins: {string.Join(", ", missingPlugins)}");
            }

            var installedPlugins = pluginInterface.InstalledPlugins
                .GroupBy(p => p.InternalName)
                .ToDictionary(g => g.Key, g => g.First());

            var effectiveEnabledSet = new HashSet<string>(preset.EnabledPlugins);

            foreach (var alwaysOnPlugin in alwaysOnPlugins)
            {
                if (installedPlugins.ContainsKey(alwaysOnPlugin))
                {
                    effectiveEnabledSet.Add(alwaysOnPlugin);
                }
            }

            progress?.Report($"Effective plugin set: {effectiveEnabledSet.Count} plugins");

            var toDisable = installedPlugins.Values
                .Where(p => p.IsLoaded && !effectiveEnabledSet.Contains(p.InternalName))
                .ToList();

            var toEnable = effectiveEnabledSet
                .Where(name => installedPlugins.ContainsKey(name)
                            && !installedPlugins[name].IsLoaded)
                .ToList();


            var failedDisable = new List<string>();
            var failedEnable = new List<string>();

            progress?.Report($"Disabling {toDisable.Count} plugins...");
            foreach (var plugin in toDisable)
            {
                var cmd = $"/xldisableplugin \"{plugin.Name}\"";
                commandManager.ProcessCommand(cmd);

                var maxWaitMs = 5000;
                var waitedMs = 0;
                var isDisabled = false;
                while (!isDisabled && waitedMs < maxWaitMs)
                {
                    await Task.Delay(config.PluginStateCheckInterval);
                    waitedMs += config.PluginStateCheckInterval;

                    var currentPlugin = pluginInterface.InstalledPlugins
                        .FirstOrDefault(p => p.InternalName == plugin.InternalName);

                    if (currentPlugin != null)
                    {
                        isDisabled = !currentPlugin.IsLoaded;
                    }
                }

                if (!isDisabled)
                {
                    failedDisable.Add(plugin.Name);
                    log.Warning($"Plugin {plugin.Name} did not disable within timeout (waited {waitedMs}ms)");
                }

                await Task.Delay(config.DelayBetweenCommands);
            }

            progress?.Report($"Enabling {toEnable.Count} plugins...");
            foreach (var pluginName in toEnable)
            {
                var plugin = installedPlugins[pluginName];
                var cmd = $"/xlenableplugin \"{plugin.Name}\"";
                commandManager.ProcessCommand(cmd);

                var maxWaitMs = 5000;
                var waitedMs = 0;
                var isEnabled = false;
                while (!isEnabled && waitedMs < maxWaitMs)
                {
                    await Task.Delay(config.PluginStateCheckInterval);
                    waitedMs += config.PluginStateCheckInterval;

                    var currentPlugin = pluginInterface.InstalledPlugins
                        .FirstOrDefault(p => p.InternalName == pluginName);

                    if (currentPlugin != null)
                    {
                        isEnabled = currentPlugin.IsLoaded;
                    }
                }

                if (!isEnabled)
                {
                    failedEnable.Add(plugin.Name);
                    log.Warning($"Plugin {plugin.Name} did not enable within timeout (waited {waitedMs}ms)");
                }

                await Task.Delay(config.DelayBetweenCommands);
            }

            config.LastAppliedPresetId = preset.Id;
            pluginInterface.SavePluginConfig(config);

            progress?.Report($"Preset '{preset.Name}' applied successfully!");
            
            var successfulDisable = toDisable.Count - failedDisable.Count;
            var successfulEnable = toEnable.Count - failedEnable.Count;
            
            if (failedDisable.Count > 0 || failedEnable.Count > 0)
            {
                var notificationMessage = $"Applied preset '{preset.Name}' with failures:\n" +
                    $"Enabled: {successfulEnable}/{toEnable.Count}, Disabled: {successfulDisable}/{toDisable.Count}";
                    
                if (failedEnable.Count > 0)
                {
                    notificationMessage += $"\nFailed to enable: {string.Join(", ", failedEnable.Take(3))}";
                    if (failedEnable.Count > 3) notificationMessage += $" and {failedEnable.Count - 3} more";
                }
                if (failedDisable.Count > 0)
                {
                    notificationMessage += $"\nFailed to disable: {string.Join(", ", failedDisable.Take(3))}";
                    if (failedDisable.Count > 3) notificationMessage += $" and {failedDisable.Count - 3} more";
                }
                
                ShowNotification(notificationMessage, true);
                
                log.Warning($"Applied preset '{preset.Name}' with failures - Enabled: {successfulEnable}/{toEnable.Count}, Disabled: {successfulDisable}/{toDisable.Count}");
                if (failedEnable.Count > 0)
                {
                    log.Warning($"Failed to enable plugins: {string.Join(", ", failedEnable)}");
                }
                if (failedDisable.Count > 0)
                {
                    log.Warning($"Failed to disable plugins: {string.Join(", ", failedDisable)}");
                }
            }
            else
            {
                ShowNotification($"Applied '{preset.Name}' ({toEnable.Count} enabled, {toDisable.Count} disabled)");
                log.Info($"Successfully applied preset '{preset.Name}': {toEnable.Count} enabled, {toDisable.Count} disabled");
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to apply preset '{preset.Name}'");
            ShowNotification($"Failed to apply '{preset.Name}': {ex.Message}", true);
            throw;
        }
    }

    public void AddAlwaysOnPlugin(string internalName)
    {
        if (!alwaysOnPlugins.Contains(internalName))
        {
            alwaysOnPlugins.Add(internalName);
            storage.SaveAlwaysOnPlugins(alwaysOnPlugins);

            var plugin = pluginInterface.InstalledPlugins
                .FirstOrDefault(p => p.InternalName == internalName);

            if (plugin != null && !plugin.IsLoaded)
            {
                commandManager.ProcessCommand($"/xlenableplugin \"{plugin.Name}\"");
            }

            ShowNotification($"Added '{internalName}' to always-on list");
            log.Info($"Added '{internalName}' to always-on list");
        }
    }

    public void RemoveAlwaysOnPlugin(string internalName)
    {
        if (alwaysOnPlugins.Remove(internalName))
        {
            storage.SaveAlwaysOnPlugins(alwaysOnPlugins);

            ShowNotification($"Removed '{internalName}' from always-on list");
            log.Info($"Removed '{internalName}' from always-on list");
        }
    }

    public bool IsAlwaysOn(string internalName)
    {
        return alwaysOnPlugins.Contains(internalName);
    }

    public PresetPreview GetPresetPreview(Preset preset)
    {
        var preview = new PresetPreview();
        var installedPlugins = pluginInterface.InstalledPlugins
            .GroupBy(p => p.InternalName)
            .ToDictionary(g => g.Key, g => g.First());

        var effectiveEnabledSet = new HashSet<string>(preset.EnabledPlugins);
        effectiveEnabledSet.UnionWith(alwaysOnPlugins);

        foreach (var plugin in installedPlugins.Values)
        {
            var shouldBeEnabled = effectiveEnabledSet.Contains(plugin.InternalName);
            var isAlwaysOn = alwaysOnPlugins.Contains(plugin.InternalName);

            if (plugin.IsLoaded && !shouldBeEnabled)
            {
                preview.ToDisable.Add(new PresetPreview.PluginChange
                {
                    InternalName = plugin.InternalName,
                    DisplayName = plugin.Name,
                    IsAlwaysOn = isAlwaysOn
                });
            }
            else if (!plugin.IsLoaded && shouldBeEnabled)
            {
                preview.ToEnable.Add(new PresetPreview.PluginChange
                {
                    InternalName = plugin.InternalName,
                    DisplayName = plugin.Name,
                    IsAlwaysOn = isAlwaysOn
                });
            }
            else if (plugin.IsLoaded && shouldBeEnabled)
            {
                preview.NoChange.Add(new PresetPreview.PluginChange
                {
                    InternalName = plugin.InternalName,
                    DisplayName = plugin.Name,
                    IsAlwaysOn = isAlwaysOn
                });
            }
        }

        foreach (var pluginName in effectiveEnabledSet)
        {
            if (!installedPlugins.ContainsKey(pluginName))
            {
                preview.Missing.Add(pluginName);
            }
        }

        return preview;
    }

    public Preset CreatePresetFromCurrent(string name)
    {
        var preset = new Preset
        {
            Name = name,
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now
        };

        foreach (var plugin in pluginInterface.InstalledPlugins)
        {
            if (plugin.IsLoaded && !alwaysOnPlugins.Contains(plugin.InternalName))
            {
                preset.EnabledPlugins.Add(plugin.InternalName);
            }
        }

        log.Info($"Created preset '{name}' with {preset.EnabledPlugins.Count} plugins");
        return preset;
    }


    public List<string> GetMissingPlugins(Preset preset)
    {
        var installedPlugins = pluginInterface.InstalledPlugins
            .GroupBy(p => p.InternalName)
            .ToDictionary(g => g.Key, g => g.First());

        return preset.EnabledPlugins
            .Where(pluginName => !installedPlugins.ContainsKey(pluginName))
            .ToList();
    }

    public Preset? GetPresetById(Guid id)
    {
        return presets.FirstOrDefault(p => p.Id == id);
    }

    public Preset? GetLastAppliedPreset()
    {
        if (config.LastAppliedPresetId == null)
            return null;

        return GetPresetById(config.LastAppliedPresetId.Value);
    }

    public void DeletePreset(Preset preset)
    {
        if (presets.Remove(preset))
        {
            storage.DeletePreset(preset);
            log.Info($"Deleted preset '{preset.Name}'");

            ShowNotification($"Deleted preset '{preset.Name}'");
        }
    }

    public void AddPreset(Preset preset)
    {
        presets.Add(preset);
        storage.SavePreset(preset);
        log.Info($"Added new preset '{preset.Name}'");
    }

    public void UpdatePreset(Preset preset)
    {
        preset.LastModified = DateTime.Now;
        storage.SavePreset(preset);
        log.Info($"Updated preset '{preset.Name}'");
    }
}
