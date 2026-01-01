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
    private readonly string globalFilePath;
    private readonly string charactersDirectory;

    private CharacterData globalData = new();
    private Dictionary<ulong, CharacterData> characters = new();

    public const ulong GlobalContentId = 0;

    public CharacterStorage(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.log = log;

        baseDirectory = pluginInterface.ConfigDirectory.FullName;
        globalFilePath = Path.Combine(baseDirectory, "global.json");
        charactersDirectory = Path.Combine(baseDirectory, "characters");

        Directory.CreateDirectory(charactersDirectory);

        MigrateIfNeeded();
        LoadAll();

        log.Info($"CharacterStorage initialized at: {baseDirectory}");
    }

    #region Public API

    public CharacterData GetGlobal() => globalData;

    public CharacterData GetCharacter(ulong contentId)
    {
        if (contentId == GlobalContentId)
            return globalData;

        return characters.TryGetValue(contentId, out var data) ? data : null!;
    }

    public CharacterData GetOrCreateCharacter(ulong contentId, string name, string world)
    {
        if (contentId == GlobalContentId)
            return globalData;

        if (characters.TryGetValue(contentId, out var existing))
        {
            var oldFileName = existing.FileName;
            existing.Name = name;
            existing.World = world;
            existing.LastSeen = DateTime.Now;

            // Rename file if name changed
            if (oldFileName != existing.FileName)
            {
                var oldPath = Path.Combine(charactersDirectory, $"{oldFileName}.json");
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }
            }

            Save(existing);
            return existing;
        }

        var newData = new CharacterData
        {
            ContentId = contentId,
            Name = name,
            World = world,
            LastSeen = DateTime.Now
        };
        characters[contentId] = newData;
        Save(newData);
        log.Info($"Created new character data: {name} @ {world}");
        return newData;
    }

    public List<CharacterData> GetAllCharacters()
    {
        return characters.Values
            .OrderBy(c => c.Name)
            .ToList();
    }

    public void Save(CharacterData data)
    {
        try
        {
            string filePath;
            if (data.ContentId == GlobalContentId)
            {
                filePath = globalFilePath;
            }
            else
            {
                filePath = Path.Combine(charactersDirectory, $"{data.FileName}.json");
            }

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to save character data for {data.Name}");
        }
    }

    public void DeleteCharacter(ulong contentId)
    {
        if (contentId == GlobalContentId) return;

        if (characters.TryGetValue(contentId, out var data))
        {
            var filePath = Path.Combine(charactersDirectory, $"{data.FileName}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            characters.Remove(contentId);
            log.Info($"Deleted character data: {data.Name}");
        }
    }

    public Preset? CopyPresetFromCharacter(ulong sourceContentId, string presetName)
    {
        var sourceData = sourceContentId == GlobalContentId ? globalData : characters.GetValueOrDefault(sourceContentId);
        if (sourceData == null) return null;

        var sourcePreset = sourceData.Presets.FirstOrDefault(p => p.Name == presetName);
        if (sourcePreset == null) return null;

        return new Preset
        {
            Name = sourcePreset.Name,
            Description = sourcePreset.Description,
            Plugins = new HashSet<string>(sourcePreset.Plugins),
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now
        };
    }

    #endregion

    #region Loading

    private void LoadAll()
    {
        globalData = LoadFile(globalFilePath) ?? new CharacterData { ContentId = GlobalContentId, Name = "Global" };
        globalData.ContentId = GlobalContentId;

        characters.Clear();
        if (Directory.Exists(charactersDirectory))
        {
            foreach (var file in Directory.GetFiles(charactersDirectory, "*.json"))
            {
                var data = LoadFile(file);
                if (data != null && data.ContentId != GlobalContentId)
                {
                    characters[data.ContentId] = data;
                }
            }
        }

        log.Info($"Loaded global data and {characters.Count} character(s)");
    }

    private CharacterData? LoadFile(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<CharacterData>(json);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to load: {filePath}");
            return null;
        }
    }

    #endregion

    #region Migration

    private void MigrateIfNeeded()
    {
        var migrationMarker = Path.Combine(baseDirectory, "v2_migrated");
        if (File.Exists(migrationMarker)) return;

        log.Info("Checking for data to migrate...");

        MigrateFromCurrentStructure();
        MigrateFromLegacyStructure();

        File.WriteAllText(migrationMarker, DateTime.Now.ToString("o"));
        log.Info("Migration complete");
    }

    private void MigrateFromCurrentStructure()
    {
        var globalDir = Path.Combine(baseDirectory, "global");
        var charsDir = Path.Combine(baseDirectory, "characters");
        var charsFile = Path.Combine(baseDirectory, "characters.json");

        if (Directory.Exists(globalDir))
        {
            var data = MigrateCharacterFolder(globalDir, GlobalContentId, "Global", "");
            if (data != null)
            {
                globalData = data;
                Save(globalData);
                log.Info("Migrated global data from folder structure");
            }
        }

        Dictionary<ulong, (string name, string world)> charInfo = new();
        if (File.Exists(charsFile))
        {
            try
            {
                var json = File.ReadAllText(charsFile);
                var list = JsonConvert.DeserializeObject<List<CharacterInfoLegacy>>(json);
                if (list != null)
                {
                    foreach (var c in list)
                    {
                        charInfo[c.ContentId] = (c.Name, c.World);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Failed to read characters.json for migration");
            }
        }

        if (Directory.Exists(charsDir))
        {
            foreach (var dir in Directory.GetDirectories(charsDir))
            {
                var folderName = Path.GetFileName(dir);
                if (ulong.TryParse(folderName, out var contentId) && contentId != GlobalContentId)
                {
                    var (name, world) = charInfo.GetValueOrDefault(contentId, ($"Character_{contentId}", ""));
                    var data = MigrateCharacterFolder(dir, contentId, name, world);
                    if (data != null)
                    {
                        characters[contentId] = data;
                        Save(data);
                        log.Info($"Migrated character: {name}");
                    }
                }
            }
        }
    }

    private CharacterData? MigrateCharacterFolder(string folderPath, ulong contentId, string name, string world)
    {
        var presetsDir = Path.Combine(folderPath, "presets");
        var alwaysOnPath = Path.Combine(folderPath, "always-on.json");
        var configPath = Path.Combine(folderPath, "config.json");

        var data = new CharacterData
        {
            ContentId = contentId,
            Name = name,
            World = world,
            LastSeen = DateTime.Now
        };

        if (Directory.Exists(presetsDir))
        {
            foreach (var file in Directory.GetFiles(presetsDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var oldPreset = JsonConvert.DeserializeObject<PresetLegacy>(json);
                    if (oldPreset != null)
                    {
                        data.Presets.Add(new Preset
                        {
                            Name = oldPreset.Name,
                            Description = oldPreset.Description ?? "",
                            Plugins = oldPreset.EnabledPlugins ?? new HashSet<string>(),
                            CreatedAt = oldPreset.CreatedAt,
                            LastModified = oldPreset.LastModified
                        });
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(ex, $"Failed to migrate preset: {file}");
                }
            }
        }

        if (File.Exists(alwaysOnPath))
        {
            try
            {
                var json = File.ReadAllText(alwaysOnPath);
                data.AlwaysOn = JsonConvert.DeserializeObject<HashSet<string>>(json) ?? new HashSet<string>();
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Failed to migrate always-on");
            }
        }

        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<CharacterConfigLegacy>(json);
                if (config != null)
                {
                    if (config.DefaultPresetId.HasValue)
                    {
                        data.DefaultPreset = FindPresetNameByGuid(presetsDir, config.DefaultPresetId.Value, data.Presets);
                    }
                    if (config.LastAppliedPresetId.HasValue)
                    {
                        data.LastAppliedPreset = FindPresetNameByGuid(presetsDir, config.LastAppliedPresetId.Value, data.Presets);
                    }
                    data.NotificationMode = config.NotificationMode;
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Failed to migrate config");
            }
        }

        return data.Presets.Count > 0 || data.AlwaysOn.Count > 0 ? data : null;
    }

    private string? FindPresetNameByGuid(string presetsDir, Guid id, List<Preset> presets)
    {
        if (!Directory.Exists(presetsDir)) return null;

        foreach (var file in Directory.GetFiles(presetsDir, $"*{id}.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var oldPreset = JsonConvert.DeserializeObject<PresetLegacy>(json);
                if (oldPreset?.Id == id)
                {
                    return oldPreset.Name;
                }
            }
            catch { }
        }

        return null;
    }

    private void MigrateFromLegacyStructure()
    {
        var legacyPresetsDir = Path.Combine(baseDirectory, "presets");
        var legacyAlwaysOnPath = Path.Combine(baseDirectory, "always-on.json");

        if (!Directory.Exists(legacyPresetsDir) && !File.Exists(legacyAlwaysOnPath))
            return;

        log.Info("Found legacy structure, migrating to global...");

        if (globalData.Presets.Count > 0 || globalData.AlwaysOn.Count > 0)
        {
            log.Info("Global already has data, skipping legacy migration");
            return;
        }

        if (Directory.Exists(legacyPresetsDir))
        {
            foreach (var file in Directory.GetFiles(legacyPresetsDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var oldPreset = JsonConvert.DeserializeObject<PresetLegacy>(json);
                    if (oldPreset != null)
                    {
                        globalData.Presets.Add(new Preset
                        {
                            Name = oldPreset.Name,
                            Description = oldPreset.Description ?? "",
                            Plugins = oldPreset.EnabledPlugins ?? new HashSet<string>(),
                            CreatedAt = oldPreset.CreatedAt,
                            LastModified = oldPreset.LastModified
                        });
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(ex, $"Failed to migrate legacy preset: {file}");
                }
            }
        }

        if (File.Exists(legacyAlwaysOnPath))
        {
            try
            {
                var json = File.ReadAllText(legacyAlwaysOnPath);
                globalData.AlwaysOn = JsonConvert.DeserializeObject<HashSet<string>>(json) ?? new HashSet<string>();
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Failed to migrate legacy always-on");
            }
        }

        if (globalData.Presets.Count > 0 || globalData.AlwaysOn.Count > 0)
        {
            Save(globalData);
            log.Info($"Migrated {globalData.Presets.Count} presets and {globalData.AlwaysOn.Count} always-on plugins from legacy structure");
        }
    }

    #endregion

    #region Legacy Models for Migration

    private class PresetLegacy
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public HashSet<string>? EnabledPlugins { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
    }

    private class CharacterConfigLegacy
    {
        public Guid? DefaultPresetId { get; set; }
        public Guid? LastAppliedPresetId { get; set; }
        public NotificationMode NotificationMode { get; set; }
    }

    private class CharacterInfoLegacy
    {
        public ulong ContentId { get; set; }
        public string Name { get; set; } = "";
        public string World { get; set; } = "";
    }

    #endregion
}
