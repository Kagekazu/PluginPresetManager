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
    private CharacterData? characterToDelete = null;

    public SettingsTab(Plugin plugin, PresetManager presetManager)
    {
        this.plugin = plugin;
        this.presetManager = presetManager;
    }

    private CharacterData Data => presetManager.CurrentData;
    private Configuration GlobalConfig => plugin.Configuration;

    public void Draw()
    {
        if (!presetManager.HasCharacter)
        {
            ImGui.TextColored(Colors.Warning, "Please log in to a character to access settings.");
            return;
        }

        UIHelpers.SectionHeader("Notifications", FontAwesomeIcon.Bell);

        ImGui.SetNextItemWidth(Sizing.InputMedium);
        var currentMode = (int)Data.NotificationMode;
        if (ImGui.Combo("##NotificationMode", ref currentMode, "None\0Toast\0Chat\0"))
        {
            Data.NotificationMode = (NotificationMode)currentMode;
            plugin.CharacterStorage.Save(Data);
        }

        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);

        UIHelpers.SectionHeader("Login Behavior", FontAwesomeIcon.SignInAlt);

        var useAlwaysOn = presetManager.UseAlwaysOnAsDefault;
        if (ImGui.Checkbox("Apply Always-On Only on login", ref useAlwaysOn))
        {
            presetManager.SetAlwaysOnAsDefault(useAlwaysOn);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("On login, disable all plugins except those marked as always-on.");
        }

        if (!useAlwaysOn)
        {
            var defaultPreset = presetManager.DefaultPreset;
            if (!string.IsNullOrEmpty(defaultPreset))
            {
                ImGui.TextColored(Colors.TextMuted, $"Default preset: {defaultPreset}");
            }
            else
            {
                ImGui.TextColored(Colors.TextMuted, "No default preset set. Set one in Manage tab.");
            }
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

        if (characters.Count > 0)
        {
            UIHelpers.VerticalSpacing(Sizing.SpacingLarge);
            UIHelpers.SectionHeader("Character Data", FontAwesomeIcon.Users);

            ImGui.TextColored(Colors.TextMuted, "Delete unused character data to clean up.");
            ImGui.Spacing();

            foreach (var character in characters.OrderBy(c => c.Name))
            {
                ImGui.Text($"{character.DisplayName}");
                ImGui.SameLine();
                ImGui.TextColored(Colors.TextMuted, $"({character.Presets.Count} presets)");

                if (character.ContentId != presetManager.CurrentCharacterId)
                {
                    ImGui.SameLine();
                    using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.5f, 0.2f, 0.2f, 1f)))
                    using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.3f, 0.3f, 1f)))
                    {
                        if (ImGui.SmallButton($"Delete##{character.ContentId}"))
                        {
                            characterToDelete = character;
                            UIHelpers.OpenConfirmationModal("DeleteCharacter", "Delete Character Data");
                        }
                    }
                }
                else
                {
                    ImGui.SameLine();
                    ImGui.TextColored(Colors.Active, "(current)");
                }
            }
        }

        DrawDeleteConfirmation();
    }

    private void DrawDeleteConfirmation()
    {
        if (characterToDelete != null)
        {
            var result = UIHelpers.ConfirmationModal(
                "DeleteCharacter",
                "Delete Character Data",
                $"Delete all data for '{characterToDelete.DisplayName}'?\n\n" +
                $"This will remove {characterToDelete.Presets.Count} preset(s) and cannot be undone.");

            if (result == true)
            {
                presetManager.DeleteCharacter(characterToDelete.ContentId);
                characterToDelete = null;
            }
            else if (result == false)
            {
                characterToDelete = null;
            }
        }
    }
}
