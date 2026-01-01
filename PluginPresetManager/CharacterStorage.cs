using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using PluginPresetManager.Models;

namespace PluginPresetManager;

public class CharacterStorage
{
    private readonly IPluginLog log;
    private readonly string baseDirectory;
    private readonly string globalDirectory;
    private readonly string charactersDirectory;
    private readonly string charactersFilePath;
    private readonly string migratedMarkerPath;

    private Dictionary<ulong, CharacterInfo> characters = new();

    public const string GlobalCharacterName = "Global";
    public const ulong GlobalContentId = 0;

    public CharacterStorage(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.log = log;

        baseDirectory = pluginInterface.ConfigDirectory.FullName;
        globalDirectory = Path.Combine(baseDirectory, "global");
        charactersDirectory = Path.Combine(baseDirectory, "characters");
        charactersFilePath = Path.Combine(baseDirectory, "characters.json");
        migratedMarkerPath = Path.Combine(baseDirectory, "migrated");

        Directory.CreateDirectory(globalDirectory);
        Directory.CreateDirectory(charactersDirectory);

        LoadCharacters();
        MigrateIfNeeded();

        log.Info($"CharacterStorage initialized at: {baseDirectory}");
    }

    public bool NeedsMigration => !File.Exists(migratedMarkerPath);

    private void MigrateIfNeeded()
    {
        if (File.Exists(migratedMarkerPath))
        {
            log.Info("Migration already completed");
            return;
        }

        log.Info("Checking for legacy data to migrate...");

        var legacyPresetsDir = Path.Combine(baseDirectory, "presets");
        var legacyAlwaysOnPath = Path.Combine(baseDirectory, "always-on.json");

        var hasLegacyData = Directory.Exists(legacyPresetsDir) || File.Exists(legacyAlwaysOnPath);

        if (hasLegacyData)
        {
            log.Info("Found legacy data, migrating to global folder...");

            var globalPresetsDir = Path.Combine(globalDirectory, "presets");
            Directory.CreateDirectory(globalPresetsDir);

            // Move presets
            if (Directory.Exists(legacyPresetsDir))
            {
                foreach (var file in Directory.GetFiles(legacyPresetsDir, "*.json"))
                {
                    var destPath = Path.Combine(globalPresetsDir, Path.GetFileName(file));
                    if (!File.Exists(destPath))
                    {
                        File.Copy(file, destPath);
                        log.Info($"Migrated preset: {Path.GetFileName(file)}");
                    }
                }
            }

            // Move always-on
            if (File.Exists(legacyAlwaysOnPath))
            {
                var destPath = Path.Combine(globalDirectory, "always-on.json");
                if (!File.Exists(destPath))
                {
                    File.Copy(legacyAlwaysOnPath, destPath);
                    log.Info("Migrated always-on.json");
                }
            }

            log.Info("Migration to global folder complete");
        }

        // Create migration marker
        File.WriteAllText(migratedMarkerPath, DateTime.Now.ToString("o"));
        log.Info("Migration marker created");
    }

    #region Character Management

    private void LoadCharacters()
    {
        if (!File.Exists(charactersFilePath))
        {
            characters = new Dictionary<ulong, CharacterInfo>();
            return;
        }

        try
        {
            var json = File.ReadAllText(charactersFilePath);
            var list = JsonConvert.DeserializeObject<List<CharacterInfo>>(json);
            characters = list?.ToDictionary(c => c.ContentId, c => c) ?? new Dictionary<ulong, CharacterInfo>();
            log.Info($"Loaded {characters.Count} character(s)");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load characters");
            characters = new Dictionary<ulong, CharacterInfo>();
        }
    }

    private void SaveCharacters()
    {
        try
        {
            var json = JsonConvert.SerializeObject(characters.Values.ToList(), Formatting.Indented);
            File.WriteAllText(charactersFilePath, json);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to save characters");
        }
    }

    public List<CharacterInfo> GetAllCharacters()
    {
        return characters.Values.OrderBy(c => c.Name).ToList();
    }

    public CharacterInfo? GetCharacter(ulong contentId)
    {
        return characters.TryGetValue(contentId, out var info) ? info : null;
    }

    public void RegisterCharacter(ulong contentId, string name, string world)
    {
        if (contentId == 0) return;

        if (characters.TryGetValue(contentId, out var existing))
        {
            existing.Name = name;
            existing.World = world;
            existing.LastSeen = DateTime.Now;
        }
        else
        {
            characters[contentId] = new CharacterInfo
            {
                ContentId = contentId,
                Name = name,
                World = world,
                LastSeen = DateTime.Now
            };
            log.Info($"Registered new character: {name} @ {world}");
        }

        SaveCharacters();
        EnsureCharacterDirectory(contentId);
    }

    public bool HasCharacterData(ulong contentId)
    {
        var charDir = GetCharacterDirectory(contentId);
        if (!Directory.Exists(charDir)) return false;

        var presetsDir = Path.Combine(charDir, "presets");
        var alwaysOnPath = Path.Combine(charDir, "always-on.json");

        return Directory.Exists(presetsDir) && Directory.GetFiles(presetsDir, "*.json").Any()
               || File.Exists(alwaysOnPath);
    }

    #endregion

    #region Directory Management

    public string GetCharacterDirectory(ulong contentId)
    {
        if (contentId == GlobalContentId)
            return globalDirectory;

        return Path.Combine(charactersDirectory, contentId.ToString());
    }

    public string GetPresetsDirectory(ulong contentId)
    {
        return Path.Combine(GetCharacterDirectory(contentId), "presets");
    }

    public string GetAlwaysOnFilePath(ulong contentId)
    {
        return Path.Combine(GetCharacterDirectory(contentId), "always-on.json");
    }

    public string GetConfigFilePath(ulong contentId)
    {
        return Path.Combine(GetCharacterDirectory(contentId), "config.json");
    }

    private void EnsureCharacterDirectory(ulong contentId)
    {
        var charDir = GetCharacterDirectory(contentId);
        var presetsDir = GetPresetsDirectory(contentId);

        Directory.CreateDirectory(charDir);
        Directory.CreateDirectory(presetsDir);
    }

    #endregion

    #region Preset Storage

    public List<Preset> LoadPresets(ulong contentId)
    {
        var presets = new List<Preset>();
        var presetsDir = GetPresetsDirectory(contentId);

        if (!Directory.Exists(presetsDir))
        {
            return presets;
        }

        foreach (var file in Directory.GetFiles(presetsDir, "*.json"))
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

    public void SavePreset(ulong contentId, Preset preset)
    {
        EnsureCharacterDirectory(contentId);

        var presetsDir = GetPresetsDirectory(contentId);
        var fileName = SanitizeFileName(preset.Name) + $"_{preset.Id}.json";
        var filePath = Path.Combine(presetsDir, fileName);

        // Delete old file if name changed
        var existingFiles = Directory.GetFiles(presetsDir, $"*{preset.Id}.json");
        foreach (var oldFile in existingFiles)
        {
            if (oldFile != filePath)
            {
                File.Delete(oldFile);
            }
        }

        var json = JsonConvert.SerializeObject(preset, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    public void DeletePreset(ulong contentId, Preset preset)
    {
        var presetsDir = GetPresetsDirectory(contentId);
        if (!Directory.Exists(presetsDir)) return;

        foreach (var file in Directory.GetFiles(presetsDir, $"*{preset.Id}.json"))
        {
            File.Delete(file);
            log.Info($"Deleted preset file: {Path.GetFileName(file)}");
        }
    }

    #endregion

    #region Always-On Storage

    public HashSet<string> LoadAlwaysOn(ulong contentId)
    {
        var filePath = GetAlwaysOnFilePath(contentId);

        if (!File.Exists(filePath))
        {
            return new HashSet<string>();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<HashSet<string>>(json) ?? new HashSet<string>();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load always-on plugins");
            return new HashSet<string>();
        }
    }

    public void SaveAlwaysOn(ulong contentId, HashSet<string> plugins)
    {
        EnsureCharacterDirectory(contentId);

        var filePath = GetAlwaysOnFilePath(contentId);
        var json = JsonConvert.SerializeObject(plugins, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    #endregion

    #region Character Config Storage

    public CharacterConfig LoadCharacterConfig(ulong contentId)
    {
        var filePath = GetConfigFilePath(contentId);

        if (!File.Exists(filePath))
        {
            return new CharacterConfig { ContentId = contentId };
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<CharacterConfig>(json) ?? new CharacterConfig { ContentId = contentId };
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load character config");
            return new CharacterConfig { ContentId = contentId };
        }
    }

    public void SaveCharacterConfig(ulong contentId, CharacterConfig config)
    {
        EnsureCharacterDirectory(contentId);

        var filePath = GetConfigFilePath(contentId);
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    #endregion

    #region Copy/Import Operations

    public void CopyFromGlobal(ulong targetContentId)
    {
        CopyCharacterData(GlobalContentId, targetContentId);
    }

    public void CopyCharacterData(ulong sourceContentId, ulong targetContentId)
    {
        if (sourceContentId == targetContentId) return;

        log.Info($"Copying data from {sourceContentId} to {targetContentId}");

        // Copy presets
        var sourcePresets = LoadPresets(sourceContentId);
        foreach (var preset in sourcePresets)
        {
            // Create new preset with new ID to avoid conflicts
            var newPreset = new Preset
            {
                Id = Guid.NewGuid(),
                Name = preset.Name,
                Description = preset.Description,
                EnabledPlugins = new HashSet<string>(preset.EnabledPlugins),
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now
            };
            SavePreset(targetContentId, newPreset);
        }

        // Copy always-on
        var sourceAlwaysOn = LoadAlwaysOn(sourceContentId);
        var targetAlwaysOn = LoadAlwaysOn(targetContentId);
        targetAlwaysOn.UnionWith(sourceAlwaysOn);
        SaveAlwaysOn(targetContentId, targetAlwaysOn);

        // Copy config
        var sourceConfig = LoadCharacterConfig(sourceContentId);
        var targetConfig = new CharacterConfig
        {
            ContentId = targetContentId,
            NotificationMode = sourceConfig.NotificationMode,
            DelayBetweenCommands = sourceConfig.DelayBetweenCommands,
            PluginStateCheckInterval = sourceConfig.PluginStateCheckInterval,
            // Don't copy preset IDs as they're different
        };
        SaveCharacterConfig(targetContentId, targetConfig);

        log.Info($"Copied {sourcePresets.Count} presets and {sourceAlwaysOn.Count} always-on plugins");
    }

    #endregion

    #region Helpers

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "preset" : sanitized;
    }

    #endregion
}
