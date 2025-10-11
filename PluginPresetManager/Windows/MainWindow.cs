using System;
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
        using (var tabBar = ImRaii.TabBar("PresetTabs###main_tabs"))
        {
            if (!tabBar) return;

            using (var tabItem = ImRaii.TabItem("Presets###tab_presets"))
            {
                if (tabItem)
                    presetsTab.Draw();
            }

            using (var tabItem = ImRaii.TabItem("Always-On Plugins###tab_always"))
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
    }

    
}
