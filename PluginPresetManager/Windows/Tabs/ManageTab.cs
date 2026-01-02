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
    private bool isSelectedPresetShared = false;

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
            var isSharedPreset = presetManager.IsSharedPreset(presetToDelete);
            var typeLabel = isSharedPreset ? "shared preset" : "preset";

            var result = UIHelpers.ConfirmationModal(
                "DeletePreset",
                "Delete Preset",
                $"Are you sure you want to delete {typeLabel} '{presetToDelete.Name}'?\n\nThis cannot be undone.");

            if (result == true)
            {
                if (isSharedPreset)
                    presetManager.DeleteSharedPreset(presetToDelete);
                else
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
            using (var combo = ImRaii.Combo("##SourceChar", currentSourceName))
            {
                if (combo)
                {
                    foreach (var (name, id) in sources)
                    {
                        if (ImGui.Selectable(name, id == importSourceCharacterId))
                        {
                            importSourceCharacterId = id;
                        }
                    }
                }
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
        var charAlwaysOnCount = presetManager.GetAlwaysOnPlugins().Count;
        var sharedAlwaysOnCount = presetManager.GetSharedAlwaysOnPlugins().Count;
        var totalAlwaysOn = charAlwaysOnCount + sharedAlwaysOnCount;

        if (presetManager.UseAlwaysOnAsDefault)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.TextColored(Colors.Star, FontAwesomeIcon.Star.ToIconString());
            }
            ImGui.SameLine();
        }

        if (ImGui.Selectable($"Always-On ({totalAlwaysOn})##alwayson", showAlwaysOn))
        {
            showAlwaysOn = true;
            selectedPreset = null;
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);
        ImGui.Separator();
        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        var characterPresets = presetManager.GetAllPresets().ToList();
        UIHelpers.SectionHeader($"Character Presets ({characterPresets.Count})", FontAwesomeIcon.LayerGroup);

        foreach (var preset in characterPresets)
        {
            var isSelected = selectedPreset?.Name == preset.Name && !showAlwaysOn && !isSelectedPresetShared;
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
                isSelectedPresetShared = false;
                showAlwaysOn = false;
            }

            DrawPresetContextMenu(preset, false);
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        var sharedPresets = presetManager.GetSharedPresets().ToList();
        UIHelpers.SectionHeader($"Shared Presets ({sharedPresets.Count})", FontAwesomeIcon.Globe);

        foreach (var preset in sharedPresets)
        {
            var isSelected = selectedPreset?.Name == preset.Name && isSelectedPresetShared;
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
            if (ImGui.Selectable($"{label}##shared", isSelected))
            {
                selectedPreset = preset;
                isSelectedPresetShared = true;
                showAlwaysOn = false;
            }

            DrawPresetContextMenu(preset, true);
        }
    }

    private void DrawPresetContextMenu(Preset preset, bool isShared)
    {
        var suffix = isShared ? "_shared" : "";
        using var ctx = ImRaii.ContextPopupItem($"PresetCtx_{preset.Name}{suffix}");
        if (!ctx) return;

        if (ImGui.MenuItem("Duplicate"))
        {
            if (isShared)
            {
                presetManager.CopySharedPresetToCharacter(preset);
            }
            else
            {
                selectedPreset = presetManager.DuplicatePreset(preset);
                isSelectedPresetShared = false;
            }
        }
        if (ImGui.MenuItem("Export to Clipboard"))
        {
            ExportPresetToClipboard(preset);
        }
        ImGui.Separator();

        if (isShared)
        {
            if (ImGui.MenuItem("Copy to Character"))
            {
                presetManager.CopySharedPresetToCharacter(preset);
            }
        }
        else
        {
            if (ImGui.MenuItem("Move to Shared"))
            {
                presetManager.MovePresetToShared(preset);
                if (selectedPreset?.Name == preset.Name)
                    selectedPreset = null;
            }
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
            DrawCombinedAlwaysOnEditor();
        else if (selectedPreset != null)
            DrawPresetEditor(selectedPreset, isSelectedPresetShared);
        else
            DrawEmptyState();
    }

    private void DrawEmptyState()
    {
        UIHelpers.EmptyState(FontAwesomeIcon.MousePointer, "Select a preset to edit");
    }

    private void DrawPresetEditor(Preset preset, bool isShared)
    {
        if (isShared)
        {
            ImGui.TextColored(Colors.Header, "Shared Preset");
            ImGui.TextColored(Colors.TextMuted, "Available to all characters");
            ImGui.Spacing();
        }

        ImGui.Text("Name");
        ImGui.SetNextItemWidth(-1);
        var name = preset.Name;
        if (ImGui.InputText("##PresetName", ref name, 100))
        {
            preset.Name = name;
            if (isShared)
                presetManager.UpdateSharedPreset(preset);
            else
                presetManager.UpdatePreset(preset);
        }

        ImGui.Spacing();

        ImGui.Text("Description");
        ImGui.SetNextItemWidth(-1);
        var desc = preset.Description;
        if (ImGui.InputTextMultiline("##PresetDesc", ref desc, 500, new Vector2(-1, 40)))
        {
            preset.Description = desc;
            if (isShared)
                presetManager.UpdateSharedPreset(preset);
            else
                presetManager.UpdatePreset(preset);
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        DrawPresetActions(preset, isShared);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawPresetPluginList(preset, isShared);
    }

    private void DrawPresetActions(Preset preset, bool isShared)
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
            ImGui.SetTooltip(isDefault ? "Click to unset as default" : "Set as default (enable 'Apply default on login' in Settings)");
        }
        ImGui.SameLine();

        if (ImGui.Button("Duplicate", new Vector2(Sizing.ButtonMedium, 0)))
        {
            if (isShared)
            {
                presetManager.CopySharedPresetToCharacter(preset);
            }
            else
            {
                selectedPreset = presetManager.DuplicatePreset(preset);
                isSelectedPresetShared = false;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Export", new Vector2(Sizing.ButtonMedium, 0)))
        {
            ExportPresetToClipboard(preset);
        }

        ImGui.SameLine();
        if (ImGui.Button("Add Current", new Vector2(Sizing.ButtonLarge, 0)))
        {
            AddCurrentlyEnabledPlugins(preset, isShared);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Add all currently enabled plugins");
        }

        if (isShared)
        {
            ImGui.SameLine();
            if (ImGui.Button("Copy to Char", new Vector2(Sizing.ButtonLarge, 0)))
            {
                presetManager.CopySharedPresetToCharacter(preset);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Copy this preset to the current character");
            }
        }
        else
        {
            ImGui.SameLine();
            if (ImGui.Button("Move to Shared", new Vector2(Sizing.ButtonLarge, 0)))
            {
                presetManager.MovePresetToShared(preset);
                selectedPreset = null;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Move this preset to shared (available to all characters)");
            }
        }
    }

    private void DrawPresetPluginList(Preset preset, bool isShared)
    {
        ImGui.Text("Plugins");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Sizing.InputMedium);
        ImGui.InputTextWithHint("##PluginSearch", "Search...", ref searchFilter, 100);

        ImGui.Spacing();

        var installedPlugins = GetInstalledPlugins();
        var effectiveAlwaysOn = presetManager.GetEffectiveAlwaysOnPlugins();

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

        if (effectiveAlwaysOn.Count > 0)
        {
            ImGui.TextColored(Colors.TextMuted, "Always-On (included automatically)");
            var anyAlwaysOnShown = false;
            foreach (var pluginName in effectiveAlwaysOn.OrderBy(p => p))
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
                    if (presetManager.IsSharedAlwaysOn(pluginName))
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(Colors.TextMuted, "[shared]");
                    }
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
            if (effectiveAlwaysOn.Contains(key) || effectiveAlwaysOn.Contains(p.InternalName)) continue;
            if (!MatchesFilter(key, installedPlugins)) continue;

            anyPluginsShown = true;
            var isInPreset = preset.Plugins.Contains(key);
            if (ImGui.Checkbox($"{p.Name}##{key}", ref isInPreset))
            {
                if (isInPreset)
                    preset.Plugins.Add(key);
                else
                    preset.Plugins.Remove(key);

                if (isShared)
                    presetManager.UpdateSharedPreset(preset);
                else
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

    private void DrawCombinedAlwaysOnEditor()
    {
        var charCount = presetManager.GetAlwaysOnPlugins().Count;
        var sharedCount = presetManager.GetSharedAlwaysOnPlugins().Count;
        var totalCount = charCount + sharedCount;

        UIHelpers.SectionHeader($"Always-On Plugins ({totalCount})", FontAwesomeIcon.Lock);

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
            var tooltip = isDefault
                ? "Click to unset as default"
                : $"Set as default ({charCount} character + {sharedCount} shared)\nEnable 'Apply default on login' in Settings";
            ImGui.SetTooltip(tooltip);
        }

        ImGui.SameLine();
        ImGui.TextColored(Colors.TextMuted, $"{charCount} character + {sharedCount} shared");

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        using (var tabBar = ImRaii.TabBar("AlwaysOnTabs"))
        {
            if (!tabBar) return;

            using (var charTab = ImRaii.TabItem($"Character ({charCount})"))
            {
                if (charTab)
                {
                    ImGui.Spacing();
                    ImGui.TextColored(Colors.TextMuted, "Plugins always enabled for this character.");
                    DrawCharacterAlwaysOnList();
                }
            }

            using (var sharedTab = ImRaii.TabItem($"Shared ({sharedCount})"))
            {
                if (sharedTab)
                {
                    ImGui.Spacing();
                    ImGui.TextColored(Colors.TextMuted, "Plugins always enabled for ALL characters.");
                    DrawSharedAlwaysOnList();
                }
            }
        }
    }

    private void DrawCharacterAlwaysOnList()
    {
        UIHelpers.VerticalSpacing(Sizing.SpacingSmall);

        ImGui.Text("Search");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Sizing.InputMedium);
        ImGui.InputTextWithHint("##AOSearch", "Filter...", ref searchFilter, 100);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var installedPlugins = GetInstalledPlugins();
        var alwaysOnPlugins = presetManager.GetAlwaysOnPlugins();
        var sharedAlwaysOn = presetManager.GetSharedAlwaysOnPlugins();
        var thisPluginName = Plugin.PluginInterface.InternalName;

        var duplicates = alwaysOnPlugins.Where(p => sharedAlwaysOn.Contains(p)).ToList();

        using var child = ImRaii.Child("CharAlwaysOnList", new Vector2(0, 0), false);
        if (!child) return;

        if (duplicates.Count > 0)
        {
            ImGui.TextColored(Colors.Warning, $"Redundant ({duplicates.Count}) - already in shared:");
            foreach (var dup in duplicates)
            {
                var displayName = installedPlugins.TryGetValue(dup, out var info) ? info.Name : dup;
                ImGui.TextColored(Colors.TextMuted, $"  • {displayName}");
            }
            if (ImGui.Button("Remove Redundant"))
            {
                foreach (var dup in duplicates)
                {
                    presetManager.RemoveAlwaysOnPlugin(dup);
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Remove these from character always-on since they're already in shared");
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        var anyPluginsShown = false;
        foreach (var (key, p) in installedPlugins.OrderBy(kv => kv.Value.Name).ThenBy(kv => kv.Key))
        {
            if (!MatchesFilter(key, installedPlugins)) continue;

            anyPluginsShown = true;
            var isAlwaysOn = alwaysOnPlugins.Contains(key);
            var isSharedAlwaysOn = sharedAlwaysOn.Contains(key);
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
                    ImGui.Text("(required)");
                }
            }
            else if (isSharedAlwaysOn)
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
                ImGui.TextColored(Colors.TextMuted, "(in shared)");
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

    private void DrawSharedAlwaysOnList()
    {
        UIHelpers.VerticalSpacing(Sizing.SpacingSmall);

        ImGui.Text("Search");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Sizing.InputMedium);
        ImGui.InputTextWithHint("##SharedAOSearch", "Filter...", ref searchFilter, 100);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var installedPlugins = GetInstalledPlugins();
        var sharedAlwaysOn = presetManager.GetSharedAlwaysOnPlugins();
        var characterAlwaysOn = presetManager.GetAlwaysOnPlugins();
        var thisPluginName = Plugin.PluginInterface.InternalName;

        using var child = ImRaii.Child("SharedAlwaysOnList", new Vector2(0, 0), false);
        if (!child) return;

        var anyPluginsShown = false;
        foreach (var (key, p) in installedPlugins.OrderBy(kv => kv.Value.Name).ThenBy(kv => kv.Key))
        {
            if (!MatchesFilter(key, installedPlugins)) continue;

            anyPluginsShown = true;
            var isSharedAlwaysOn = sharedAlwaysOn.Contains(key);
            var isCharacterAlwaysOn = characterAlwaysOn.Contains(key);
            var isThisPlugin = p.InternalName == thisPluginName && !p.IsDev;

            if (isThisPlugin)
            {
                using (ImRaii.Disabled())
                {
                    var check = true;
                    ImGui.Checkbox($"##{key}_sao", ref check);
                }
                ImGui.SameLine();
                ImGui.Text(p.Name);
                DrawPluginTags(p);
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.Warning))
                {
                    ImGui.Text("(required)");
                }
            }
            else if (isCharacterAlwaysOn)
            {
                using (ImRaii.Disabled())
                {
                    var check = isSharedAlwaysOn;
                    ImGui.Checkbox($"##{key}_sao", ref check);
                }
                ImGui.SameLine();
                ImGui.Text(p.Name);
                DrawPluginTags(p);
                ImGui.SameLine();
                ImGui.TextColored(Colors.TextMuted, "(in character)");
            }
            else
            {
                if (ImGui.Checkbox($"{p.Name}##{key}_sao", ref isSharedAlwaysOn))
                {
                    if (isSharedAlwaysOn)
                        presetManager.AddSharedAlwaysOnPlugin(key);
                    else
                        presetManager.RemoveSharedAlwaysOnPlugin(key);
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

    private void AddCurrentlyEnabledPlugins(Preset preset, bool isShared)
    {
        var effectiveAlwaysOn = presetManager.GetEffectiveAlwaysOnPlugins();
        var added = 0;
        foreach (var p in Plugin.PluginInterface.InstalledPlugins)
        {
            var key = GetPluginKey(p);
            if (p.IsLoaded &&
                !preset.Plugins.Contains(key) &&
                !effectiveAlwaysOn.Contains(key) &&
                !effectiveAlwaysOn.Contains(p.InternalName))
            {
                preset.Plugins.Add(key);
                added++;
            }
        }
        if (added > 0)
        {
            if (isShared)
                presetManager.UpdateSharedPreset(preset);
            else
                presetManager.UpdatePreset(preset);
        }
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
