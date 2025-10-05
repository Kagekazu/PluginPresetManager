using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PluginPresetManager.Models;

namespace PluginPresetManager;

public class PresetManager
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly PresetStorage storage;

    private List<Preset> presets = new();
    private HashSet<string> alwaysOnPlugins = new();

    public PresetManager(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        IPluginLog log,
        Configuration config,
        PresetStorage storage)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.chatGui = chatGui;
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

    public List<Preset> GetAllPresets() => presets;

    public HashSet<string> GetAlwaysOnPlugins() => alwaysOnPlugins;

    public async Task ApplyAlwaysOnOnlyAsync(IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Applying always-on plugins only...");
            log.Info("Applying always-on only mode");

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


            progress?.Report($"Disabling {toDisable.Count} plugins...");
            foreach (var plugin in toDisable)
            {
                log.Info($"Disabling: {plugin.Name} (InternalName: {plugin.InternalName})");
                var cmd = $"/xldisableplugin \"{plugin.InternalName}\"";
                commandManager.ProcessCommand(cmd);

                var maxWaitMs = 5000;
                var waitedMs = 0;
                var isDisabled = false;
                while (!isDisabled && waitedMs < maxWaitMs)
                {
                    await Task.Delay(100);
                    waitedMs += 100;

                    var currentPlugin = pluginInterface.InstalledPlugins
                        .FirstOrDefault(p => p.InternalName == plugin.InternalName);

                    if (currentPlugin != null)
                    {
                        isDisabled = !currentPlugin.IsLoaded;
                    }
                }

                await Task.Delay(config.DelayBetweenCommands);
            }

            progress?.Report($"Enabling {toEnable.Count} always-on plugins...");
            foreach (var pluginName in toEnable)
            {
                var plugin = installedPlugins[pluginName];
                log.Info($"Enabling: {plugin.Name} (InternalName: {plugin.InternalName})");
                var cmd = $"/xlenableplugin \"{plugin.InternalName}\"";
                commandManager.ProcessCommand(cmd);

                var maxWaitMs = 5000;
                var waitedMs = 0;
                var isEnabled = false;
                while (!isEnabled && waitedMs < maxWaitMs)
                {
                    await Task.Delay(100);
                    waitedMs += 100;

                    var currentPlugin = pluginInterface.InstalledPlugins
                        .FirstOrDefault(p => p.InternalName == pluginName);

                    if (currentPlugin != null)
                    {
                        isEnabled = currentPlugin.IsLoaded;
                    }
                }

                await Task.Delay(config.DelayBetweenCommands);
            }

            config.LastAppliedPresetId = null;
            pluginInterface.SavePluginConfig(config);

            progress?.Report("Always-on only mode applied!");

            if (config.ShowNotifications)
            {
                if (config.VerboseNotifications)
                {
                    chatGui.Print($"[Preset] Applied always-on only mode ({alwaysOnPlugins.Count} plugins enabled, {toDisable.Count} disabled)");
                }
                else
                {
                    chatGui.Print($"[Preset] Applied always-on only mode");
                }
            }

            log.Info("Successfully applied always-on only mode");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to apply always-on only mode");
            chatGui.PrintError($"[Preset] Failed to apply always-on only mode: {ex.Message}");
            throw;
        }
    }

    public async Task ApplyPresetAsync(Preset preset, IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Validating preset...");
            var missingPlugins = GetMissingPlugins(preset);

            if (missingPlugins.Any() && config.ShowNotifications && config.VerboseNotifications)
            {
                chatGui.Print($"[Preset] Warning: {missingPlugins.Count} plugins missing from preset '{preset.Name}'");
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
                else if (config.ShowNotifications && config.VerboseNotifications)
                {
                    chatGui.Print($"[Preset] Warning: Always-on plugin '{alwaysOnPlugin}' not installed");
                }
            }

            progress?.Report($"Effective plugin set: {effectiveEnabledSet.Count} plugins");
            log.Info($"Applying preset '{preset.Name}' with {effectiveEnabledSet.Count} effective plugins");

            var toDisable = installedPlugins.Values
                .Where(p => p.IsLoaded && !effectiveEnabledSet.Contains(p.InternalName))
                .ToList();

            var toEnable = effectiveEnabledSet
                .Where(name => installedPlugins.ContainsKey(name)
                            && !installedPlugins[name].IsLoaded)
                .ToList();


            progress?.Report($"Disabling {toDisable.Count} plugins...");
            foreach (var plugin in toDisable)
            {
                log.Info($"Disabling: {plugin.Name} (InternalName: {plugin.InternalName})");
                var cmd = $"/xldisableplugin \"{plugin.InternalName}\"";
                log.Debug($"Executing: {cmd}");
                commandManager.ProcessCommand(cmd);

                var maxWaitMs = 5000;
                var waitedMs = 0;
                var isDisabled = false;
                while (!isDisabled && waitedMs < maxWaitMs)
                {
                    await Task.Delay(100);
                    waitedMs += 100;

                    var currentPlugin = pluginInterface.InstalledPlugins
                        .FirstOrDefault(p => p.InternalName == plugin.InternalName);

                    if (currentPlugin != null)
                    {
                        isDisabled = !currentPlugin.IsLoaded;
                        log.Debug($"Plugin {plugin.Name} state check: IsLoaded={currentPlugin.IsLoaded}, waited={waitedMs}ms");
                    }
                }

                if (!isDisabled)
                {
                    log.Warning($"Plugin {plugin.Name} did not disable within timeout (waited {waitedMs}ms)");
                }
                else
                {
                    log.Info($"Plugin {plugin.Name} disabled successfully (took {waitedMs}ms)");
                }

                await Task.Delay(config.DelayBetweenCommands);
            }

            progress?.Report($"Enabling {toEnable.Count} plugins...");
            foreach (var pluginName in toEnable)
            {
                var plugin = installedPlugins[pluginName];
                log.Info($"Enabling: {plugin.Name} (InternalName: {plugin.InternalName})");
                var cmd = $"/xlenableplugin \"{plugin.InternalName}\"";
                log.Debug($"Executing: {cmd}");
                commandManager.ProcessCommand(cmd);

                var maxWaitMs = 5000;
                var waitedMs = 0;
                var isEnabled = false;
                while (!isEnabled && waitedMs < maxWaitMs)
                {
                    await Task.Delay(100);
                    waitedMs += 100;

                    var currentPlugin = pluginInterface.InstalledPlugins
                        .FirstOrDefault(p => p.InternalName == pluginName);

                    if (currentPlugin != null)
                    {
                        isEnabled = currentPlugin.IsLoaded;
                        log.Debug($"Plugin {plugin.Name} state check: IsLoaded={isEnabled}, waited={waitedMs}ms");
                    }
                }

                if (!isEnabled)
                {
                    log.Warning($"Plugin {plugin.Name} did not enable within timeout (waited {waitedMs}ms)");
                    if (config.ShowNotifications && config.VerboseNotifications)
                    {
                        chatGui.PrintError($"[Preset] Failed to enable {plugin.Name}");
                    }
                }
                else
                {
                    log.Info($"Plugin {plugin.Name} enabled successfully (took {waitedMs}ms)");
                }

                await Task.Delay(config.DelayBetweenCommands);
            }

            config.LastAppliedPresetId = preset.Id;
            pluginInterface.SavePluginConfig(config);

            progress?.Report($"Preset '{preset.Name}' applied successfully!");

            if (config.ShowNotifications)
            {
                if (config.VerboseNotifications)
                {
                    chatGui.Print($"[Preset] Applied '{preset.Name}' ({toEnable.Count} enabled, {toDisable.Count} disabled)");
                }
                else
                {
                    chatGui.Print($"[Preset] Applied '{preset.Name}'");
                }
            }

            log.Info($"Successfully applied preset '{preset.Name}'");
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to apply preset '{preset.Name}'");
            chatGui.PrintError($"[Preset] Failed to apply '{preset.Name}': {ex.Message}");
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

            if (config.ShowNotifications)
            {
                chatGui.Print($"[Preset] Added '{internalName}' to always-on list");
            }

            log.Info($"Added '{internalName}' to always-on list");
        }
    }

    public void RemoveAlwaysOnPlugin(string internalName)
    {
        if (alwaysOnPlugins.Remove(internalName))
        {
            storage.SaveAlwaysOnPlugins(alwaysOnPlugins);

            if (config.ShowNotifications)
            {
                chatGui.Print($"[Preset] Removed '{internalName}' from always-on list");
            }

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

            if (config.ShowNotifications)
            {
                chatGui.Print($"[Preset] Deleted preset '{preset.Name}'");
            }
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
