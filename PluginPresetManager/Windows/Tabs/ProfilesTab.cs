using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using PluginPresetManager.Models;
using PluginPresetManager.UI;

namespace PluginPresetManager.Windows.Tabs;

public class ProfilesTab
{
    private readonly PresetManager presetManager;

    public ProfilesTab(PresetManager presetManager)
    {
        this.presetManager = presetManager;
    }

    public void Draw()
    {
        if (!presetManager.HasCharacter)
        {
            ImGui.TextColored(Colors.Warning, "Please log in to a character to use presets.");
            return;
        }

        var data = presetManager.CurrentData;
        var presets = presetManager.GetAllPresets();
        var sharedPresets = presetManager.GetSharedPresets();
        var charAlwaysOnCount = presetManager.GetAlwaysOnPlugins().Count;
        var sharedAlwaysOnCount = presetManager.GetSharedAlwaysOnPlugins().Count;
        var totalAlwaysOn = charAlwaysOnCount + sharedAlwaysOnCount;
        var lastApplied = presetManager.GetLastAppliedPreset();
        var isAlwaysOnActive = presetManager.WasLastAppliedAlwaysOn;

        if (presetManager.IsApplying)
        {
            DrawApplyingStatus();
            return;
        }

        DrawHeader(lastApplied, isAlwaysOnActive);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawAlwaysOnOnlyOption(charAlwaysOnCount, sharedAlwaysOnCount, isAlwaysOnActive);

        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);

        UIHelpers.SectionHeader($"Character Presets ({presets.Count})", FontAwesomeIcon.LayerGroup);

        if (presets.Count == 0)
        {
            ImGui.TextColored(Colors.TextMuted, "No character presets");
        }
        else
        {
            foreach (var preset in presets)
            {
                DrawPresetRow(preset, data, lastApplied, isAlwaysOnActive, false);
                ImGui.Spacing();
            }
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingMedium);

        UIHelpers.SectionHeader($"Shared Presets ({sharedPresets.Count})", FontAwesomeIcon.Globe);

        if (sharedPresets.Count == 0)
        {
            ImGui.TextColored(Colors.TextMuted, "No shared presets");
        }
        else
        {
            foreach (var preset in sharedPresets)
            {
                DrawPresetRow(preset, data, lastApplied, isAlwaysOnActive, true);
                ImGui.Spacing();
            }
        }

        if (presets.Count == 0 && sharedPresets.Count == 0)
        {
            DrawEmptyState();
        }
    }

    private void DrawApplyingStatus()
    {
        var availHeight = ImGui.GetContentRegionAvail().Y;
        ImGui.Dummy(new Vector2(0, availHeight * 0.35f));

        UIHelpers.CenteredIcon(FontAwesomeIcon.Spinner, Colors.Warning);
        ImGui.Spacing();
        UIHelpers.CenteredText(presetManager.ApplyingStatus, Colors.Warning);
    }

    private void DrawHeader(Preset? lastApplied, bool isAlwaysOnActive)
    {
        if (isAlwaysOnActive)
        {
            UIHelpers.CenteredText("Active: Always-On Only", Colors.Success);
        }
        else if (lastApplied != null)
        {
            UIHelpers.CenteredText($"Active: {lastApplied.Name}", Colors.Success);
        }
        else
        {
            UIHelpers.CenteredText("No preset active", Colors.TextMuted);
        }
    }

    private void DrawEmptyState()
    {
        ImGui.Spacing();
        UIHelpers.CenteredIcon(FontAwesomeIcon.FolderOpen, Colors.TextMuted);
        ImGui.Spacing();
        UIHelpers.CenteredText("No presets yet", Colors.TextMuted);
        UIHelpers.CenteredText("Create one in the Manage tab", Colors.TextDisabled);
    }

    private void DrawAlwaysOnOnlyOption(int charCount, int sharedCount, bool isActive)
    {
        var totalCount = charCount + sharedCount;
        var isDefault = presetManager.UseAlwaysOnAsDefault;

        UIHelpers.StatusDot(isActive);
        ImGui.SameLine();

        if (isDefault)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.TextColored(Colors.Star, FontAwesomeIcon.Star.ToIconString());
            }
            ImGui.SameLine();
        }

        using (ImRaii.PushColor(ImGuiCol.Text, isActive ? Colors.Active : Colors.TextNormal))
        {
            if (ImGui.Selectable("Always-On Only##AlwaysOnOnly", false))
            {
                if (!presetManager.IsApplying)
                {
                    _ = presetManager.ApplyAlwaysOnOnlyAsync();
                }
            }
        }

        if (ImGui.IsItemHovered())
        {
            UIHelpers.BeginTooltip("Always-On Only");
            if (isDefault)
            {
                var applyOnLogin = presetManager.ApplyDefaultOnLogin;
                ImGui.TextColored(Colors.Star, applyOnLogin ? "★ Default (applies on login)" : "★ Default");
                ImGui.Spacing();
            }
            ImGui.Text($"Character always-on: {charCount}");
            ImGui.Text($"Shared always-on: {sharedCount}");
            ImGui.TextColored(Colors.Header, $"Total: {totalCount}");
            ImGui.Spacing();
            ImGui.TextColored(Colors.TextDisabled, "Click to apply");
            UIHelpers.EndTooltip();
        }

        ImGui.TextColored(Colors.TextMuted, $"     {charCount} character + {sharedCount} shared = {totalCount} plugins");
    }

    private void DrawPresetRow(Preset preset, CharacterData data, Preset? lastApplied, bool isAlwaysOnActive, bool isShared)
    {
        var isActive = lastApplied?.Name == preset.Name && !isAlwaysOnActive;
        var isDefault = data.DefaultPreset == preset.Name;
        var pluginCount = preset.Plugins.Count;
        var effectiveAlwaysOn = presetManager.GetEffectiveAlwaysOnPlugins();
        var alwaysOnCount = effectiveAlwaysOn.Count;
        var totalPlugins = pluginCount + alwaysOnCount;

        UIHelpers.StatusDot(isActive);
        ImGui.SameLine();

        if (isDefault)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.TextColored(Colors.Star, FontAwesomeIcon.Star.ToIconString());
            }
            ImGui.SameLine();
        }

        var textColor = isActive ? Colors.Active : Colors.TextNormal;
        using (ImRaii.PushColor(ImGuiCol.Text, textColor))
        {
            var selectableId = isShared ? $"{preset.Name}##shared_{preset.Name}" : $"{preset.Name}##{preset.Name}";
            if (ImGui.Selectable(selectableId, false))
            {
                if (!presetManager.IsApplying && !isActive)
                {
                    _ = presetManager.ApplyPresetAsync(preset);
                }
            }
        }

        if (ImGui.IsItemHovered())
        {
            DrawPresetTooltip(preset, pluginCount, alwaysOnCount, totalPlugins, isDefault, isShared);
        }

        ImGui.TextColored(Colors.TextMuted, $"     {pluginCount} preset + {alwaysOnCount} always-on = {totalPlugins} plugins");
    }

    private void DrawPresetTooltip(Preset preset, int pluginCount, int alwaysOnCount, int totalPlugins, bool isDefault, bool isShared)
    {
        var title = isShared ? $"{preset.Name} (Shared)" : preset.Name;
        UIHelpers.BeginTooltip(title);

        if (isDefault)
        {
            var applyOnLogin = presetManager.ApplyDefaultOnLogin;
            ImGui.TextColored(Colors.Star, applyOnLogin ? "★ Default (applies on login)" : "★ Default");
            ImGui.Spacing();
        }

        if (isShared)
        {
            ImGui.TextColored(Colors.TextMuted, "Available to all characters");
            ImGui.Spacing();
        }

        ImGui.Text($"Preset plugins: {pluginCount}");
        ImGui.Text($"Always-on (combined): {alwaysOnCount}");
        ImGui.TextColored(Colors.Header, $"Total: {totalPlugins}");

        if (!string.IsNullOrEmpty(preset.Description))
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextWrapped(preset.Description);
        }

        ImGui.Spacing();
        ImGui.TextColored(Colors.TextDisabled, "Click to apply");

        UIHelpers.EndTooltip();
    }
}
