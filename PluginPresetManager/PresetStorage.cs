using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using PluginPresetManager.Models;

namespace PluginPresetManager;

public class PresetStorage
{
    private readonly IPluginLog log;
    private readonly string storageDirectory;
    private readonly string presetsDirectory;
    private readonly string alwaysOnFilePath;

    public PresetStorage(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.log = log;

        storageDirectory = pluginInterface.ConfigDirectory.FullName;
        presetsDirectory = Path.Combine(storageDirectory, "presets");
        alwaysOnFilePath = Path.Combine(storageDirectory, "always-on.json");

        Directory.CreateDirectory(presetsDirectory);

        log.Info($"PresetStorage initialized at: {storageDirectory}");
    }

    #region Preset Management

    public List<Preset> LoadAllPresets()
    {
        var presets = new List<Preset>();

        if (!Directory.Exists(presetsDirectory))
        {
            log.Info("Presets directory doesn't exist, returning empty list");
            return presets;
        }

        var files = Directory.GetFiles(presetsDirectory, "*.json");
        log.Info($"Found {files.Length} preset files");

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var preset = JsonConvert.DeserializeObject<Preset>(json);

                if (preset != null)
                {
                    presets.Add(preset);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to load preset from {file}");
            }
        }

        return presets.OrderBy(p => p.Name).ToList();
    }

    private string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "preset" : sanitized;
    }

    private string GetPresetFileName(Preset preset)
    {
        var sanitizedName = SanitizeFileName(preset.Name);
        return $"{sanitizedName}_{preset.Id}.json";
    }

    public void SavePreset(Preset preset)
    {
        try
        {
            var newFileName = GetPresetFileName(preset);
            var newFilePath = Path.Combine(presetsDirectory, newFileName);

            var existingFiles = Directory.GetFiles(presetsDirectory, $"*{preset.Id}.json");
            var existingFile = existingFiles.FirstOrDefault();

            if (existingFile != null && existingFile != newFilePath)
            {
                File.Delete(existingFile);
                log.Info($"Deleted old preset file: {Path.GetFileName(existingFile)}");
            }

            var json = JsonConvert.SerializeObject(preset, Formatting.Indented);
            File.WriteAllText(newFilePath, json);

            log.Info($"Saved preset '{preset.Name}' to {newFileName}");
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to save preset '{preset.Name}'");
            throw;
        }
    }

    public void DeletePreset(Preset preset)
    {
        try
        {
            var files = Directory.GetFiles(presetsDirectory, $"*{preset.Id}.json");

            foreach (var file in files)
            {
                File.Delete(file);
                log.Info($"Deleted preset '{preset.Name}' from disk");
            }

            if (files.Length == 0)
            {
                log.Warning($"No file found for preset '{preset.Name}' (ID: {preset.Id})");
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to delete preset '{preset.Name}'");
            throw;
        }
    }

    #endregion

    #region Always-On Plugins

    public HashSet<string> LoadAlwaysOnPlugins()
    {
        if (!File.Exists(alwaysOnFilePath))
        {
            log.Info("Always-on file doesn't exist, returning empty set");
            return new HashSet<string>();
        }

        try
        {
            var json = File.ReadAllText(alwaysOnFilePath);
            var plugins = JsonConvert.DeserializeObject<HashSet<string>>(json);

            if (plugins != null)
            {
                log.Info($"Loaded {plugins.Count} always-on plugins");
                return plugins;
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load always-on plugins");
        }

        return new HashSet<string>();
    }

    public void SaveAlwaysOnPlugins(HashSet<string> plugins)
    {
        try
        {
            var json = JsonConvert.SerializeObject(plugins, Formatting.Indented);
            File.WriteAllText(alwaysOnFilePath, json);

            log.Info($"Saved {plugins.Count} always-on plugins");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to save always-on plugins");
            throw;
        }
    }

    #endregion
}
