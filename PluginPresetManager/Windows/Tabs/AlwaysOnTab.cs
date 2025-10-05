using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginPresetManager.Windows.Tabs;

public class AlwaysOnTab
{
	private readonly Plugin plugin;
	private readonly PresetManager presetManager;

	public AlwaysOnTab(Plugin plugin, PresetManager presetManager)
	{
		this.plugin = plugin;
		this.presetManager = presetManager;
	}

	public void Draw()
	{
		ImGui.TextWrapped("Plugins in this list will ALWAYS be enabled, regardless of which preset is active.");
		ImGui.Spacing();
		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Note: PluginPresetManager is automatically added to this list to prevent disabling itself.");
		ImGui.Spacing();

		ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Always-On Plugins: {presetManager.GetAlwaysOnPlugins().Count}");
		ImGui.Separator();

		if (ImGui.BeginChild("AlwaysOnList", new Vector2(0, -35), true))
		{
			var installedPlugins = Plugin.PluginInterface.InstalledPlugins
				.GroupBy(p => p.InternalName)
				.ToDictionary(g => g.Key, g => g.First());

			foreach (var pluginName in presetManager.GetAlwaysOnPlugins().OrderBy(n => n).ToList())
			{
				var isInstalled = installedPlugins.TryGetValue(pluginName, out var pluginInfo);
				if (isInstalled)
				{
					var color = pluginInfo!.IsLoaded ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1);
					ImGui.TextColored(color, pluginInfo.IsLoaded ? "●" : "○");
					ImGui.SameLine();
					ImGui.TextUnformatted(pluginInfo.Name);
				}
				else
				{
					ImGui.TextColored(new Vector4(1, 0, 0, 1), "✗");
					ImGui.SameLine();
					ImGui.TextColored(new Vector4(1, 0, 0, 1), pluginName);
					ImGui.SameLine();
					ImGui.TextColored(new Vector4(0.8f, 0.3f, 0.3f, 1), "(missing)");
				}

				ImGui.SameLine();
				var isThisPlugin = pluginName == Plugin.PluginInterface.InternalName;
				if (isThisPlugin) ImGui.BeginDisabled();
				if (ImGui.SmallButton($"Remove##{pluginName}"))
				{
					if (!isThisPlugin)
						presetManager.RemoveAlwaysOnPlugin(pluginName);
				}
				if (isThisPlugin)
				{
					ImGui.EndDisabled();
					if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
						ImGui.SetTooltip("Cannot remove PluginPresetManager from always-on to prevent self-disable");
				}

				if (isInstalled && !pluginInfo!.IsLoaded)
				{
					ImGui.SameLine();
					ImGui.TextColored(new Vector4(1, 0, 0, 1), "(Plugin is not loaded!)");
				}
			}

			ImGui.EndChild();
		}

		if (ImGui.Button("Add Plugin to Always-On", new Vector2(-1, 0)))
		{
			ImGui.OpenPopup("SelectPluginAlwaysOn");
		}

		if (ImGui.BeginPopup("SelectPluginAlwaysOn"))
		{
			ImGui.TextUnformatted("Select plugin to add:");
			ImGui.Separator();

			foreach (var pi in Plugin.PluginInterface.InstalledPlugins.OrderBy(p => p.Name))
			{
				if (presetManager.GetAlwaysOnPlugins().Contains(pi.InternalName))
					continue;

				if (ImGui.Selectable(pi.Name))
				{
					presetManager.AddAlwaysOnPlugin(pi.InternalName);
					ImGui.CloseCurrentPopup();
				}
			}

			ImGui.EndPopup();
		}
	}
}


