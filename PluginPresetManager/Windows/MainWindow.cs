using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using PluginPresetManager.Models;

namespace PluginPresetManager.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration config;
    private readonly PresetManager presetManager;

    private string newPresetName = string.Empty;
    private Preset? selectedPreset;
    private string searchFilter = string.Empty;

    public MainWindow(Plugin plugin)
        : base("Plugin Preset Manager###PluginPresetManager")
    {
        Size = new Vector2(600, 480);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
        this.config = plugin.Configuration;
        this.presetManager = plugin.PresetManager;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("PresetTabs###main_tabs"))
        {
            if (ImGui.BeginTabItem("Presets###tab_presets"))
            {
                DrawPresetsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Always-On Plugins###tab_always"))
            {
                DrawAlwaysOnTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("All Plugins###tab_all"))
            {
                DrawAllPluginsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Help###tab_help"))
            {
                DrawHelpTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawPresetsTab()
    {
        if (ImGui.BeginChild("PresetList", new Vector2(170, 0), true))
        {
            ImGui.InputTextWithHint("##NewPreset", "New preset name...", ref newPresetName, 100);

            if (ImGui.Button("Create Empty", new Vector2(-1, 0)))
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

                if (isLastApplied)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 0.5f, 1));
                }

                var displayName = preset.Name;
                if (isDefault)
                {
                    displayName = $"★ {preset.Name}";
                }

                if (ImGui.Selectable($"{displayName}##{preset.Id}", isSelected))
                {
                    selectedPreset = preset;
                }

                if (isLastApplied)
                {
                    ImGui.PopStyleColor();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
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
                    ImGui.EndTooltip();
                }
            }

            ImGui.EndChild();
        }

        ImGui.SameLine();

        if (ImGui.BeginChild("PresetDetails"))
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

            ImGui.EndChild();
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

        var preview = presetManager.GetPresetPreview(preset);
        if (preview.ToEnable.Any() || preview.ToDisable.Any())
        {
            ImGui.TextColored(new Vector4(1, 1, 0, 1), "Changes:");
            ImGui.SameLine();
            if (preview.ToEnable.Any())
                ImGui.TextColored(new Vector4(0, 1, 0, 1), $"+{preview.ToEnable.Count}");
            ImGui.SameLine();
            if (preview.ToDisable.Any())
                ImGui.TextColored(new Vector4(1, 0, 0, 1), $"-{preview.ToDisable.Count}");
            if (preview.Missing.Any())
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0, 0, 1), $"⚠{preview.Missing.Count} missing");
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "✓ Applied");
        }

        if (ImGui.Button("Apply", new Vector2(80, 0)))
        {
            _ = presetManager.ApplyPresetAsync(preset);
        }

        ImGui.SameLine();

        var isDefault = config.DefaultPresetId == preset.Id;
        if (isDefault)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1));
        }
        if (ImGui.Button(isDefault ? "Default ✓" : "Set Default", new Vector2(100, 0)))
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
        if (isDefault)
        {
            ImGui.PopStyleColor();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(isDefault ? "Click to unset as default preset" : "Set this preset to apply automatically on login");
        }

        ImGui.SameLine();

        if (ImGui.Button("Delete", new Vector2(70, 0)))
        {
            ImGui.OpenPopup($"DeleteConfirm###{preset.Id}");
        }

        if (ImGui.Button("Add Enabled Plugins", new Vector2(150, 0)))
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
        if (ImGui.BeginPopupModal($"DeleteConfirm###{preset.Id}", ref trueValue, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text($"Are you sure you want to delete '{preset.Name}'?");
            ImGui.Spacing();

            if (ImGui.Button("Yes", new Vector2(120, 0)))
            {
                presetManager.DeletePreset(preset);
                selectedPreset = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            if (ImGui.Button("No", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        ImGui.Separator();

        ImGui.Text($"Plugins: {preset.EnabledPlugins.Count}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Add"))
        {
            ImGui.OpenPopup($"AddPluginToPreset###{preset.Id}");
        }

        if (ImGui.BeginPopup($"AddPluginToPreset###{preset.Id}"))
        {
            ImGui.TextUnformatted("Add plugins:");
            ImGui.InputTextWithHint("##AddPluginSearch", "Search...", ref searchFilter, 100);

            if (ImGui.BeginChild("AddPluginList", new Vector2(400, 300)))
            {
                var installedPlugins = Plugin.PluginInterface.InstalledPlugins
                    .OrderBy(p => p.Name)
                    .ToList();

                foreach (var plugin in installedPlugins)
                {
                    if (preset.EnabledPlugins.Contains(plugin.InternalName))
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
                        preset.EnabledPlugins.Add(plugin.InternalName);
                        presetManager.UpdatePreset(preset);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted($"Version: {plugin.Version}");
                        if (plugin.IsDev) ImGui.TextColored(new Vector4(1, 0, 1, 1), "[DEV Plugin]");
                        if (plugin.IsThirdParty) ImGui.TextColored(new Vector4(1, 1, 0, 1), "[Third-Party]");
                        ImGui.EndTooltip();
                    }
                }

                ImGui.EndChild();
            }

            ImGui.EndPopup();
        }

        if (ImGui.BeginChild("PresetPlugins", new Vector2(0, 0), true))
        {
            var installedPlugins = Plugin.PluginInterface.InstalledPlugins
                .GroupBy(p => p.InternalName)
                .ToDictionary(g => g.Key, g => g.First());
            var alwaysOnPlugins = presetManager.GetAlwaysOnPlugins();

            var alwaysOnInPreset = alwaysOnPlugins.Where(p => installedPlugins.ContainsKey(p)).ToList();
            if (alwaysOnInPreset.Any())
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 1, 1), $"Always-On ({alwaysOnInPreset.Count}):");
                foreach (var pluginName in alwaysOnInPreset.OrderBy(p => installedPlugins[p].Name))
                {
                    var plugin = installedPlugins[pluginName];
                    var color = plugin.IsLoaded ? new Vector4(0, 1, 0, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1);
                    ImGui.TextColored(color, plugin.IsLoaded ? "●" : "○");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(plugin.Name);
                }
                ImGui.Separator();
            }

            if (preset.EnabledPlugins.Any())
            {
                ImGui.TextColored(new Vector4(1, 1, 1, 1), $"Selected ({preset.EnabledPlugins.Count}):");
                foreach (var pluginName in preset.EnabledPlugins.OrderBy(x => x))
                {
                    var isInstalled = installedPlugins.TryGetValue(pluginName, out var plugin);

                    if (isInstalled)
                    {
                        var color = plugin!.IsLoaded ? new Vector4(0, 1, 0, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1);
                        ImGui.TextColored(color, plugin.IsLoaded ? "●" : "○");
                        ImGui.SameLine();
                        ImGui.TextUnformatted(plugin.Name);
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - 40);
                        if (ImGui.SmallButton($"X##{pluginName}"))
                        {
                            preset.EnabledPlugins.Remove(pluginName);
                            presetManager.UpdatePreset(preset);
                        }
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), "✗");
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), pluginName);
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.8f, 0.3f, 0.3f, 1), "(missing)");
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - 40);
                        if (ImGui.SmallButton($"X##{pluginName}"))
                        {
                            preset.EnabledPlugins.Remove(pluginName);
                            presetManager.UpdatePreset(preset);
                        }
                    }
                }
            }
            else
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "No plugins. Click 'Add' to add.");
            }

            ImGui.EndChild();
        }
    }

    private void DrawAlwaysOnTab()
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

            foreach (var pluginName in presetManager.GetAlwaysOnPlugins().ToList())
            {
                var isInstalled = installedPlugins.TryGetValue(pluginName, out var plugin);

                if (isInstalled)
                {
                    var color = plugin!.IsLoaded
                        ? new Vector4(0, 1, 0, 1)
                        : new Vector4(1, 0, 0, 1);

                    ImGui.TextColored(color, plugin.IsLoaded ? "●" : "○");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(plugin.Name);
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
                if (isThisPlugin)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.SmallButton($"Remove##{pluginName}"))
                {
                    if (!isThisPlugin)
                    {
                        presetManager.RemoveAlwaysOnPlugin(pluginName);
                    }
                }

                if (isThisPlugin)
                {
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.SetTooltip("Cannot remove PluginPresetManager from always-on to prevent self-disable");
                    }
                }

                if (isInstalled && !plugin!.IsLoaded)
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

            foreach (var plugin in Plugin.PluginInterface.InstalledPlugins.OrderBy(p => p.Name))
            {
                if (presetManager.GetAlwaysOnPlugins().Contains(plugin.InternalName))
                    continue;

                if (ImGui.Selectable(plugin.Name))
                {
                    presetManager.AddAlwaysOnPlugin(plugin.InternalName);
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndPopup();
        }
    }

    private void DrawAllPluginsTab()
    {
        ImGui.TextUnformatted("All Installed Plugins:");
        ImGui.Separator();

        ImGui.InputTextWithHint("##Search", "Search plugins...", ref searchFilter, 100);
        ImGui.Spacing();

        var allPlugins = Plugin.PluginInterface.InstalledPlugins.ToList();
        var loadedCount = allPlugins.Count(p => p.IsLoaded);
        ImGui.Text($"Total: {allPlugins.Count} | Loaded: {loadedCount} | Unloaded: {allPlugins.Count - loadedCount}");
        ImGui.Separator();

        if (ImGui.BeginChild("AllPluginsList"))
        {
            if (ImGui.BeginTable("PluginTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY))
            {
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Version", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("Always-On", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableHeadersRow();

                foreach (var plugin in allPlugins.OrderBy(p => p.Name))
                {
                    if (!string.IsNullOrEmpty(searchFilter) &&
                        !plugin.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) &&
                        !plugin.InternalName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
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
                    ImGui.TextUnformatted(plugin.Version.ToString());

                    ImGui.TableNextColumn();
                    var isAlwaysOn = presetManager.GetAlwaysOnPlugins().Contains(plugin.InternalName);
                    var isThisPlugin = plugin.InternalName == Plugin.PluginInterface.InternalName;

                    if (isThisPlugin)
                    {
                        ImGui.BeginDisabled();
                        var locked = true;
                        ImGui.Checkbox($"##{plugin.InternalName}", ref locked);
                        ImGui.EndDisabled();
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip("PluginPresetManager is always-on to prevent self-disable");
                        }
                    }
                    else if (ImGui.Checkbox($"##{plugin.InternalName}", ref isAlwaysOn))
                    {
                        if (isAlwaysOn)
                        {
                            presetManager.AddAlwaysOnPlugin(plugin.InternalName);
                        }
                        else
                        {
                            presetManager.RemoveAlwaysOnPlugin(plugin.InternalName);
                        }
                    }
                }

                ImGui.EndTable();
            }

            ImGui.EndChild();
        }
    }

    private void DrawHelpTab()
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

        if (ImGui.Button("Open GitHub Repository", new Vector2(200, 0)))
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
