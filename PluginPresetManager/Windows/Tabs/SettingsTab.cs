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
		
		ImGui.Text("Notifications:");
		var currentMode = (int)config.NotificationMode;
		if (ImGui.Combo("##NotificationMode", ref currentMode, "None\0Toast\0Chat\0"))
		{
			config.NotificationMode = (NotificationMode)currentMode;
			Plugin.PluginInterface.SavePluginConfig(config);
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("How to display notifications\nToast: Non-intrusive popup notifications\nChat: Messages in chat window\nNone: No notifications");
		}
	}
}


