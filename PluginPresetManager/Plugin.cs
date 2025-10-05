using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using PluginPresetManager.Windows;

namespace PluginPresetManager;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/ppreset";
    private const string CommandNameShort = "/ppm";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; init; }
    public PresetStorage Storage { get; init; }
    public PresetManager PresetManager { get; init; }

    public readonly WindowSystem WindowSystem = new("PluginPresetManager");
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Storage = new PresetStorage(PluginInterface, Log);

        PresetManager = new PresetManager(
            PluginInterface,
            CommandManager,
            ChatGui,
            Log,
            Configuration,
            Storage);

        var thisPluginInternalName = PluginInterface.InternalName;
        if (!PresetManager.GetAlwaysOnPlugins().Contains(thisPluginInternalName))
        {
            Log.Info("Adding PluginPresetManager to always-on list to prevent self-disable");
            PresetManager.AddAlwaysOnPlugin(thisPluginInternalName);
        }

        if (Configuration.DefaultPresetId.HasValue)
        {
            var defaultPreset = PresetManager.GetAllPresets()
                .FirstOrDefault(p => p.Id == Configuration.DefaultPresetId.Value);
            if (defaultPreset != null)
            {
                Log.Info($"Auto-applying default preset: {defaultPreset.Name}");
                _ = PresetManager.ApplyPresetAsync(defaultPreset);
            }
            else
            {
                Log.Warning($"Default preset ID {Configuration.DefaultPresetId.Value} not found");
            }
        }

        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Plugin Preset Manager window"
        });

        CommandManager.AddHandler(CommandNameShort, new CommandInfo(OnCommandShort)
        {
            HelpMessage = "Apply a preset by name or 'alwayson' to disable all except always-on. Usage: /ppm <preset name|alwayson>"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Info($"Plugin Preset Manager loaded successfully");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandNameShort);

        Log.Info("Plugin Preset Manager disposed");
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    private void OnCommandShort(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            MainWindow.Toggle();
            return;
        }

        var argument = args.Trim();

        if (argument.Equals("alwayson", StringComparison.OrdinalIgnoreCase))
        {
            ChatGui.Print("[Preset] Applying always-on only mode...");
            Log.Info("Applying always-on only mode via command");
            _ = PresetManager.ApplyAlwaysOnOnlyAsync();
            return;
        }

        var allPresets = PresetManager.GetAllPresets();
        var preset = allPresets.FirstOrDefault(p =>
            p.Name.Equals(argument, StringComparison.OrdinalIgnoreCase));

        if (preset != null)
        {
            ChatGui.Print($"[Preset] Applying preset '{preset.Name}'...");
            Log.Info($"Applying preset '{preset.Name}' via command");
            _ = PresetManager.ApplyPresetAsync(preset);
        }
        else
        {
            ChatGui.PrintError($"[Preset] Preset '{argument}' not found.");
            if (allPresets.Any())
            {
                ChatGui.Print("[Preset] Available presets:");
                foreach (var p in allPresets)
                {
                    ChatGui.Print($"  - {p.Name}");
                }
                ChatGui.Print("[Preset] Special commands:");
                ChatGui.Print("  - alwayson (disable everything except always-on plugins)");
            }
            else
            {
                ChatGui.Print("[Preset] No presets available. Use /ppreset to create one.");
                ChatGui.Print("[Preset] Special commands:");
                ChatGui.Print("  - alwayson (disable everything except always-on plugins)");
            }
        }
    }

    public void ToggleMainUi() => MainWindow.Toggle();

    private void OpenConfigUi()
    {
        MainWindow.FocusSettingsTab();
    }
}
