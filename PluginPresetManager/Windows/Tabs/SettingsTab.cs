using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using PluginPresetManager.Models;
using PluginPresetManager.UI;

namespace PluginPresetManager.Windows.Tabs;

public class SettingsTab
{
    private readonly Plugin plugin;
    private readonly PresetManager presetManager;

    public SettingsTab(Plugin plugin, PresetManager presetManager)
    {
        this.plugin = plugin;
        this.presetManager = presetManager;
    }

    private CharacterConfig CharConfig => presetManager.CurrentConfig;
    private Configuration GlobalConfig => plugin.Configuration;

    public void Draw()
    {
        UIHelpers.SectionHeader("Notifications", FontAwesomeIcon.Bell);

        ImGui.SetNextItemWidth(Sizing.InputMedium);
        var currentMode = (int)CharConfig.NotificationMode;
        if (ImGui.Combo("##NotificationMode", ref currentMode, "None\0Toast\0Chat\0"))
        {
            CharConfig.NotificationMode = (NotificationMode)currentMode;
            presetManager.SaveCharacterConfig();
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);

        UIHelpers.SectionHeader("Advanced", FontAwesomeIcon.Cog);

        var useExperimental = GlobalConfig.UseExperimentalPersistence;
        if (ImGui.Checkbox("Experimental Persistent Mode", ref useExperimental))
        {
            GlobalConfig.UseExperimentalPersistence = useExperimental;
            plugin.SaveConfiguration();
        }
        if (ImGui.IsItemHovered())
        {
            UIHelpers.BeginTooltip("Experimental Feature");
            ImGui.TextColored(Colors.Warning, "Plugin states will persist across game restarts.");
            ImGui.Spacing();
            ImGui.TextWrapped("Uses internal Dalamud APIs via reflection.");
            ImGui.TextWrapped("May break on Dalamud updates.");
            UIHelpers.EndTooltip();
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);

        UIHelpers.SectionHeader("Info", FontAwesomeIcon.InfoCircle);

        var characters = presetManager.GetAllCharacters();
        var presets = presetManager.GetAllPresets();
        var alwaysOn = presetManager.GetAlwaysOnPlugins();

        ImGui.TextColored(Colors.TextMuted, $"Characters: {characters.Count}");
        ImGui.TextColored(Colors.TextMuted, $"Presets: {presets.Count}");
        ImGui.TextColored(Colors.TextMuted, $"Always-On: {alwaysOn.Count}");
    }
}
