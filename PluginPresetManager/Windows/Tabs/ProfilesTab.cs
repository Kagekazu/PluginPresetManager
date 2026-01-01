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
        var data = presetManager.CurrentData;
        var presets = presetManager.GetAllPresets();
        var alwaysOnCount = presetManager.GetAlwaysOnPlugins().Count;
        var lastApplied = presetManager.GetLastAppliedPreset();

        // Status text if applying
        if (presetManager.IsApplying)
        {
            DrawApplyingStatus();
            return;
        }

        // Header
        DrawHeader(lastApplied);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Always-On Only option
        DrawAlwaysOnOnlyOption(alwaysOnCount, lastApplied == null);

        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);

        // Presets section
        UIHelpers.SectionHeader("Presets", FontAwesomeIcon.LayerGroup);

        if (presets.Count == 0)
        {
            DrawEmptyState();
        }
        else
        {
            foreach (var preset in presets)
            {
                DrawPresetRow(preset, data, lastApplied);
                ImGui.Spacing();
            }
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

    private void DrawHeader(Preset? lastApplied)
    {
        if (lastApplied != null)
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

    private void DrawAlwaysOnOnlyOption(int alwaysOnCount, bool isActive)
    {
        UIHelpers.StatusDot(isActive);
        ImGui.SameLine();

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

        ImGui.TextColored(Colors.TextMuted, $"     Disable all except {alwaysOnCount} always-on plugin(s)");
    }

    private void DrawPresetRow(Preset preset, CharacterData data, Preset? lastApplied)
    {
        var isActive = lastApplied?.Name == preset.Name;
        var isDefault = data.DefaultPreset == preset.Name;
        var pluginCount = preset.Plugins.Count;
        var alwaysOnCount = presetManager.GetAlwaysOnPlugins().Count;
        var totalPlugins = pluginCount + alwaysOnCount;

        // Status dot
        UIHelpers.StatusDot(isActive);
        ImGui.SameLine();

        // Default star
        if (isDefault)
        {
            ImGui.TextColored(Colors.Star, FontAwesomeIcon.Star.ToIconString());
            ImGui.SameLine();
        }

        // Preset name
        var textColor = isActive ? Colors.Active : Colors.TextNormal;
        using (ImRaii.PushColor(ImGuiCol.Text, textColor))
        {
            if (ImGui.Selectable($"{preset.Name}##{preset.Name}", false))
            {
                if (!presetManager.IsApplying && !isActive)
                {
                    _ = presetManager.ApplyPresetAsync(preset);
                }
            }
        }

        // Tooltip
        if (ImGui.IsItemHovered())
        {
            DrawPresetTooltip(preset, pluginCount, alwaysOnCount, totalPlugins, isDefault);
        }

        // Plugin count
        ImGui.TextColored(Colors.TextMuted, $"     {totalPlugins} plugins");
    }

    private void DrawPresetTooltip(Preset preset, int pluginCount, int alwaysOnCount, int totalPlugins, bool isDefault)
    {
        UIHelpers.BeginTooltip(preset.Name);

        if (isDefault)
        {
            ImGui.TextColored(Colors.Star, "★ Default preset (auto-applies on login)");
            ImGui.Spacing();
        }

        ImGui.Text($"Preset plugins: {pluginCount}");
        ImGui.Text($"Always-on: {alwaysOnCount}");
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
