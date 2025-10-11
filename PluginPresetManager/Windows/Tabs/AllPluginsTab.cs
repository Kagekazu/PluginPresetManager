using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;

namespace PluginPresetManager.Windows.Tabs;

public class AllPluginsTab
{
	private readonly PresetManager presetManager;
	private string searchFilter = string.Empty;
	
	private DateTime lastUpdateTime = DateTime.MinValue;
	private const int UpdateIntervalMs = 500;
	
	private List<IExposedPlugin>? allPlugins;
	private int loadedCount;
	private int totalCount;

	public AllPluginsTab(PresetManager presetManager)
	{
		this.presetManager = presetManager;
	}

	public void Draw()
	{
		var now = DateTime.Now;
		if ((now - lastUpdateTime).TotalMilliseconds >= UpdateIntervalMs)
		{
			allPlugins = Plugin.PluginInterface.InstalledPlugins.ToList();
			loadedCount = allPlugins.Count(p => p.IsLoaded);
			totalCount = allPlugins.Count;
			lastUpdateTime = now;
		}
		
		if (allPlugins == null)
		{
			allPlugins = Plugin.PluginInterface.InstalledPlugins.ToList();
			loadedCount = allPlugins.Count(p => p.IsLoaded);
			totalCount = allPlugins.Count;
			lastUpdateTime = DateTime.Now;
		}
		
		ImGui.TextUnformatted("All Installed Plugins:");
		ImGui.Separator();

		ImGui.SetNextItemWidth(-1);
		ImGui.InputTextWithHint("##AllPluginsSearch", "Search plugins...", ref searchFilter, 100);
		ImGui.Spacing();

		ImGui.Text($"Total: {totalCount} | Loaded: {loadedCount} | Unloaded: {totalCount - loadedCount}");
		ImGui.Separator();

		using (var child = ImRaii.Child("AllPluginsListChild"))
		{
			if (child)
			{
				using (var table = ImRaii.Table("AllPluginsTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY))
				{
					if (table)
					{
						ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 60);
						ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
						ImGui.TableSetupColumn("Version", ImGuiTableColumnFlags.WidthFixed, 100);
						ImGui.TableSetupColumn("Always-On", ImGuiTableColumnFlags.WidthFixed, 80);
						ImGui.TableHeadersRow();

						foreach (var plugin in allPlugins!.OrderBy(p => p.Name))
						{
							if (!string.IsNullOrEmpty(searchFilter) &&
								!plugin.Name.Contains(searchFilter, System.StringComparison.OrdinalIgnoreCase) &&
								!plugin.InternalName.Contains(searchFilter, System.StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}

							ImGui.TableNextRow();

							ImGui.TableNextColumn();
							if (plugin.IsLoaded)
							{
								ImGui.TextColored(new Vector4(0, 1, 0, 1), "Loaded");
							}
							else
							{
								ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "Unloaded");
							}

							ImGui.TableNextColumn();
							ImGui.TextUnformatted(plugin.Name);

							if (plugin.IsDev)
							{
								ImGui.SameLine();
								ImGui.TextColored(new Vector4(1, 0, 1, 1), "[DEV]");
							}
							if (plugin.IsThirdParty)
							{
								ImGui.SameLine();
								ImGui.TextColored(new Vector4(1, 1, 0, 1), "[3rd]");
							}

							ImGui.TableNextColumn();
							ImGui.TextUnformatted(plugin.Version?.ToString() ?? "N/A");

							ImGui.TableNextColumn();
							var isAlwaysOn = presetManager.GetAlwaysOnPlugins().Contains(plugin.InternalName);
							var isThisPlugin = plugin.InternalName == Plugin.PluginInterface.InternalName;

							if (isThisPlugin)
							{
								using (ImRaii.Disabled())
								{
									var locked = true;
									ImGui.Checkbox($"##AllPlugins_AlwaysOn_{plugin.InternalName}", ref locked);
								}
								if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
								{
									ImGui.SetTooltip("PluginPresetManager is always-on to prevent self-disable");
								}
							}
							else if (ImGui.Checkbox($"##AllPlugins_AlwaysOn_{plugin.InternalName}", ref isAlwaysOn))
							{
								if (isAlwaysOn)
									presetManager.AddAlwaysOnPlugin(plugin.InternalName);
								else
									presetManager.RemoveAlwaysOnPlugin(plugin.InternalName);
							}
						}
					}
				}
			}
		}
	}
}


