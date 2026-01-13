using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using PluginPresetManager.UI;

namespace PluginPresetManager.Windows;

public class DtrPopupWindow : Window
{
    private readonly Plugin plugin;
    private readonly PresetManager presetManager;
    private bool justOpened = false;
    private bool isHovered = false;
    private bool wasApplying = false;

    public DtrPopupWindow(Plugin plugin)
        : base("###PresetQuickSelect",
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        this.presetManager = plugin.PresetManager;

        RespectCloseHotkey = false;
    }

    public override void PreDraw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4, 4));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 2f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.3f, 0.3f, 0.3f, 1f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(5);

        if (justOpened)
        {
            justOpened = false;
        }
        else if (IsOpen && !presetManager.IsApplying && !isHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            IsOpen = false;
        }
    }

    public override void Draw()
    {
        isHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows);
        var width = 160f;

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            IsOpen = false;
            return;
        }

        if (!presetManager.HasCharacter)
        {
            ImGui.TextColored(Colors.TextDisabled, "Not logged in");
            return;
        }

        if (presetManager.IsApplying)
        {
            wasApplying = true;
            ImGui.TextColored(Colors.TextMuted, presetManager.ApplyingStatus);
            ImGui.ProgressBar(presetManager.ApplyingProgress, new Vector2(width, 0), "");
            return;
        }

        if (wasApplying)
        {
            wasApplying = false;
            IsOpen = false;
            return;
        }

        var presets = presetManager.GetAllPresets();
        var sharedPresets = presetManager.GetSharedPresets();
        var lastApplied = presetManager.GetLastAppliedPreset();
        var isAlwaysOnActive = presetManager.WasLastAppliedAlwaysOn;

        if (presets.Count == 0 && sharedPresets.Count == 0)
        {
            ImGui.TextColored(Colors.TextDisabled, "No presets");
            if (DrawMenuItem("Open Manager...", false))
            {
                plugin.ToggleMainUi();
                IsOpen = false;
            }
            return;
        }

        foreach (var preset in presets)
        {
            var isActive = lastApplied?.Name == preset.Name && !isAlwaysOnActive;

            if (DrawMenuItem(preset.Name, isActive))
            {
                _ = presetManager.ApplyPresetAsync(preset);
            }
        }

        if (sharedPresets.Count > 0)
        {
            if (presets.Count > 0)
            {
                ImGui.Spacing();
                ImGui.TextColored(Colors.TextMuted, "  Shared");
            }

            foreach (var preset in sharedPresets)
            {
                var isActive = lastApplied?.Name == preset.Name && !isAlwaysOnActive;

                if (DrawMenuItem(preset.Name, isActive))
                {
                    _ = presetManager.ApplyPresetAsync(preset);
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (DrawMenuItem("Always-On Only", isAlwaysOnActive))
        {
            _ = presetManager.ApplyAlwaysOnOnlyAsync();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (DrawMenuItem("Rescue Windows", false))
        {
            plugin.WindowRescueHelper.RescueAllOffScreen();
        }

        if (DrawMenuItem("Open Manager...", false))
        {
            plugin.ToggleMainUi();
            IsOpen = false;
        }

        if (DrawMenuItem("Close", false))
        {
            IsOpen = false;
        }
    }

    private bool DrawMenuItem(string label, bool isActive)
    {
        var width = 160f;

        var headerColor = isActive
            ? new Vector4(0.2f, 0.4f, 0.2f, 1f)
            : new Vector4(0f, 0f, 0f, 0f);
        var hoverColor = isActive
            ? new Vector4(0.25f, 0.5f, 0.25f, 1f)
            : new Vector4(0.3f, 0.3f, 0.3f, 1f);

        using (ImRaii.PushColor(ImGuiCol.Header, headerColor))
        using (ImRaii.PushColor(ImGuiCol.HeaderHovered, hoverColor))
        {
            var displayLabel = isActive ? $"> {label}" : $"   {label}";
            return ImGui.Selectable(displayLabel, false, ImGuiSelectableFlags.None, new Vector2(width, 0));
        }
    }

    public new void Toggle()
    {
        if (IsOpen)
        {
            IsOpen = false;
        }
        else
        {
            var mousePos = ImGui.GetMousePos();
            var displaySize = ImGui.GetIO().DisplaySize;
            var windowWidth = 170f;
            var estimatedHeight = 200f;

            var posX = mousePos.X - windowWidth / 2;
            var posY = mousePos.Y + 5;

            if (posX + windowWidth > displaySize.X - 5)
                posX = displaySize.X - windowWidth - 5;
            if (posX < 5)
                posX = 5;

            if (posY + estimatedHeight > displaySize.Y - 5)
                posY = mousePos.Y - estimatedHeight - 5;

            Position = new Vector2(posX, posY);
            PositionCondition = ImGuiCond.Always;
            justOpened = true;
            IsOpen = true;
        }
    }
}
