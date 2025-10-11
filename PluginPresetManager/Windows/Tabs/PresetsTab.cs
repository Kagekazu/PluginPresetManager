using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using PluginPresetManager.Models;

namespace PluginPresetManager.Windows.Tabs;

public class PresetsTab
{
	private readonly Plugin plugin;
	private readonly Configuration config;
	private readonly PresetManager presetManager;

	private string newPresetName = string.Empty;
	private Preset? selectedPreset;
	private string searchFilter = string.Empty;
	private bool openAddPluginPopup = false;

	public PresetsTab(Plugin plugin, Configuration config, PresetManager presetManager)
	{
		this.plugin = plugin;
		this.config = config;
		this.presetManager = presetManager;
	}

	public void Draw()
	{
		using (var child = ImRaii.Child("PresetList", new Vector2(170, 0), true))
		{
			if (child)
			{
				ImGui.SetNextItemWidth(-1);
			ImGui.InputTextWithHint("##NewPreset", "New preset name...", ref newPresetName, 100);

			if (ImGui.Button("Create Empty##PresetsTabCreateBtn", new Vector2(-1, 0)))
			{
				if (!string.IsNullOrWhiteSpace(newPresetName))
				{
					var newPreset = new Preset
					{
						Name = newPresetName,
						CreatedAt = DateTime.Now,
						LastModified = DateTime.Now
					};
					presetManager.AddPreset(newPreset);
					selectedPreset = newPreset;
					newPresetName = string.Empty;
				}
			}

			ImGui.Separator();
			ImGui.TextUnformatted("Presets:");
			ImGui.Separator();

			foreach (var preset in presetManager.GetAllPresets())
			{
				var isSelected = selectedPreset == preset;
				var isLastApplied = config.LastAppliedPresetId == preset.Id;
				var isDefault = config.DefaultPresetId == preset.Id;

				using (isLastApplied ? ImRaii.PushColor(ImGuiCol.Text, new Vector4(0, 1, 0.5f, 1)) : null)
				{
					var displayName = preset.Name;
					if (isDefault)
					{
						displayName = $"★ {preset.Name}";
					}

					if (ImGui.Selectable($"{displayName}##{preset.Id}", isSelected))
					{
						selectedPreset = preset;
					}
				}

				if (ImGui.IsItemHovered())
				{
					using (ImRaii.Tooltip())
					{
						ImGui.TextUnformatted($"Plugins: {preset.EnabledPlugins.Count}");
						ImGui.TextUnformatted($"Created: {preset.CreatedAt:g}");
						if (preset.LastModified != preset.CreatedAt)
						{
							ImGui.TextUnformatted($"Modified: {preset.LastModified:g}");
						}
						if (!string.IsNullOrEmpty(preset.Description))
						{
							ImGui.Separator();
							ImGui.TextWrapped(preset.Description);
						}
						if (isDefault)
						{
							ImGui.Separator();
							ImGui.TextColored(new Vector4(1, 1, 0, 1), "★ Default (Auto-applies on login)");
						}
						if (isLastApplied)
						{
							ImGui.Separator();
							ImGui.TextColored(new Vector4(0, 1, 0.5f, 1), "Currently Applied");
						}
					}
				}
			}
			}
		}

		ImGui.SameLine();

		using (var child = ImRaii.Child("PresetDetails"))
		{
			if (child)
			{

			if (selectedPreset != null)
			{
				DrawPresetDetails(selectedPreset);
			}
			else
			{
				ImGui.TextUnformatted("Select a preset to view details");
				ImGui.Spacing();
				ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1),
					"Create a new preset using the button on the left,\nor select an existing preset from the list.");
			}
			}
		}

		if (selectedPreset != null && openAddPluginPopup)
		{
			ImGui.OpenPopup($"AddPluginToPreset###{selectedPreset.Id}");
			openAddPluginPopup = false;
		}

		if (selectedPreset != null)
		{
			using (var popup = ImRaii.Popup($"AddPluginToPreset###{selectedPreset.Id}"))
			{
				if (popup)
				{
					ImGui.TextUnformatted("Add plugins:");
					ImGui.InputTextWithHint("##AddPluginSearch", "Search...", ref searchFilter, 100);

					using (var childList = ImRaii.Child("AddPluginList", new Vector2(400, 300)))
					{
						if (childList)
						{
							var installedPlugins = Plugin.PluginInterface.InstalledPlugins
								.OrderBy(p => p.Name)
								.ToList();

							foreach (var plugin in installedPlugins)
							{
								if (selectedPreset.EnabledPlugins.Contains(plugin.InternalName))
									continue;

								if (presetManager.GetAlwaysOnPlugins().Contains(plugin.InternalName))
									continue;

								if (!string.IsNullOrEmpty(searchFilter) &&
									!plugin.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) &&
									!plugin.InternalName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
								{
									continue;
								}

								if (ImGui.Selectable($"{plugin.Name}##{plugin.InternalName}"))
								{
									selectedPreset.EnabledPlugins.Add(plugin.InternalName);
									presetManager.UpdatePreset(selectedPreset);
								}

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
							}
						}
					}
				}
			}
		}
	}

	private void DrawPresetDetails(Preset preset)
	{
		var presetName = preset.Name;
		ImGui.SetNextItemWidth(-1);
		if (ImGui.InputText("##PresetName", ref presetName, 100))
		{
			preset.Name = presetName;
			presetManager.UpdatePreset(preset);
		}
		ImGui.Separator();

		var description = preset.Description;
		if (ImGui.InputTextMultiline("##Desc", ref description, 500, new Vector2(-1, 35)))
		{
			preset.Description = description;
			presetManager.UpdatePreset(preset);
		}
		if (string.IsNullOrEmpty(description) && !ImGui.IsItemActive() && !ImGui.IsItemFocused())
		{
			var min = ImGui.GetItemRectMin();
			var dl = ImGui.GetWindowDrawList();
			dl.AddText(new Vector2(min.X + 4, min.Y + 4), ImGui.GetColorU32(ImGuiCol.TextDisabled), "Description...");
		}

		var preview = presetManager.GetPresetPreview(preset);
		if (preview.ToEnable.Any() || preview.ToDisable.Any())
		{
			ImGui.TextColored(new Vector4(1, 1, 0, 1), "Changes:");
			ImGui.SameLine();
			if (preview.ToEnable.Any())
				ImGui.TextColored(new Vector4(0, 1, 0, 1), $"+{preview.ToEnable.Count}");
			ImGui.SameLine();
			if (preview.ToDisable.Any())
				ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), $"-{preview.ToDisable.Count}");
			if (preview.Missing.Any())
			{
				ImGui.SameLine();
				ImGui.TextColored(new Vector4(1, 0, 0, 1), $"{preview.Missing.Count} missing");
			}
		}
		else
		{
			ImGui.TextColored(new Vector4(0, 1, 0, 1), "✓ Applied");
		}

		var style = ImGui.GetStyle();
		var totalButtonsWidth = 80f + 100f + 70f + (2 * style.ItemSpacing.X);
		var right = ImGui.GetContentRegionMax().X;
		ImGui.SetCursorPosX(right - totalButtonsWidth);
		if (ImGui.Button($"Apply##{preset.Id}_Apply", new Vector2(80, 0)))
		{
			_ = presetManager.ApplyPresetAsync(preset);
		}
		ImGui.SameLine();

		var isDefault = config.DefaultPresetId == preset.Id;
		using (isDefault ? ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1)) : null)
		{
			if (ImGui.Button(isDefault ? $"Default ✓##{preset.Id}_Default" : $"Set Default##{preset.Id}_Default", new Vector2(100, 0)))
			{
				if (isDefault)
				{
					config.DefaultPresetId = null;
				}
				else
				{
					config.DefaultPresetId = preset.Id;
				}
				Plugin.PluginInterface.SavePluginConfig(config);
			}
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(isDefault 
				? "Click to unset as default preset" 
				: "Set this preset to apply automatically when you log in to a character");
		}
		ImGui.SameLine();
		if (ImGui.Button($"Delete##{preset.Id}_Delete", new Vector2(70, 0)))
		{
			ImGui.OpenPopup($"DeleteConfirm###{preset.Id}");
		}

		if (ImGui.Button($"Add Enabled Plugins##{preset.Id}_AddEnabled", new Vector2(150, 0)))
		{
			var alwaysOn = presetManager.GetAlwaysOnPlugins();
			var addedCount = 0;
			foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
			{
				if (plugin.IsLoaded &&
					!preset.EnabledPlugins.Contains(plugin.InternalName) &&
					!alwaysOn.Contains(plugin.InternalName))
				{
					preset.EnabledPlugins.Add(plugin.InternalName);
					addedCount++;
				}
			}
			if (addedCount > 0)
			{
				presetManager.UpdatePreset(preset);
			}
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("Add all currently enabled plugins to this preset");
		}

		var trueValue = true;
		using (var modal = ImRaii.PopupModal($"DeleteConfirm###{preset.Id}", ref trueValue, ImGuiWindowFlags.AlwaysAutoResize))
		{
			if (modal)
			{
				ImGui.Text($"Are you sure you want to delete '{preset.Name}'?");
				ImGui.Spacing();

				if (ImGui.Button($"Yes##{preset.Id}_DeleteYes", new Vector2(120, 0)))
				{
					presetManager.DeletePreset(preset);
					selectedPreset = null;
					ImGui.CloseCurrentPopup();
				}

				ImGui.SameLine();

				if (ImGui.Button($"No##{preset.Id}_DeleteNo", new Vector2(120, 0)))
				{
					ImGui.CloseCurrentPopup();
				}
			}
		}

		ImGui.Separator();

		if (ImGui.Button($"Add Plugin##AddPlugin{preset.Id}", new Vector2(100, 0)))
		{
			searchFilter = string.Empty;
			openAddPluginPopup = true;
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip("Add individual plugins to this preset");
		}
		ImGui.SameLine();
		ImGui.Text($"Plugins: {preset.EnabledPlugins.Count}");

		using (var child = ImRaii.Child("PresetPlugins", new Vector2(0, 0), true))
		{
			if (child)
			{
				var installedPlugins = Plugin.PluginInterface.InstalledPlugins
				.GroupBy(p => p.InternalName)
				.ToDictionary(g => g.Key, g => g.First());
			var alwaysOnPlugins = presetManager.GetAlwaysOnPlugins();

			if (alwaysOnPlugins.Any())
			{
				ImGui.TextColored(new Vector4(0.5f, 0.5f, 1, 1), $"Always-On ({alwaysOnPlugins.Count}):");
				foreach (var pluginName in alwaysOnPlugins.OrderBy(p => installedPlugins.ContainsKey(p) ? installedPlugins[p].Name : p))
				{
					if (installedPlugins.TryGetValue(pluginName, out var plugin))
					{
						var color = plugin.IsLoaded ? new Vector4(0, 1, 0, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1);
						ImGui.TextColored(color, plugin.IsLoaded ? "●" : "○");
						ImGui.SameLine();
						ImGui.TextUnformatted(plugin.Name);
					}
					else
					{
						ImGui.TextColored(new Vector4(1, 0, 0, 1), pluginName);
						ImGui.SameLine();
						ImGui.TextColored(new Vector4(1, 0, 0, 1), "(missing)");
					}
				}
				ImGui.Separator();
			}

			if (preset.EnabledPlugins.Any())
			{
				ImGui.TextColored(new Vector4(1, 1, 1, 1), $"Selected ({preset.EnabledPlugins.Count}):");
				using (var table = ImRaii.Table("PresetSelectedTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV))
				{
					if (table)
					{
						ImGui.TableSetupColumn("Plugin", ImGuiTableColumnFlags.WidthStretch);
						ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80);
						foreach (var pluginName in preset.EnabledPlugins.OrderBy(x => x))
						{
							var isInstalled = installedPlugins.TryGetValue(pluginName, out var plugin);
							ImGui.TableNextRow();
							ImGui.TableNextColumn();
							if (isInstalled)
							{
								var color = plugin!.IsLoaded ? new Vector4(0, 1, 0, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1);
								ImGui.TextColored(color, plugin.IsLoaded ? "●" : "○");
								ImGui.SameLine();
								ImGui.TextUnformatted(plugin.Name);
							}
							else
							{
								ImGui.TextColored(new Vector4(1, 0, 0, 1), pluginName);
								ImGui.SameLine();
								ImGui.TextColored(new Vector4(1, 0, 0, 1), "(missing)");
							}
							ImGui.TableNextColumn();
							if (ImGui.SmallButton($"Remove##{pluginName}"))
							{
								preset.EnabledPlugins.Remove(pluginName);
								presetManager.UpdatePreset(preset);
							}
						}
					}
				}
			}
			else
			{
				ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "No plugins. Click 'Add' to add.");
			}
			}
		}
	}
}


