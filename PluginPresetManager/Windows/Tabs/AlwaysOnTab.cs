using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;

namespace PluginPresetManager.Windows.Tabs;

public class AlwaysOnTab
{
	private readonly Plugin plugin;
	private readonly PresetManager presetManager;
	private string searchFilter = string.Empty;
	
	private DateTime lastUpdateTime = DateTime.MinValue;
	private const int UpdateIntervalMs = 500;
	
	private Dictionary<string, IExposedPlugin>? installedPlugins;
	private List<IExposedPlugin>? pluginList;

	public AlwaysOnTab(Plugin plugin, PresetManager presetManager)
	{
		this.plugin = plugin;
		this.presetManager = presetManager;
	}

	public void Draw()
	{
		var now = DateTime.Now;
		if ((now - lastUpdateTime).TotalMilliseconds >= UpdateIntervalMs)
		{
			installedPlugins = Plugin.PluginInterface.InstalledPlugins
				.GroupBy(p => p.InternalName)
				.ToDictionary(g => g.Key, g => g.First());
			pluginList = Plugin.PluginInterface.InstalledPlugins.OrderBy(p => p.Name).ToList();
			lastUpdateTime = now;
		}
		
		if (installedPlugins == null || pluginList == null)
		{
			installedPlugins = Plugin.PluginInterface.InstalledPlugins
				.GroupBy(p => p.InternalName)
				.ToDictionary(g => g.Key, g => g.First());
			pluginList = Plugin.PluginInterface.InstalledPlugins.OrderBy(p => p.Name).ToList();
			lastUpdateTime = DateTime.Now;
		}
		
		ImGui.TextWrapped("Plugins in this list will ALWAYS be enabled, regardless of which preset is active.");
		ImGui.Spacing();
		ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1), "Note: PluginPresetManager is automatically added to this list to prevent disabling itself.");
		ImGui.Spacing();

		ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Always-On Plugins: {presetManager.GetAlwaysOnPlugins().Count}");
		ImGui.Separator();

		if (ImGui.BeginChild("AlwaysOnList", new Vector2(0, -35), true))
		{

			foreach (var pluginName in presetManager.GetAlwaysOnPlugins().OrderBy(n => n).ToList())
			{
				var isInstalled = installedPlugins!.TryGetValue(pluginName, out var pluginInfo);
				if (isInstalled)
				{
					var color = pluginInfo!.IsLoaded ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1);
					ImGui.TextColored(color, pluginInfo.IsLoaded ? "●" : "○");
					ImGui.SameLine();
					ImGui.TextUnformatted(pluginInfo.Name);
				}
				else
				{
					ImGui.TextColored(new Vector4(1, 0, 0, 1), pluginName);
					ImGui.SameLine();
					ImGui.TextColored(new Vector4(1, 0, 0, 1), "(missing)");
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
			searchFilter = string.Empty;
			ImGui.OpenPopup("SelectPluginAlwaysOn");
		}

		if (ImGui.BeginPopup("SelectPluginAlwaysOn"))
		{
			ImGui.TextUnformatted("Select plugin to add:");
			ImGui.InputTextWithHint("##AlwaysOnSearch", "Search...", ref searchFilter, 100);

			if (ImGui.BeginChild("AlwaysOnPluginList", new Vector2(400, 300)))
			{
				foreach (var pi in pluginList!)
				{
					if (presetManager.GetAlwaysOnPlugins().Contains(pi.InternalName))
						continue;

					if (!string.IsNullOrEmpty(searchFilter) &&
						!pi.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) &&
						!pi.InternalName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					if (ImGui.Selectable(pi.Name))
					{
						presetManager.AddAlwaysOnPlugin(pi.InternalName);
						ImGui.CloseCurrentPopup();
					}

					if (pi.IsDev)
					{
						ImGui.SameLine();
						ImGui.TextColored(new Vector4(1, 0, 1, 1), "[DEV]");
					}
					if (pi.IsThirdParty)
					{
						ImGui.SameLine();
						ImGui.TextColored(new Vector4(1, 1, 0, 1), "[3rd]");
					}
				}

				ImGui.EndChild();
			}

			ImGui.EndPopup();
		}
	}
}


