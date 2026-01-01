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
    private const int DelayBetweenCommands = 50;
    private const int PluginStateCheckInterval = 500;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IChatGui chatGui;
    private readonly INotificationManager notificationManager;
    private readonly IPluginLog log;
    private readonly Configuration globalConfig;
    private readonly CharacterStorage storage;
    private readonly DalamudReflectionHelper reflectionHelper;

    private CharacterData currentData;

    public bool IsApplying { get; private set; }
    public string ApplyingStatus { get; private set; } = string.Empty;
    public float ApplyingProgress { get; private set; }

    public ulong CurrentCharacterId => currentData.ContentId;
    public CharacterData CurrentData => currentData;

    public PresetManager(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        INotificationManager notificationManager,
        IPluginLog log,
        Configuration globalConfig,
        CharacterStorage storage)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.chatGui = chatGui;
        this.notificationManager = notificationManager;
        this.log = log;
        this.globalConfig = globalConfig;
        this.storage = storage;

        reflectionHelper = new DalamudReflectionHelper(pluginInterface, log);
        if (globalConfig.UseExperimentalPersistence)
        {
            reflectionHelper.TryInitialize();
        }

        currentData = storage.GetGlobal();
    }

    #region Character Switching

    public void SwitchCharacter(ulong contentId, string? name = null, string? world = null)
    {
        if (contentId == CharacterStorage.GlobalContentId)
        {
            currentData = storage.GetGlobal();
        }
        else if (name != null)
        {
            currentData = storage.GetOrCreateCharacter(contentId, name, world ?? "");
        }
        else
        {
            var existing = storage.GetCharacter(contentId);
            if (existing != null)
            {
                currentData = existing;
            }
            else
            {
                currentData = storage.GetGlobal();
            }
        }

        globalConfig.LastSelectedCharacterId = contentId;
        log.Info($"Switched to: {currentData.DisplayName}");
    }

    public List<CharacterData> GetAllCharacters() => storage.GetAllCharacters();

    public void DeleteCharacter(ulong contentId) => storage.DeleteCharacter(contentId);

    #endregion

    #region Preset Management

    public List<Preset> GetAllPresets() => currentData.Presets;

    public Preset? GetPresetByName(string name)
    {
        return currentData.Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public Preset? GetLastAppliedPreset()
    {
        if (string.IsNullOrEmpty(currentData.LastAppliedPreset))
            return null;
        return GetPresetByName(currentData.LastAppliedPreset);
    }

    public void AddPreset(Preset preset)
    {
        // Ensure unique name
        var baseName = preset.Name;
        var counter = 1;
        while (currentData.Presets.Any(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase)))
        {
            preset.Name = $"{baseName} ({counter++})";
        }

        currentData.Presets.Add(preset);
        Save();
        log.Info($"Added preset: {preset.Name}");
    }

    public void UpdatePreset(Preset preset)
    {
        preset.LastModified = DateTime.Now;
        Save();
        log.Info($"Updated preset: {preset.Name}");
    }

    public void DeletePreset(Preset preset)
    {
        if (currentData.Presets.Remove(preset))
        {
            if (currentData.DefaultPreset == preset.Name)
                currentData.DefaultPreset = null;
            if (currentData.LastAppliedPreset == preset.Name)
                currentData.LastAppliedPreset = null;

            Save();
            ShowNotification($"Deleted preset '{preset.Name}'");
            log.Info($"Deleted preset: {preset.Name}");
        }
    }

    public Preset DuplicatePreset(Preset source)
    {
        var duplicate = new Preset
        {
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            Plugins = new HashSet<string>(source.Plugins),
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now
        };

        AddPreset(duplicate);
        ShowNotification($"Duplicated preset '{source.Name}'");
        return duplicate;
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
            if (plugin.IsLoaded && !currentData.AlwaysOn.Contains(plugin.InternalName))
            {
                preset.Plugins.Add(plugin.InternalName);
            }
        }

        log.Info($"Created preset '{name}' with {preset.Plugins.Count} plugins");
        return preset;
    }

    public Preset? ImportPresetFromCharacter(ulong sourceContentId, string presetName)
    {
        var preset = storage.CopyPresetFromCharacter(sourceContentId, presetName);
        if (preset != null)
        {
            AddPreset(preset);
            ShowNotification($"Imported preset '{preset.Name}'");
        }
        return preset;
    }

    #endregion

    #region Always-On Management

    public HashSet<string> GetAlwaysOnPlugins() => currentData.AlwaysOn;

    public bool IsAlwaysOn(string internalName) => currentData.AlwaysOn.Contains(internalName);

    public void AddAlwaysOnPlugin(string internalName)
    {
        if (currentData.AlwaysOn.Add(internalName))
        {
            Save();

            var plugin = pluginInterface.InstalledPlugins
                .FirstOrDefault(p => p.InternalName == internalName);

            if (plugin != null && !plugin.IsLoaded)
            {
                commandManager.ProcessCommand($"/xlenableplugin \"{plugin.Name}\"");
            }

            ShowNotification($"Added '{internalName}' to always-on");
            log.Info($"Added always-on: {internalName}");
        }
    }

    public void RemoveAlwaysOnPlugin(string internalName)
    {
        if (currentData.AlwaysOn.Remove(internalName))
        {
            Save();
            ShowNotification($"Removed '{internalName}' from always-on");
            log.Info($"Removed always-on: {internalName}");
        }
    }

    #endregion

    #region Default Preset

    public string? DefaultPreset => currentData.DefaultPreset;

    public void SetDefaultPreset(string? presetName)
    {
        currentData.DefaultPreset = presetName;
        Save();
    }

    #endregion

    #region Apply Presets

    public async Task ApplyAlwaysOnOnlyAsync(IProgress<string>? progress = null)
    {
        if (IsApplying) return;

        IsApplying = true;
        ApplyingStatus = "Preparing...";
        ApplyingProgress = 0;

        try
        {
            progress?.Report("Applying always-on plugins only...");
            log.Info("Starting always-on only mode");

            var installedPlugins = pluginInterface.InstalledPlugins
                .GroupBy(p => p.InternalName)
                .ToDictionary(g => g.Key, g => g.First());

            var effectiveEnabledSet = new HashSet<string>(currentData.AlwaysOn);

            var toDisable = installedPlugins.Values
                .Where(p => p.IsLoaded && !effectiveEnabledSet.Contains(p.InternalName))
                .ToList();

            var toEnable = effectiveEnabledSet
                .Where(name => installedPlugins.ContainsKey(name) && !installedPlugins[name].IsLoaded)
                .ToList();

            await ApplyChangesAsync(toDisable, toEnable, installedPlugins, progress);

            currentData.LastAppliedPreset = null;
            Save();

            ShowNotification($"Applied always-on only mode ({toEnable.Count} enabled, {toDisable.Count} disabled)");
            log.Info($"Applied always-on only: {toEnable.Count} enabled, {toDisable.Count} disabled");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to apply always-on only mode");
            ShowNotification($"Failed: {ex.Message}", true);
            throw;
        }
        finally
        {
            IsApplying = false;
            ApplyingStatus = string.Empty;
            ApplyingProgress = 0;
        }
    }

    public async Task ApplyPresetAsync(Preset preset, IProgress<string>? progress = null)
    {
        if (IsApplying) return;

        IsApplying = true;
        ApplyingStatus = "Preparing...";
        ApplyingProgress = 0;

        try
        {
            progress?.Report("Validating preset...");
            log.Info($"Applying preset: {preset.Name}");

            var installedPlugins = pluginInterface.InstalledPlugins
                .GroupBy(p => p.InternalName)
                .ToDictionary(g => g.Key, g => g.First());

            var effectiveEnabledSet = new HashSet<string>(preset.Plugins);
            foreach (var alwaysOn in currentData.AlwaysOn)
            {
                if (installedPlugins.ContainsKey(alwaysOn))
                    effectiveEnabledSet.Add(alwaysOn);
            }

            var toDisable = installedPlugins.Values
                .Where(p => p.IsLoaded && !effectiveEnabledSet.Contains(p.InternalName))
                .ToList();

            var toEnable = effectiveEnabledSet
                .Where(name => installedPlugins.ContainsKey(name) && !installedPlugins[name].IsLoaded)
                .ToList();

            await ApplyChangesAsync(toDisable, toEnable, installedPlugins, progress);

            currentData.LastAppliedPreset = preset.Name;
            Save();

            ShowNotification($"Applied '{preset.Name}' ({toEnable.Count} enabled, {toDisable.Count} disabled)");
            log.Info($"Applied preset '{preset.Name}': {toEnable.Count} enabled, {toDisable.Count} disabled");
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to apply preset '{preset.Name}'");
            ShowNotification($"Failed: {ex.Message}", true);
            throw;
        }
        finally
        {
            IsApplying = false;
            ApplyingStatus = string.Empty;
            ApplyingProgress = 0;
        }
    }

    private async Task ApplyChangesAsync(
        List<IExposedPlugin> toDisable,
        List<string> toEnable,
        Dictionary<string, IExposedPlugin> installedPlugins,
        IProgress<string>? progress)
    {
        var total = toDisable.Count + toEnable.Count;
        var current = 0;

        progress?.Report($"Disabling {toDisable.Count} plugins...");
        foreach (var plugin in toDisable)
        {
            current++;
            ApplyingProgress = (float)current / total;
            ApplyingStatus = $"Disabling {plugin.Name}...";
            await DisablePluginAsync(plugin);
            await WaitForPluginState(plugin.InternalName, false);
            await Task.Delay(DelayBetweenCommands);
        }

        progress?.Report($"Enabling {toEnable.Count} plugins...");
        foreach (var pluginName in toEnable)
        {
            current++;
            ApplyingProgress = (float)current / total;
            var plugin = installedPlugins[pluginName];
            ApplyingStatus = $"Enabling {plugin.Name}...";
            await EnablePluginAsync(plugin);
            await WaitForPluginState(pluginName, true);
            await Task.Delay(DelayBetweenCommands);
        }
    }

    private async Task WaitForPluginState(string internalName, bool expectedLoaded)
    {
        var maxWaitMs = 30000;
        var waitedMs = 0;

        while (waitedMs < maxWaitMs)
        {
            await Task.Delay(PluginStateCheckInterval);
            waitedMs += PluginStateCheckInterval;

            var plugin = pluginInterface.InstalledPlugins.FirstOrDefault(p => p.InternalName == internalName);
            if (plugin != null && plugin.IsLoaded == expectedLoaded)
                return;
        }

        log.Warning($"Plugin {internalName} did not reach expected state within timeout");
    }

    #endregion

    #region Preview

    public PresetPreview GetPresetPreview(Preset preset)
    {
        var preview = new PresetPreview();
        var installedPlugins = pluginInterface.InstalledPlugins
            .GroupBy(p => p.InternalName)
            .ToDictionary(g => g.Key, g => g.First());

        var effectiveEnabledSet = new HashSet<string>(preset.Plugins);
        effectiveEnabledSet.UnionWith(currentData.AlwaysOn);

        foreach (var plugin in installedPlugins.Values)
        {
            var shouldBeEnabled = effectiveEnabledSet.Contains(plugin.InternalName);
            var isAlwaysOn = currentData.AlwaysOn.Contains(plugin.InternalName);

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

    public List<string> GetMissingPlugins(Preset preset)
    {
        var installed = pluginInterface.InstalledPlugins
            .Select(p => p.InternalName)
            .ToHashSet();

        return preset.Plugins.Where(p => !installed.Contains(p)).ToList();
    }

    #endregion

    #region Helpers

    private void Save()
    {
        storage.Save(currentData);
    }

    private async Task EnablePluginAsync(IExposedPlugin plugin)
    {
        if (globalConfig.UseExperimentalPersistence && reflectionHelper.TryInitialize())
        {
            var success = await reflectionHelper.SetPluginStateAsync(plugin, true);
            if (success) return;
            log.Warning($"Fallback to command for {plugin.Name}");
        }

        commandManager.ProcessCommand($"/xlenableplugin \"{plugin.Name}\"");
    }

    private async Task DisablePluginAsync(IExposedPlugin plugin)
    {
        if (globalConfig.UseExperimentalPersistence && reflectionHelper.TryInitialize())
        {
            var success = await reflectionHelper.SetPluginStateAsync(plugin, false);
            if (success) return;
            log.Warning($"Fallback to command for {plugin.Name}");
        }

        commandManager.ProcessCommand($"/xldisableplugin \"{plugin.Name}\"");
    }

    private void ShowNotification(string message, bool isError = false)
    {
        switch (currentData.NotificationMode)
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

    #endregion
}
