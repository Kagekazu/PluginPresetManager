using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using PluginPresetManager.Models;
using PluginPresetManager.UI;

namespace PluginPresetManager.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly PresetManager presetManager;

    private readonly Tabs.ProfilesTab profilesTab;
    private readonly Tabs.ManageTab manageTab;
    private readonly Tabs.SettingsTab settingsTab;
    private readonly Tabs.HelpTab helpTab;

    private bool focusSettingsTabNextDraw = false;

    public MainWindow(Plugin plugin)
        : base("Plugin Preset Manager###PluginPresetManager")
    {
        Size = new Vector2(550, 450);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
        this.presetManager = plugin.PresetManager;

        profilesTab = new Tabs.ProfilesTab(presetManager);
        manageTab = new Tabs.ManageTab(plugin, presetManager);
        settingsTab = new Tabs.SettingsTab(plugin, presetManager);
        helpTab = new Tabs.HelpTab();
    }

    public void Dispose() { }

    public void FocusSettingsTab()
    {
        focusSettingsTabNextDraw = true;
        IsOpen = true;
    }

    public override void Draw()
    {
        DrawHeader();

        ImGui.Spacing();

        using (var tabBar = ImRaii.TabBar("MainTabs"))
        {
            if (!tabBar) return;

            using (var tab = ImRaii.TabItem("Profiles"))
            {
                if (tab)
                {
                    ImGui.Spacing();
                    profilesTab.Draw();
                }
            }

            using (var tab = ImRaii.TabItem("Manage"))
            {
                if (tab)
                {
                    ImGui.Spacing();
                    manageTab.Draw();
                }
            }

            var settingsFlags = focusSettingsTabNextDraw ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            using (var tab = ImRaii.TabItem("Settings", settingsFlags))
            {
                if (tab)
                {
                    if (focusSettingsTabNextDraw) focusSettingsTabNextDraw = false;
                    ImGui.Spacing();
                    settingsTab.Draw();
                }
            }

            using (var tab = ImRaii.TabItem("Help"))
            {
                if (tab)
                {
                    ImGui.Spacing();
                    helpTab.Draw();
                }
            }
        }
    }

    private void DrawHeader()
    {
        if (!presetManager.HasCharacter)
        {
            ImGui.TextColored(Colors.Warning, "Please log in to a character to use presets.");
            return;
        }

        var characters = presetManager.GetAllCharacters();
        var currentId = presetManager.CurrentCharacterId;

        if (characters.Count <= 1)
        {
            ImGui.Text($"Character: {presetManager.CurrentData.DisplayName}");
            return;
        }

        var items = characters.Select(c => (c.DisplayName, c.ContentId)).ToList();

        var currentIndex = 0;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].ContentId == currentId)
            {
                currentIndex = i;
                break;
            }
        }

        ImGui.Text("Character:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);

        using (var combo = ImRaii.Combo("##CharSelect", items[currentIndex].DisplayName))
        {
            if (combo)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var isSelected = i == currentIndex;
                    var isCurrent = items[i].ContentId == Plugin.PlayerState.ContentId;

                    var label = items[i].DisplayName;
                    if (isCurrent)
                    {
                        label += " (you)";
                    }

                    if (ImGui.Selectable(label, isSelected))
                    {
                        if (items[i].ContentId != currentId)
                        {
                            presetManager.SwitchCharacter(items[i].ContentId);
                            plugin.SaveConfiguration();
                        }
                    }

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
            }
        }
    }
}
