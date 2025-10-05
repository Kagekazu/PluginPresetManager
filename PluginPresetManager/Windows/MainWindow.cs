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

    
    private readonly PluginPresetManager.Windows.Tabs.PresetsTab presetsTab;
    private readonly PluginPresetManager.Windows.Tabs.AlwaysOnTab alwaysOnTab;
    private readonly PluginPresetManager.Windows.Tabs.AllPluginsTab allPluginsTab;
    private readonly PluginPresetManager.Windows.Tabs.HelpTab helpTab;
    private readonly PluginPresetManager.Windows.Tabs.SettingsTab settingsTab;

    private bool focusSettingsTabNextDraw = false;

    

    public MainWindow(Plugin plugin)
        : base("Plugin Preset Manager###PluginPresetManager")
    {
        Size = new Vector2(600, 480);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
        this.config = plugin.Configuration;
        this.presetManager = plugin.PresetManager;

        
        presetsTab = new PluginPresetManager.Windows.Tabs.PresetsTab(plugin, config, presetManager);
        alwaysOnTab = new PluginPresetManager.Windows.Tabs.AlwaysOnTab(plugin, presetManager);
        allPluginsTab = new PluginPresetManager.Windows.Tabs.AllPluginsTab(presetManager);
        helpTab = new PluginPresetManager.Windows.Tabs.HelpTab();
        settingsTab = new PluginPresetManager.Windows.Tabs.SettingsTab(config, presetManager);
    }

    public void Dispose() { }

    public void FocusSettingsTab()
    {
        focusSettingsTabNextDraw = true;
        IsOpen = true;
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("PresetTabs###main_tabs"))
        {
            if (ImGui.BeginTabItem("Presets###tab_presets"))
            {
                presetsTab.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Always-On Plugins###tab_always"))
            {
                alwaysOnTab.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("All Plugins###tab_all"))
            {
                allPluginsTab.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Help###tab_help"))
            {
                helpTab.Draw();
                ImGui.EndTabItem();
            }

            var settingsFlags = focusSettingsTabNextDraw ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            if (ImGui.BeginTabItem("Settings###tab_settings", settingsFlags))
            {
                if (focusSettingsTabNextDraw) focusSettingsTabNextDraw = false;
                settingsTab.Draw();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    
}
