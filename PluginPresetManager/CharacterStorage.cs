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
    private readonly string charactersDirectory;
    private readonly string sharedDataPath;

    private Dictionary<ulong, CharacterData> characters = new();
    private CharacterData? pendingMigrationData;
    private SharedData sharedData = new();

    public SharedData SharedData => sharedData;

    public CharacterStorage(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.log = log;

        baseDirectory = pluginInterface.ConfigDirectory.FullName;
        charactersDirectory = Path.Combine(baseDirectory, "characters");
        sharedDataPath = Path.Combine(baseDirectory, "shared.json");

        Directory.CreateDirectory(charactersDirectory);

        LoadAll();
        LoadSharedData();
        CheckForPendingMigration();

        log.Info($"CharacterStorage initialized at: {baseDirectory}");
    }

    #region Public API

    public CharacterData? GetCharacter(ulong contentId)
    {
        return characters.TryGetValue(contentId, out var data) ? data : null;
    }

    public CharacterData GetOrCreateCharacter(ulong contentId, string name, string world)
    {
        if (characters.TryGetValue(contentId, out var existing))
        {
            var oldFileName = existing.FileName;
            existing.Name = name;
            existing.World = world;
            existing.LastSeen = DateTime.Now;

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

        // Apply pending migration data to this character
        if (pendingMigrationData != null)
        {
            log.Info($"Applying pending migration data to {name}");
            foreach (var preset in pendingMigrationData.Presets)
            {
                newData.Presets.Add(preset);
            }
            foreach (var plugin in pendingMigrationData.AlwaysOn)
            {
                newData.AlwaysOn.Add(plugin);
            }
            newData.NotificationMode = pendingMigrationData.NotificationMode;

            pendingMigrationData = null;
            MarkMigrationComplete();
        }

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
            var filePath = Path.Combine(charactersDirectory, $"{data.FileName}.json");
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
        var sourceData = characters.GetValueOrDefault(sourceContentId);
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
        characters.Clear();
        if (Directory.Exists(charactersDirectory))
        {
            foreach (var file in Directory.GetFiles(charactersDirectory, "*.json"))
            {
                var data = LoadFile(file);
                if (data != null && data.ContentId != 0)
                {
                    characters[data.ContentId] = data;
                }
            }
        }

        log.Info($"Loaded {characters.Count} character(s)");
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

    private void LoadSharedData()
    {
        if (!File.Exists(sharedDataPath))
        {
            sharedData = new SharedData();
            return;
        }

        try
        {
            var json = File.ReadAllText(sharedDataPath);
            sharedData = JsonConvert.DeserializeObject<SharedData>(json) ?? new SharedData();
            log.Info($"Loaded shared data: {sharedData.Presets.Count} presets, {sharedData.AlwaysOn.Count} always-on");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load shared data");
            sharedData = new SharedData();
        }
    }

    public void SaveSharedData()
    {
        try
        {
            var json = JsonConvert.SerializeObject(sharedData, Formatting.Indented);
            File.WriteAllText(sharedDataPath, json);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to save shared data");
        }
    }

    #endregion

    #region Migration

    private void CheckForPendingMigration()
    {
        var migrationMarker = Path.Combine(baseDirectory, "v2_migrated");
        if (File.Exists(migrationMarker)) return;

        log.Info("Checking for data to migrate...");

        pendingMigrationData = new CharacterData();

        MigrateFromCurrentStructure();
        MigrateFromLegacyStructure();

        if (pendingMigrationData.Presets.Count == 0 && pendingMigrationData.AlwaysOn.Count == 0)
        {
            pendingMigrationData = null;
            MarkMigrationComplete();
        }
        else
        {
            log.Info($"Found {pendingMigrationData.Presets.Count} presets and {pendingMigrationData.AlwaysOn.Count} always-on plugins pending migration");
        }
    }

    private void MarkMigrationComplete()
    {
        var migrationMarker = Path.Combine(baseDirectory, "v2_migrated");
        File.WriteAllText(migrationMarker, DateTime.Now.ToString("o"));
        log.Info("Migration complete");
    }

    private void MigrateFromCurrentStructure()
    {
        var globalDir = Path.Combine(baseDirectory, "global");
        var charsDir = Path.Combine(baseDirectory, "characters");
        var charsFile = Path.Combine(baseDirectory, "characters.json");
        var globalFile = Path.Combine(baseDirectory, "global.json");

        // Migrate old global.json to pending
        if (File.Exists(globalFile))
        {
            var globalData = LoadFile(globalFile);
            if (globalData != null)
            {
                foreach (var preset in globalData.Presets)
                {
                    pendingMigrationData!.Presets.Add(preset);
                }
                foreach (var plugin in globalData.AlwaysOn)
                {
                    pendingMigrationData!.AlwaysOn.Add(plugin);
                }
                log.Info("Migrated global.json to pending");
            }
        }

        // Migrate old global folder to pending
        if (Directory.Exists(globalDir))
        {
            var data = MigrateCharacterFolder(globalDir);
            if (data != null)
            {
                foreach (var preset in data.Presets)
                {
                    pendingMigrationData!.Presets.Add(preset);
                }
                foreach (var plugin in data.AlwaysOn)
                {
                    pendingMigrationData!.AlwaysOn.Add(plugin);
                }
                log.Info("Migrated global folder to pending");
            }
        }

        // Load character info from old characters.json
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

        // Migrate character folders (old structure with contentId as folder name)
        if (Directory.Exists(charsDir))
        {
            foreach (var dir in Directory.GetDirectories(charsDir))
            {
                var folderName = Path.GetFileName(dir);
                if (ulong.TryParse(folderName, out var contentId) && contentId != 0)
                {
                    var (name, world) = charInfo.GetValueOrDefault(contentId, ($"Character_{contentId}", ""));
                    var data = MigrateCharacterFolder(dir);
                    if (data != null)
                    {
                        data.ContentId = contentId;
                        data.Name = name;
                        data.World = world;
                        characters[contentId] = data;
                        Save(data);
                        log.Info($"Migrated character: {name}");
                    }
                }
            }
        }
    }

    private CharacterData? MigrateCharacterFolder(string folderPath)
    {
        var presetsDir = Path.Combine(folderPath, "presets");
        var alwaysOnPath = Path.Combine(folderPath, "always-on.json");
        var configPath = Path.Combine(folderPath, "config.json");

        var data = new CharacterData { LastSeen = DateTime.Now };

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

        log.Info("Found legacy structure, adding to pending migration...");

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
                        pendingMigrationData!.Presets.Add(new Preset
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
                var alwaysOn = JsonConvert.DeserializeObject<HashSet<string>>(json);
                if (alwaysOn != null)
                {
                    foreach (var plugin in alwaysOn)
                    {
                        pendingMigrationData!.AlwaysOn.Add(plugin);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Failed to migrate legacy always-on");
            }
        }

        log.Info($"Added {pendingMigrationData!.Presets.Count} presets from legacy structure");
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
