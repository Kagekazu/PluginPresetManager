using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using PluginPresetManager.Models;

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
        var characters = presetManager.GetAllCharacters();
        var currentId = presetManager.CurrentCharacterId;

        var items = new List<(string name, ulong id)> { ("Global", CharacterStorage.GlobalContentId) };
        foreach (var c in characters)
        {
            items.Add((c.DisplayName, c.ContentId));
        }

        var currentIndex = 0;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].id == currentId)
            {
                currentIndex = i;
                break;
            }
        }

        ImGui.Text("Character:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);

        if (ImGui.BeginCombo("##CharSelect", items[currentIndex].name))
        {
            for (var i = 0; i < items.Count; i++)
            {
                var isSelected = i == currentIndex;
                var isCurrent = items[i].id == Plugin.PlayerState.ContentId;

                var label = items[i].name;
                if (isCurrent && items[i].id != CharacterStorage.GlobalContentId)
                {
                    label += " (you)";
                }

                if (ImGui.Selectable(label, isSelected))
                {
                    if (items[i].id != currentId)
                    {
                        presetManager.SwitchCharacter(items[i].id);
                        plugin.SaveConfiguration();
                    }
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }
}
