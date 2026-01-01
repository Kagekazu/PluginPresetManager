using System.Linq;
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

    private CharacterData Data => presetManager.CurrentData;
    private Configuration GlobalConfig => plugin.Configuration;

    public void Draw()
    {
        UIHelpers.SectionHeader("Notifications", FontAwesomeIcon.Bell);

        ImGui.SetNextItemWidth(Sizing.InputMedium);
        var currentMode = (int)Data.NotificationMode;
        if (ImGui.Combo("##NotificationMode", ref currentMode, "None\0Toast\0Chat\0"))
        {
            Data.NotificationMode = (NotificationMode)currentMode;
            plugin.CharacterStorage.Save(Data);
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
        ImGui.TextColored(Colors.TextMuted, $"Presets (current): {presets.Count}");
        ImGui.TextColored(Colors.TextMuted, $"Always-On: {alwaysOn.Count}");

        // Character management
        if (characters.Count > 0)
        {
            UIHelpers.VerticalSpacing(Sizing.SpacingLarge);
            UIHelpers.SectionHeader("Character Data", FontAwesomeIcon.Users);

            foreach (var character in characters.OrderByDescending(c => c.LastSeen))
            {
                ImGui.Text($"{character.DisplayName}");
                ImGui.SameLine();
                ImGui.TextColored(Colors.TextMuted, $"(last seen: {character.LastSeen:yyyy-MM-dd})");

                if (character.ContentId != presetManager.CurrentCharacterId)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Delete##{character.ContentId}"))
                    {
                        presetManager.DeleteCharacter(character.ContentId);
                    }
                }
            }
        }
    }
}
