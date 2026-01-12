using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using PluginPresetManager.UI;

namespace PluginPresetManager.Windows.Tabs;

/// <summary>
/// Tab for rescuing windows that are stuck off-screen.
/// </summary>
public class WindowRescueTab
{
    private readonly WindowRescueHelper rescueHelper;
    private List<WindowRescueHelper.WindowInfo>? cachedWindows;
    private string searchFilter = string.Empty;

    public WindowRescueTab(WindowRescueHelper rescueHelper)
    {
        this.rescueHelper = rescueHelper;
    }

    public void Draw()
    {
        UIHelpers.SectionHeader("Unstick Windows", FontAwesomeIcon.WindowRestore);
        ImGui.TextColored(Colors.TextMuted, "Move stuck off-screen windows back to center.");
        ImGui.TextColored(Colors.TextMuted, "Note: Best effort - may not work for all plugin windows.");

        ImGui.Spacing();
        DrawToolbar();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawWindowList();
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("Refresh"))
            cachedWindows = null;

        ImGui.SameLine();

        var offScreenCount = cachedWindows?.Count(w => w.IsOffScreen || w.IsPartiallyOffScreen) ?? 0;
        using (ImRaii.PushColor(ImGuiCol.Button, Colors.Warning, offScreenCount > 0))
        {
            if (ImGui.Button($"Rescue Off-Screen ({offScreenCount})"))
            {
                if (cachedWindows != null)
                {
                    foreach (var w in cachedWindows.Where(w => w.IsOffScreen || w.IsPartiallyOffScreen))
                        rescueHelper.MoveWindowToCenter(w.Name);
                }
                cachedWindows = null;
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##Search", "Search...", ref searchFilter, 100);
    }

    private void DrawWindowList()
    {
        cachedWindows ??= rescueHelper.GetAllWindows();

        if (cachedWindows.Count == 0)
        {
            ImGui.TextColored(Colors.TextMuted, "No windows found.");
            ImGui.TextColored(Colors.TextMuted, "Open some plugin windows first, then click Refresh.");
            return;
        }

        var offScreenCount = cachedWindows.Count(w => w.IsOffScreen || w.IsPartiallyOffScreen);
        var totalCount = cachedWindows.Count;

        var viewport = ImGui.GetMainViewport();
        ImGui.TextColored(Colors.TextMuted, $"Screen: {viewport.Size.X:F0}x{viewport.Size.Y:F0}");

        if (offScreenCount > 0)
        {
            ImGui.TextColored(Colors.Warning, $"{offScreenCount} off-screen");
            ImGui.SameLine();
            ImGui.TextColored(Colors.TextMuted, $"/ {totalCount} total");
        }
        else
        {
            ImGui.TextColored(Colors.Success, "All windows on-screen");
            ImGui.SameLine();
            ImGui.TextColored(Colors.TextMuted, $"({totalCount} total)");
        }

        ImGui.Spacing();

        using var child = ImRaii.Child("WindowList", new System.Numerics.Vector2(0, 0), false);
        if (!child) return;

        var filtered = cachedWindows
            .Where(w => MatchesFilter(w.Name))
            .OrderByDescending(w => w.IsOffScreen)
            .ThenByDescending(w => w.IsPartiallyOffScreen)
            .ThenBy(w => w.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (filtered.Count == 0)
        {
            ImGui.TextColored(Colors.TextMuted, "No windows match your search.");
            return;
        }

        foreach (var window in filtered)
        {
            if (ImGui.SmallButton($"Move##{window.Name}"))
            {
                rescueHelper.MoveWindowToCenter(window.Name);
                cachedWindows = null;
            }

            ImGui.SameLine();

            if (window.IsOffScreen || window.IsPartiallyOffScreen)
            {
                ImGui.TextColored(Colors.Warning, window.DisplayName);
                ImGui.SameLine();
                ImGui.TextColored(Colors.Warning, "[OFF-SCREEN]");
            }
            else
            {
                ImGui.Text(window.DisplayName);
            }

            if (ImGui.IsItemHovered())
            {
                var pluginInfo = window.PluginName != null ? $"Plugin: {window.PluginName}\n" : "";
                ImGui.SetTooltip($"{pluginInfo}ID: {window.Name}\nPos: ({window.Pos.X:F0}, {window.Pos.Y:F0})\nSize: ({window.Size.X:F0}, {window.Size.Y:F0})");
            }
        }
    }

    private bool MatchesFilter(string window)
    {
        return string.IsNullOrEmpty(searchFilter) ||
               window.Contains(searchFilter, StringComparison.OrdinalIgnoreCase);
    }
}
