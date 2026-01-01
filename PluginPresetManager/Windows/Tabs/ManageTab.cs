using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Newtonsoft.Json;
using PluginPresetManager.Models;
using PluginPresetManager.UI;

namespace PluginPresetManager.Windows.Tabs;

public class ManageTab
{
    private readonly Plugin plugin;
    private readonly PresetManager presetManager;

    private string newPresetName = string.Empty;
    private Preset? selectedPreset;
    private string searchFilter = string.Empty;
    private string importError = string.Empty;
    private bool showAlwaysOn = false;
    private bool showImportFromCharacter = false;
    private ulong importSourceCharacterId = 0;

    private Preset? presetToDelete = null;
    private bool openDeleteModal = false;

    private Dictionary<string, IExposedPlugin>? cachedPlugins;
    private int lastPluginCount = -1;

    public ManageTab(Plugin plugin, PresetManager presetManager)
    {
        this.plugin = plugin;
        this.presetManager = presetManager;
    }

    private CharacterData Data => presetManager.CurrentData;

    private Dictionary<string, IExposedPlugin> GetInstalledPlugins()
    {
        var currentCount = Plugin.PluginInterface.InstalledPlugins.Count();
        if (cachedPlugins == null || lastPluginCount != currentCount)
        {
            cachedPlugins = new Dictionary<string, IExposedPlugin>();
            foreach (var p in Plugin.PluginInterface.InstalledPlugins)
            {
                var key = GetPluginKey(p);
                cachedPlugins[key] = p;
            }
            lastPluginCount = currentCount;
        }
        return cachedPlugins;
    }

    private static string GetPluginKey(IExposedPlugin plugin)
    {
        return plugin.IsDev ? $"{plugin.InternalName}#dev" : plugin.InternalName;
    }

    private static bool IsDevKey(string key) => key.EndsWith("#dev");

    private static string GetInternalNameFromKey(string key)
    {
        return key.EndsWith("#dev") ? key[..^4] : key;
    }

    public void Draw()
    {
        if (!presetManager.HasCharacter)
        {
            ImGui.TextColored(Colors.Warning, "Please log in to a character to manage presets.");
            return;
        }

        DrawTopBar();

        ImGui.Separator();
        ImGui.Spacing();

        using (var leftChild = ImRaii.Child("LeftPanel", new Vector2(Sizing.LeftPanelWidth, 0), true))
        {
            if (leftChild)
                DrawLeftPanel();
        }

        ImGui.SameLine();

        using (var rightChild = ImRaii.Child("RightPanel", new Vector2(0, 0), true))
        {
            if (rightChild)
                DrawRightPanel();
        }

        DrawDeleteConfirmation();
    }

    private void DrawDeleteConfirmation()
    {
        if (openDeleteModal && presetToDelete != null)
        {
            UIHelpers.OpenConfirmationModal("DeletePreset", "Delete Preset");
            openDeleteModal = false;
        }

        if (presetToDelete != null)
        {
            var result = UIHelpers.ConfirmationModal(
                "DeletePreset",
                "Delete Preset",
                $"Are you sure you want to delete '{presetToDelete.Name}'?\n\nThis cannot be undone.");

            if (result == true)
            {
                presetManager.DeletePreset(presetToDelete);
                if (selectedPreset?.Name == presetToDelete.Name)
                    selectedPreset = null;
                presetToDelete = null;
            }
            else if (result == false)
            {
                presetToDelete = null;
            }
        }
    }

    private void DrawTopBar()
    {
        ImGui.SetNextItemWidth(Sizing.InputMedium);
        ImGui.InputTextWithHint("##NewPreset", "New preset name...", ref newPresetName, 100);

        ImGui.SameLine();
        if (UIHelpers.IconButton(FontAwesomeIcon.Plus, "Create", "Create empty preset", Sizing.ButtonSmall))
        {
            CreatePreset();
        }

        ImGui.SameLine();
        if (UIHelpers.IconButton(FontAwesomeIcon.Save, "AddEnabled", "Add currently enabled plugins as preset", Sizing.ButtonSmall))
        {
            CreatePresetFromCurrent();
        }

        ImGui.SameLine();
        if (UIHelpers.IconButton(FontAwesomeIcon.FileImport, "Import", "Import from clipboard", Sizing.ButtonSmall))
        {
            ImportPresetFromClipboard();
        }

        ImGui.SameLine();
        if (UIHelpers.IconButton(FontAwesomeIcon.UserFriends, "FromChar", "Import from another character", Sizing.ButtonSmall))
        {
            showImportFromCharacter = !showImportFromCharacter;
            importSourceCharacterId = 0;
        }

        if (!string.IsNullOrEmpty(importError))
        {
            ImGui.SameLine();
            ImGui.TextColored(Colors.Error, importError);
        }

        if (showImportFromCharacter)
        {
            DrawImportFromCharacterPopup();
        }

        ImGui.Spacing();
    }

    private void DrawImportFromCharacterPopup()
    {
        ImGui.Spacing();
        using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.15f, 0.15f, 0.15f, 1f)))
        using (var child = ImRaii.Child("ImportFromChar", new Vector2(0, 120), true))
        {
            if (!child) return;

            ImGui.Text("Import preset from:");
            ImGui.SameLine();

            var sources = presetManager.GetAllCharacters()
                .Where(c => c.ContentId != presetManager.CurrentCharacterId)
                .Select(c => (c.DisplayName, c.ContentId))
                .ToList();

            if (sources.Count == 0)
            {
                ImGui.TextColored(Colors.TextMuted, "No other characters available");
                if (ImGui.Button("Close"))
                {
                    showImportFromCharacter = false;
                }
                return;
            }

            if (!sources.Any(s => s.ContentId == importSourceCharacterId))
            {
                importSourceCharacterId = sources[0].ContentId;
            }

            var currentSourceName = sources.First(s => s.ContentId == importSourceCharacterId).DisplayName;

            ImGui.SetNextItemWidth(150);
            if (ImGui.BeginCombo("##SourceChar", currentSourceName))
            {
                foreach (var (name, id) in sources)
                {
                    if (ImGui.Selectable(name, id == importSourceCharacterId))
                    {
                        importSourceCharacterId = id;
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();

            var sourceData = plugin.CharacterStorage.GetCharacter(importSourceCharacterId);

            if (sourceData != null && sourceData.Presets.Count > 0)
            {
                ImGui.Text("Presets:");
                using (var presetChild = ImRaii.Child("PresetList", new Vector2(0, 0), false))
                {
                    if (presetChild)
                    {
                        foreach (var preset in sourceData.Presets)
                        {
                            if (ImGui.Button($"Import##{preset.Name}"))
                            {
                                presetManager.ImportPresetFromCharacter(importSourceCharacterId, preset.Name);
                                showImportFromCharacter = false;
                            }
                            ImGui.SameLine();
                            ImGui.Text($"{preset.Name} ({preset.Plugins.Count} plugins)");
                        }
                    }
                }
            }
            else
            {
                ImGui.TextColored(Colors.TextMuted, "No presets available");
            }
        }
    }

    private void CreatePreset()
    {
        if (string.IsNullOrWhiteSpace(newPresetName)) return;

        var newPreset = new Preset
        {
            Name = newPresetName,
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now
        };
        presetManager.AddPreset(newPreset);
        selectedPreset = newPreset;
        newPresetName = string.Empty;
        showAlwaysOn = false;
    }

    private void CreatePresetFromCurrent()
    {
        var name = string.IsNullOrWhiteSpace(newPresetName) ? "New Preset" : newPresetName;
        var newPreset = presetManager.CreatePresetFromCurrent(name);
        presetManager.AddPreset(newPreset);
        selectedPreset = newPreset;
        newPresetName = string.Empty;
        showAlwaysOn = false;
    }

    private void DrawLeftPanel()
    {
        UIHelpers.SectionHeader("Presets", FontAwesomeIcon.LayerGroup);

        foreach (var preset in presetManager.GetAllPresets())
        {
            var isSelected = selectedPreset?.Name == preset.Name && !showAlwaysOn;
            var isDefault = Data.DefaultPreset == preset.Name;

            if (isDefault)
            {
                using (ImRaii.PushFont(UiBuilder.IconFont))
                {
                    ImGui.TextColored(Colors.Star, FontAwesomeIcon.Star.ToIconString());
                }
                ImGui.SameLine();
            }

            var label = $"{preset.Name} ({preset.Plugins.Count})";
            if (ImGui.Selectable(label, isSelected))
            {
                selectedPreset = preset;
                showAlwaysOn = false;
            }

            DrawPresetContextMenu(preset);
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);

        var alwaysOnPlugins = presetManager.GetAlwaysOnPlugins();
        UIHelpers.SectionHeader($"Always-On ({alwaysOnPlugins.Count})", FontAwesomeIcon.Lock);

        if (presetManager.UseAlwaysOnAsDefault)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.TextColored(Colors.Star, FontAwesomeIcon.Star.ToIconString());
            }
            ImGui.SameLine();
        }

        if (ImGui.Selectable("Always-On...", showAlwaysOn))
        {
            showAlwaysOn = true;
            selectedPreset = null;
        }
    }

    private void DrawPresetContextMenu(Preset preset)
    {
        using var ctx = ImRaii.ContextPopupItem($"PresetCtx_{preset.Name}");
        if (!ctx) return;

        if (ImGui.MenuItem("Duplicate"))
        {
            selectedPreset = presetManager.DuplicatePreset(preset);
        }
        if (ImGui.MenuItem("Export to Clipboard"))
        {
            ExportPresetToClipboard(preset);
        }
        ImGui.Separator();
        if (ImGui.MenuItem("Delete"))
        {
            presetToDelete = preset;
            openDeleteModal = true;
        }
    }

    private void DrawRightPanel()
    {
        if (showAlwaysOn)
            DrawAlwaysOnEditor();
        else if (selectedPreset != null)
            DrawPresetEditor(selectedPreset);
        else
            DrawEmptyState();
    }

    private void DrawEmptyState()
    {
        UIHelpers.EmptyState(FontAwesomeIcon.MousePointer, "Select a preset to edit");
    }

    private void DrawPresetEditor(Preset preset)
    {
        ImGui.Text("Name");
        ImGui.SetNextItemWidth(-1);
        var name = preset.Name;
        if (ImGui.InputText("##PresetName", ref name, 100))
        {
            preset.Name = name;
            presetManager.UpdatePreset(preset);
        }

        ImGui.Spacing();

        ImGui.Text("Description");
        ImGui.SetNextItemWidth(-1);
        var desc = preset.Description;
        if (ImGui.InputTextMultiline("##PresetDesc", ref desc, 500, new Vector2(-1, 40)))
        {
            preset.Description = desc;
            presetManager.UpdatePreset(preset);
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        DrawPresetActions(preset);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawPresetPluginList(preset);
    }

    private void DrawPresetActions(Preset preset)
    {
        var isDefault = Data.DefaultPreset == preset.Name;

        if (isDefault)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.1f, 1f)))
            {
                if (ImGui.Button("★ Default", new Vector2(Sizing.ButtonLarge, 0)))
                {
                    presetManager.SetDefaultPreset(null);
                }
            }
        }
        else
        {
            if (ImGui.Button("Set Default", new Vector2(Sizing.ButtonLarge, 0)))
            {
                presetManager.SetDefaultPreset(preset.Name);
            }
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(isDefault ? "Click to unset" : "Auto-apply on login");
        }

        ImGui.SameLine();
        if (ImGui.Button("Duplicate", new Vector2(Sizing.ButtonMedium, 0)))
        {
            selectedPreset = presetManager.DuplicatePreset(preset);
        }

        ImGui.SameLine();
        if (ImGui.Button("Export", new Vector2(Sizing.ButtonMedium, 0)))
        {
            ExportPresetToClipboard(preset);
        }

        ImGui.SameLine();
        if (ImGui.Button("Add Current", new Vector2(Sizing.ButtonLarge, 0)))
        {
            AddCurrentlyEnabledPlugins(preset);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Add all currently enabled plugins");
        }
    }

    private void DrawPresetPluginList(Preset preset)
    {
        ImGui.Text("Plugins");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Sizing.InputMedium);
        ImGui.InputTextWithHint("##PluginSearch", "Search...", ref searchFilter, 100);

        ImGui.Spacing();

        var installedPlugins = GetInstalledPlugins();
        var alwaysOnPlugins = presetManager.GetAlwaysOnPlugins();

        var missingPlugins = preset.Plugins
            .Where(p => !installedPlugins.ContainsKey(p))
            .ToList();

        using var child = ImRaii.Child("PluginList", new Vector2(0, 0), false);
        if (!child) return;

        if (missingPlugins.Count > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.Warning))
            {
                ImGui.Text($"Missing Plugins ({missingPlugins.Count})");
            }
            foreach (var pluginName in missingPlugins)
            {
                ImGui.TextColored(Colors.Error, $"  • {pluginName}");
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        if (alwaysOnPlugins.Count > 0)
        {
            ImGui.TextColored(Colors.TextMuted, "Always-On (included automatically)");
            var anyAlwaysOnShown = false;
            foreach (var pluginName in alwaysOnPlugins.OrderBy(p => p))
            {
                if (!MatchesFilter(pluginName, installedPlugins)) continue;
                anyAlwaysOnShown = true;

                using (ImRaii.Disabled())
                {
                    var check = true;
                    ImGui.Checkbox($"##{pluginName}_ao", ref check);
                }
                ImGui.SameLine();
                if (installedPlugins.TryGetValue(pluginName, out var info))
                {
                    ImGui.TextColored(Colors.TextMuted, info.Name);
                    DrawPluginTags(info);
                }
                else
                {
                    ImGui.TextColored(Colors.TextMuted, $"{pluginName} (not installed)");
                }
            }
            if (anyAlwaysOnShown)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }
        }

        ImGui.TextColored(Colors.Header, "Preset Plugins");

        var anyPluginsShown = false;
        foreach (var (key, p) in installedPlugins.OrderBy(kv => kv.Value.Name).ThenBy(kv => kv.Key))
        {
            if (alwaysOnPlugins.Contains(key) || alwaysOnPlugins.Contains(p.InternalName)) continue;
            if (!MatchesFilter(key, installedPlugins)) continue;

            anyPluginsShown = true;
            var isInPreset = preset.Plugins.Contains(key);
            if (ImGui.Checkbox($"{p.Name}##{key}", ref isInPreset))
            {
                if (isInPreset)
                    preset.Plugins.Add(key);
                else
                    preset.Plugins.Remove(key);
                presetManager.UpdatePreset(preset);
            }

            DrawPluginTags(p);
        }

        if (!anyPluginsShown && !string.IsNullOrEmpty(searchFilter))
        {
            ImGui.Spacing();
            ImGui.TextColored(Colors.TextMuted, "No plugins match your search.");
        }
    }

    private void DrawAlwaysOnEditor()
    {
        UIHelpers.SectionHeader("Always-On Plugins", FontAwesomeIcon.Lock);
        ImGui.TextColored(Colors.TextMuted, "These plugins stay enabled regardless of preset.");

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        var isDefault = presetManager.UseAlwaysOnAsDefault;
        if (isDefault)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.1f, 1f)))
            {
                if (ImGui.Button("★ Default", new Vector2(Sizing.ButtonLarge, 0)))
                {
                    presetManager.SetAlwaysOnAsDefault(false);
                }
            }
        }
        else
        {
            if (ImGui.Button("Set Default", new Vector2(Sizing.ButtonLarge, 0)))
            {
                presetManager.SetAlwaysOnAsDefault(true);
            }
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(isDefault ? "Click to unset" : "Auto-apply on login (disables all except always-on)");
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        ImGui.Text("Search");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Sizing.InputMedium);
        ImGui.InputTextWithHint("##AOSearch", "Filter...", ref searchFilter, 100);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var installedPlugins = GetInstalledPlugins();
        var alwaysOnPlugins = presetManager.GetAlwaysOnPlugins();
        var thisPluginName = Plugin.PluginInterface.InternalName;

        using var child = ImRaii.Child("AlwaysOnList", new Vector2(0, 0), false);
        if (!child) return;

        var anyPluginsShown = false;
        foreach (var (key, p) in installedPlugins.OrderBy(kv => kv.Value.Name).ThenBy(kv => kv.Key))
        {
            if (!MatchesFilter(key, installedPlugins)) continue;

            anyPluginsShown = true;
            var isAlwaysOn = alwaysOnPlugins.Contains(key);
            var isThisPlugin = p.InternalName == thisPluginName && !p.IsDev;

            if (isThisPlugin)
            {
                using (ImRaii.Disabled())
                {
                    var check = true;
                    ImGui.Checkbox($"##{key}_ao", ref check);
                }
                ImGui.SameLine();
                ImGui.Text(p.Name);
                DrawPluginTags(p);
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.Warning))
                {
                    ImGui.Text("(this plugin - always required)");
                }
            }
            else
            {
                if (ImGui.Checkbox($"{p.Name}##{key}_ao", ref isAlwaysOn))
                {
                    if (isAlwaysOn)
                        presetManager.AddAlwaysOnPlugin(key);
                    else
                        presetManager.RemoveAlwaysOnPlugin(key);
                }
                DrawPluginTags(p);
            }
        }

        if (!anyPluginsShown && !string.IsNullOrEmpty(searchFilter))
        {
            ImGui.Spacing();
            ImGui.TextColored(Colors.TextMuted, "No plugins match your search.");
        }
    }

    private static void DrawPluginTags(IExposedPlugin plugin)
    {
        if (plugin.IsDev)
        {
            ImGui.SameLine();
            ImGui.TextColored(Colors.TagDev, "[DEV]");
        }
        if (plugin.IsThirdParty)
        {
            ImGui.SameLine();
            ImGui.TextColored(Colors.TagThirdParty, "[3rd]");
        }
    }

    private bool MatchesFilter(string internalName, Dictionary<string, IExposedPlugin> plugins)
    {
        if (string.IsNullOrEmpty(searchFilter)) return true;

        if (internalName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
            return true;

        if (plugins.TryGetValue(internalName, out var plugin) &&
            plugin.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private void AddCurrentlyEnabledPlugins(Preset preset)
    {
        var alwaysOn = presetManager.GetAlwaysOnPlugins();
        var added = 0;
        foreach (var p in Plugin.PluginInterface.InstalledPlugins)
        {
            var key = GetPluginKey(p);
            if (p.IsLoaded &&
                !preset.Plugins.Contains(key) &&
                !alwaysOn.Contains(key) &&
                !alwaysOn.Contains(p.InternalName))
            {
                preset.Plugins.Add(key);
                added++;
            }
        }
        if (added > 0)
            presetManager.UpdatePreset(preset);
    }

    private void ExportPresetToClipboard(Preset preset)
    {
        try
        {
            var exportData = new
            {
                preset.Name,
                preset.Description,
                Plugins = preset.Plugins.ToList()
            };
            ImGui.SetClipboardText(JsonConvert.SerializeObject(exportData, Formatting.Indented));
            Plugin.Log.Info($"Exported preset '{preset.Name}' to clipboard");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to export preset");
        }
    }

    private void ImportPresetFromClipboard()
    {
        try
        {
            importError = string.Empty;
            var json = ImGui.GetClipboardText();

            if (string.IsNullOrWhiteSpace(json))
            {
                importError = "Clipboard empty";
                return;
            }

            var data = JsonConvert.DeserializeAnonymousType(json, new
            {
                Name = "",
                Description = (string?)null,
                Plugins = new List<string>(),
                EnabledPlugins = new List<string>()
            });

            if (data == null || string.IsNullOrWhiteSpace(data.Name))
            {
                importError = "Invalid data";
                return;
            }

            var plugins = data.Plugins?.Count > 0 ? data.Plugins : data.EnabledPlugins;

            var newPreset = new Preset
            {
                Name = data.Name,
                Description = data.Description ?? string.Empty,
                Plugins = new HashSet<string>(plugins ?? new List<string>()),
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now
            };

            presetManager.AddPreset(newPreset);
            selectedPreset = newPreset;
            showAlwaysOn = false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Failed to import preset from clipboard");
            importError = "Parse failed";
        }
    }
}
