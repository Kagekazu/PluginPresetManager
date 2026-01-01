using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using PluginPresetManager.Models;

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
		// Character-specific settings section
		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Character Settings");
		ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "(These apply to the currently selected character)");
		ImGui.Spacing();

		ImGui.Text("Notifications:");
		var currentMode = (int)CharConfig.NotificationMode;
		if (ImGui.Combo("##NotificationMode", ref currentMode, "None\0Toast\0Chat\0"))
		{
			CharConfig.NotificationMode = (NotificationMode)currentMode;
			presetManager.SaveCharacterConfig();
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("How to display notifications\nToast: Non-intrusive popup notifications\nChat: Messages in chat window\nNone: No notifications");
		}

		ImGui.Spacing();

		ImGui.Text("Plugin State Check Interval:");
		var checkInterval = CharConfig.PluginStateCheckInterval;
		if (ImGui.SliderInt("##CheckInterval", ref checkInterval, 100, 2000, "%d ms"))
		{
			CharConfig.PluginStateCheckInterval = checkInterval;
			presetManager.SaveCharacterConfig();
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("How often to check if a plugin has finished loading/unloading");
		}

		ImGui.Text("Delay Between Commands:");
		var delayBetween = CharConfig.DelayBetweenCommands;
		if (ImGui.SliderInt("##DelayBetween", ref delayBetween, 10, 500, "%d ms"))
		{
			CharConfig.DelayBetweenCommands = delayBetween;
			presetManager.SaveCharacterConfig();
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("Delay between each enable/disable command");
		}

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();

		// Global settings section
		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Global Settings");
		ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "(These apply to all characters)");
		ImGui.Spacing();

		var useExperimental = GlobalConfig.UseExperimentalPersistence;
		if (ImGui.Checkbox("Use Experimental Persistent Mode", ref useExperimental))
		{
			GlobalConfig.UseExperimentalPersistence = useExperimental;
			plugin.SaveConfiguration();
		}
		if (ImGui.IsItemHovered())
		{
			using (ImRaii.Tooltip())
			{
				ImGui.TextColored(new Vector4(1, 1, 0, 1), "EXPERIMENTAL");
				ImGui.TextWrapped("When enabled, plugin enable/disable states will persist across game restarts.");
				ImGui.Spacing();
				ImGui.TextWrapped("This uses internal Dalamud APIs via reflection and may break on Dalamud updates.");
				ImGui.Spacing();
				ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), "Use at your own risk!");
			}
		}

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();

		// Info section
		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Information");
		ImGui.Spacing();

		var characters = presetManager.GetAllCharacters();
		ImGui.Text($"Registered Characters: {characters.Count}");

		var presets = presetManager.GetAllPresets();
		ImGui.Text($"Presets (current character): {presets.Count}");

		var alwaysOn = presetManager.GetAlwaysOnPlugins();
		ImGui.Text($"Always-On Plugins (current character): {alwaysOn.Count}");
	}
}


