using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginPresetManager.Windows.Tabs;

public class HelpTab
{
	public void Draw()
	{
		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Commands");
		ImGui.Separator();

		ImGui.TextColored(new Vector4(1, 1, 0.5f, 1), "/ppreset");
		ImGui.SameLine();
		ImGui.TextUnformatted("- Open the main Plugin Preset Manager window");

		ImGui.TextColored(new Vector4(1, 1, 0.5f, 1), "/ppm");
		ImGui.SameLine();
		ImGui.TextUnformatted("- Toggle the main window (same as /ppreset)");

		ImGui.TextColored(new Vector4(1, 1, 0.5f, 1), "/ppm <preset name>");
		ImGui.SameLine();
		ImGui.TextUnformatted("- Apply a preset by name");
		ImGui.Indent();
		ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Example: /ppm Raiding");
		ImGui.Unindent();

		ImGui.TextColored(new Vector4(1, 1, 0.5f, 1), "/ppm alwayson");
		ImGui.SameLine();
		ImGui.TextUnformatted("- Enable only always-on plugins, disable everything else");

		ImGui.Separator();

		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Features");
		ImGui.Separator();

		ImGui.BulletText("Presets: Save and apply different plugin configurations");
		ImGui.BulletText("Always-On: Plugins that stay enabled regardless of preset");
		ImGui.BulletText("Default Preset: Auto-apply a preset when you log in");

		ImGui.Separator();

		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Settings");
		ImGui.Separator();

		ImGui.BulletText("Show Notifications - Display chat messages when presets are applied");
		ImGui.BulletText("Verbose Notifications - Show detailed info (plugin counts, warnings)");
		ImGui.BulletText("Delay Between Commands - Time to wait between plugin commands (adjust if needed)");

		ImGui.Separator();

		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Notes");
		ImGui.Separator();

		ImGui.TextWrapped("• Dalamud's plugin installer UI may not reflect changes immediately.");

		ImGui.Separator();

		if (ImGui.Button("Open GitHub Repository##HelpTabGitHub", new Vector2(200, 0)))
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = "https://github.com/Brappp/PluginPresetManager",
				UseShellExecute = true
			});
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("https://github.com/Brappp/PluginPresetManager");
		}
	}
}


