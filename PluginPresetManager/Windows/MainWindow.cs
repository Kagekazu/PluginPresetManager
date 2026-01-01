using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using PluginPresetManager.Models;

namespace PluginPresetManager.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration config;
    private readonly PresetManager presetManager;

    private readonly PluginPresetManager.Windows.Tabs.PresetsTab presetsTab;
    private readonly PluginPresetManager.Windows.Tabs.AlwaysOnTab alwaysOnTab;
    private readonly PluginPresetManager.Windows.Tabs.AllPluginsTab allPluginsTab;
    private readonly PluginPresetManager.Windows.Tabs.HelpTab helpTab;
    private readonly PluginPresetManager.Windows.Tabs.SettingsTab settingsTab;

    private bool focusSettingsTabNextDraw = false;
    private bool showNewCharacterPopup = false;

    public MainWindow(Plugin plugin)
        : base("Plugin Preset Manager###PluginPresetManager")
    {
        Size = new Vector2(650, 520);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
        this.config = plugin.Configuration;
        this.presetManager = plugin.PresetManager;

        presetsTab = new PluginPresetManager.Windows.Tabs.PresetsTab(plugin, presetManager);
        alwaysOnTab = new PluginPresetManager.Windows.Tabs.AlwaysOnTab(plugin, presetManager);
        allPluginsTab = new PluginPresetManager.Windows.Tabs.AllPluginsTab(presetManager);
        helpTab = new PluginPresetManager.Windows.Tabs.HelpTab();
        settingsTab = new PluginPresetManager.Windows.Tabs.SettingsTab(plugin, presetManager);
    }

    public void Dispose() { }

    public void FocusSettingsTab()
    {
        focusSettingsTabNextDraw = true;
        IsOpen = true;
    }

    public override void Draw()
    {
        DrawCharacterSelector();
        DrawProgressBar();

        ImGui.Separator();

        using (var tabBar = ImRaii.TabBar("PresetTabs###main_tabs"))
        {
            if (!tabBar) return;

            using (var tabItem = ImRaii.TabItem("Presets###tab_presets"))
            {
                if (tabItem)
                    presetsTab.Draw();
            }

            using (var tabItem = ImRaii.TabItem("Always-On###tab_always"))
            {
                if (tabItem)
                    alwaysOnTab.Draw();
            }

            using (var tabItem = ImRaii.TabItem("All Plugins###tab_all"))
            {
                if (tabItem)
                    allPluginsTab.Draw();
            }

            using (var tabItem = ImRaii.TabItem("Help###tab_help"))
            {
                if (tabItem)
                    helpTab.Draw();
            }

            var settingsFlags = focusSettingsTabNextDraw ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            using (var tabItem = ImRaii.TabItem("Settings###tab_settings", settingsFlags))
            {
                if (tabItem)
                {
                    if (focusSettingsTabNextDraw) focusSettingsTabNextDraw = false;
                    settingsTab.Draw();
                }
            }
        }

        DrawNewCharacterPopup();
    }

    private void DrawCharacterSelector()
    {
        var characters = presetManager.GetAllCharacters();
        var currentId = presetManager.CurrentCharacterId;

        // Build display list
        var displayItems = new List<(string name, ulong id)> { ("Global", CharacterStorage.GlobalContentId) };
        foreach (var c in characters)
        {
            displayItems.Add((c.DisplayName, c.ContentId));
        }

        // Find current index
        var currentIndex = 0;
        for (var i = 0; i < displayItems.Count; i++)
        {
            if (displayItems[i].id == currentId)
            {
                currentIndex = i;
                break;
            }
        }

        ImGui.Text("Character:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);

        if (ImGui.BeginCombo("##CharacterSelector", displayItems[currentIndex].name))
        {
            for (var i = 0; i < displayItems.Count; i++)
            {
                var isSelected = i == currentIndex;
                var isCurrent = displayItems[i].id == Plugin.PlayerState.ContentId;

                var label = displayItems[i].name;
                if (isCurrent && displayItems[i].id != CharacterStorage.GlobalContentId)
                {
                    label = $"{label} (Current)";
                }

                if (ImGui.Selectable(label, isSelected))
                {
                    if (displayItems[i].id != currentId)
                    {
                        var targetId = displayItems[i].id;

                        // Check if character has data, if not show popup
                        if (targetId != CharacterStorage.GlobalContentId && !presetManager.HasCharacterData(targetId))
                        {
                            showNewCharacterPopup = true;
                        }

                        presetManager.SwitchCharacter(targetId);
                        plugin.SaveConfiguration();
                    }
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        // Show current preset status
        ImGui.SameLine();
        var lastApplied = presetManager.GetLastAppliedPreset();
        if (lastApplied != null)
        {
            ImGui.TextColored(new Vector4(0, 1, 0.5f, 1), $"Active: {lastApplied.Name}");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "No preset active");
        }

        // Undo button
        if (presetManager.CanUndo)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Undo"))
            {
                _ = presetManager.UndoLastApplyAsync();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Revert to previous plugin state");
            }
        }
    }

    private void DrawProgressBar()
    {
        if (!presetManager.IsApplying) return;

        ImGui.ProgressBar(presetManager.ApplyingProgress, new Vector2(-1, 0), presetManager.ApplyingStatus);
    }

    private void DrawNewCharacterPopup()
    {
        if (showNewCharacterPopup)
        {
            ImGui.OpenPopup("NewCharacterSetup");
            showNewCharacterPopup = false;
        }

        var open = true;
        if (ImGui.BeginPopupModal("NewCharacterSetup", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("This character has no presets yet.");
            ImGui.Text("Would you like to:");
            ImGui.Spacing();

            if (ImGui.Button("Copy from Global", new Vector2(200, 0)))
            {
                presetManager.CopyFromGlobal();
                ImGui.CloseCurrentPopup();
            }

            var characters = presetManager.GetAllCharacters();
            if (characters.Any())
            {
                ImGui.Spacing();
                ImGui.Text("Or copy from another character:");

                foreach (var c in characters)
                {
                    if (c.ContentId == presetManager.CurrentCharacterId) continue;
                    if (!presetManager.HasCharacterData(c.ContentId)) continue;

                    if (ImGui.Button($"Copy from {c.DisplayName}##copy_{c.ContentId}", new Vector2(200, 0)))
                    {
                        presetManager.CopyFromCharacter(c.ContentId);
                        ImGui.CloseCurrentPopup();
                    }
                }
            }

            ImGui.Spacing();
            if (ImGui.Button("Start Fresh", new Vector2(200, 0)))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }
}
