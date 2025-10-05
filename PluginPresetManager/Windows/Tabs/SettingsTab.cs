using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginPresetManager.Windows.Tabs;

public class SettingsTab
{
	private readonly Configuration config;
	private readonly PresetManager presetManager;

	public SettingsTab(Configuration config, PresetManager presetManager)
	{
		this.config = config;
		this.presetManager = presetManager;
	}

	public void Draw()
	{
		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Settings");
		var showNotifications = config.ShowNotifications;
		if (ImGui.Checkbox("Show Notifications", ref showNotifications))
		{
			config.ShowNotifications = showNotifications;
			Plugin.PluginInterface.SavePluginConfig(config);
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("Show chat notifications when presets are applied");
		}

		if (config.ShowNotifications)
		{
			ImGui.Indent();
			var verboseNotifications = config.VerboseNotifications;
			if (ImGui.Checkbox("Verbose Notifications", ref verboseNotifications))
			{
				config.VerboseNotifications = verboseNotifications;
				Plugin.PluginInterface.SavePluginConfig(config);
			}
			if (ImGui.IsItemHovered())
			{
				ImGui.SetTooltip("Show detailed info (plugin counts, warnings, etc.)");
			}
			ImGui.Unindent();
		}

		var delay = config.DelayBetweenCommands;
		if (ImGui.SliderInt("Delay (ms)", ref delay, 50, 500))
		{
			config.DelayBetweenCommands = delay;
			Plugin.PluginInterface.SavePluginConfig(config);
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("Time to wait between enable/disable commands\nHigher values are more stable but slower");
		}
	}
}


