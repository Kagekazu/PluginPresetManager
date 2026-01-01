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

    public ManageTab(Plugin plugin, PresetManager presetManager)
    {
        this.plugin = plugin;
        this.presetManager = presetManager;
    }

    private CharacterConfig Config => presetManager.CurrentConfig;

    public void Draw()
    {
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
    }

    private void DrawTopBar()
    {
        ImGui.SetNextItemWidth(Sizing.InputMedium);
        ImGui.InputTextWithHint("##NewPreset", "New preset name...", ref newPresetName, 100);

        ImGui.SameLine();
        if (UIHelpers.IconButton(FontAwesomeIcon.Plus, "Create", "Create new preset", Sizing.ButtonSmall))
        {
            CreatePreset();
        }

        ImGui.SameLine();
        if (UIHelpers.IconButton(FontAwesomeIcon.FileImport, "Import", "Import from clipboard", Sizing.ButtonSmall))
        {
            ImportPresetFromClipboard();
        }

        if (!string.IsNullOrEmpty(importError))
        {
            ImGui.SameLine();
            ImGui.TextColored(Colors.Error, importError);
        }

        ImGui.Spacing();
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

    private void DrawLeftPanel()
    {
        UIHelpers.SectionHeader("Presets", FontAwesomeIcon.LayerGroup);

        foreach (var preset in presetManager.GetAllPresets())
        {
            var isSelected = selectedPreset?.Id == preset.Id && !showAlwaysOn;
            var isDefault = Config.DefaultPresetId == preset.Id;

            // Build label
            if (isDefault)
            {
                ImGui.TextColored(Colors.Star, FontAwesomeIcon.Star.ToIconString());
                ImGui.SameLine();
            }

            var label = $"{preset.Name} ({preset.EnabledPlugins.Count})";
            if (ImGui.Selectable(label, isSelected))
            {
                selectedPreset = preset;
                showAlwaysOn = false;
            }

            DrawPresetContextMenu(preset);
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);

        // Always-On section
        var alwaysOnPlugins = presetManager.GetAlwaysOnPlugins();
        UIHelpers.SectionHeader($"Always-On ({alwaysOnPlugins.Count})", FontAwesomeIcon.Lock);

        if (ImGui.Selectable("Manage Always-On...", showAlwaysOn))
        {
            showAlwaysOn = true;
            selectedPreset = null;
        }
    }

    private void DrawPresetContextMenu(Preset preset)
    {
        using var ctx = ImRaii.ContextPopupItem($"PresetCtx_{preset.Id}");
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
            presetManager.DeletePreset(preset);
            if (selectedPreset?.Id == preset.Id)
                selectedPreset = null;
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
        UIHelpers.EmptyState(
            FontAwesomeIcon.MousePointer,
            "Select a preset to edit");
    }

    private void DrawPresetEditor(Preset preset)
    {
        // Name input
        ImGui.Text("Name");
        ImGui.SetNextItemWidth(-1);
        var name = preset.Name;
        if (ImGui.InputText("##PresetName", ref name, 100))
        {
            preset.Name = name;
            presetManager.UpdatePreset(preset);
        }

        ImGui.Spacing();

        // Description
        ImGui.Text("Description");
        ImGui.SetNextItemWidth(-1);
        var desc = preset.Description;
        if (ImGui.InputTextMultiline("##PresetDesc", ref desc, 500, new Vector2(-1, 40)))
        {
            preset.Description = desc;
            presetManager.UpdatePreset(preset);
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        // Action buttons
        DrawPresetActions(preset);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Plugin list
        DrawPresetPluginList(preset);
    }

    private void DrawPresetActions(Preset preset)
    {
        var isDefault = Config.DefaultPresetId == preset.Id;

        // Set Default button
        if (isDefault)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.1f, 1f)))
            {
                if (ImGui.Button("★ Default", new Vector2(Sizing.ButtonLarge, 0)))
                {
                    Config.DefaultPresetId = null;
                    presetManager.SaveCharacterConfig();
                }
            }
        }
        else
        {
            if (ImGui.Button("Set Default", new Vector2(Sizing.ButtonLarge, 0)))
            {
                Config.DefaultPresetId = preset.Id;
                presetManager.SaveCharacterConfig();
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

        var installedPlugins = Plugin.PluginInterface.InstalledPlugins
            .GroupBy(p => p.InternalName)
            .ToDictionary(g => g.Key, g => g.First());

        var alwaysOnPlugins = presetManager.GetAlwaysOnPlugins();

        using var child = ImRaii.Child("PluginList", new Vector2(0, 0), false);
        if (!child) return;

        // Always-on section
        if (alwaysOnPlugins.Count > 0)
        {
            ImGui.TextColored(Colors.TextMuted, "Always-On (included automatically)");
            foreach (var pluginName in alwaysOnPlugins.OrderBy(p => p))
            {
                if (!MatchesFilter(pluginName, installedPlugins)) continue;

                using (ImRaii.Disabled())
                {
                    var check = true;
                    ImGui.Checkbox($"##{pluginName}_ao", ref check);
                }
                ImGui.SameLine();
                var displayName = installedPlugins.TryGetValue(pluginName, out var info)
                    ? info.Name
                    : $"{pluginName} (not installed)";
                ImGui.TextColored(Colors.TextMuted, displayName);
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        // Preset plugins
        ImGui.TextColored(Colors.Header, "Preset Plugins");

        foreach (var p in installedPlugins.Values.OrderBy(p => p.Name))
        {
            if (alwaysOnPlugins.Contains(p.InternalName)) continue;
            if (!MatchesFilter(p.InternalName, installedPlugins)) continue;

            var isInPreset = preset.EnabledPlugins.Contains(p.InternalName);
            if (ImGui.Checkbox($"{p.Name}##{p.InternalName}", ref isInPreset))
            {
                if (isInPreset)
                    preset.EnabledPlugins.Add(p.InternalName);
                else
                    preset.EnabledPlugins.Remove(p.InternalName);
                presetManager.UpdatePreset(preset);
            }

            DrawPluginTags(p);
        }
    }

    private void DrawAlwaysOnEditor()
    {
        UIHelpers.SectionHeader("Always-On Plugins", FontAwesomeIcon.Lock);
        ImGui.TextColored(Colors.TextMuted, "These plugins stay enabled regardless of preset.");

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        ImGui.Text("Search");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Sizing.InputMedium);
        ImGui.InputTextWithHint("##AOSearch", "Filter...", ref searchFilter, 100);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var installedPlugins = Plugin.PluginInterface.InstalledPlugins
            .GroupBy(p => p.InternalName)
            .ToDictionary(g => g.Key, g => g.First());

        var alwaysOnPlugins = presetManager.GetAlwaysOnPlugins();
        var thisPluginName = Plugin.PluginInterface.InternalName;

        using var child = ImRaii.Child("AlwaysOnList", new Vector2(0, 0), false);
        if (!child) return;

        foreach (var p in installedPlugins.Values.OrderBy(p => p.Name))
        {
            if (!MatchesFilter(p.InternalName, installedPlugins)) continue;

            var isAlwaysOn = alwaysOnPlugins.Contains(p.InternalName);
            var isThisPlugin = p.InternalName == thisPluginName;

            using (isThisPlugin ? ImRaii.Disabled() : null)
            {
                if (ImGui.Checkbox($"{p.Name}##{p.InternalName}_ao", ref isAlwaysOn))
                {
                    if (isAlwaysOn)
                        presetManager.AddAlwaysOnPlugin(p.InternalName);
                    else
                        presetManager.RemoveAlwaysOnPlugin(p.InternalName);
                }
            }

            if (isThisPlugin)
            {
                ImGui.SameLine();
                ImGui.TextColored(Colors.TextDisabled, "(required)");
            }

            DrawPluginTags(p);
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
            if (p.IsLoaded &&
                !preset.EnabledPlugins.Contains(p.InternalName) &&
                !alwaysOn.Contains(p.InternalName))
            {
                preset.EnabledPlugins.Add(p.InternalName);
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
                EnabledPlugins = preset.EnabledPlugins.ToList()
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
                EnabledPlugins = new List<string>()
            });

            if (data == null || string.IsNullOrWhiteSpace(data.Name))
            {
                importError = "Invalid data";
                return;
            }

            var newPreset = new Preset
            {
                Id = Guid.NewGuid(),
                Name = data.Name,
                Description = data.Description ?? string.Empty,
                EnabledPlugins = new HashSet<string>(data.EnabledPlugins ?? new List<string>()),
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now
            };

            presetManager.AddPreset(newPreset);
            selectedPreset = newPreset;
            showAlwaysOn = false;
        }
        catch
        {
            importError = "Parse failed";
        }
    }
}
