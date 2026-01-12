using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PluginPresetManager;

/// <summary>
/// Helper for detecting and rescuing windows that are stuck off-screen.
/// Uses ImGui's internal window list to find and move windows.
/// </summary>
public class WindowRescueHelper
{
    private readonly IPluginLog log;

    private static readonly string[] InternalWindowPatterns =
    {
        "gizmo", "debug", "imguidemo", "metrics", "stack tool", "style editor"
    };

    public WindowRescueHelper(IPluginLog log)
    {
        this.log = log;
    }

    public class WindowInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? PluginName { get; set; }
        public Vector2 Pos { get; set; }
        public Vector2 Size { get; set; }
        public bool IsOffScreen { get; set; }
        public bool IsPartiallyOffScreen { get; set; }
    }

    /// <summary>
    /// Gets all visible plugin windows from ImGui's internal context.
    /// </summary>
    public unsafe List<WindowInfo> GetAllWindows(bool onlyFromLoadedPlugins = false)
    {
        var windows = new List<WindowInfo>();

        try
        {
            var contextPtr = ImGuiNative.GetCurrentContext();
            if (contextPtr == null)
            {
                log.Warning("ImGui context is null");
                return windows;
            }

            var context = new ImGuiContextPtr(contextPtr);
            var windowList = context.Windows;

            var viewport = ImGui.GetMainViewport();
            var screenPos = viewport.Pos;
            var screenSize = viewport.Size;
            var screenRight = screenPos.X + screenSize.X;
            var screenBottom = screenPos.Y + screenSize.Y;

            var loadedPlugins = Plugin.PluginInterface.InstalledPlugins
                .Where(p => p.IsLoaded)
                .ToList();

            for (var i = 0; i < windowList.Size; i++)
            {
                var window = windowList[i];
                if (window.Handle == null || !window.Active || window.Hidden)
                    continue;

                var namePtr = window.Name;
                if (namePtr == null)
                    continue;

                var name = Marshal.PtrToStringUTF8((nint)namePtr);
                if (string.IsNullOrWhiteSpace(name) || name.StartsWith("#"))
                    continue;

                var (displayPart, idPart) = ParseWindowName(name);
                if (string.IsNullOrWhiteSpace(displayPart) || idPart.StartsWith("#"))
                    continue;

                if (IsInternalWindow(name))
                    continue;

                var matchedPlugin = FindMatchingPlugin(name, displayPart, idPart, loadedPlugins);
                if (onlyFromLoadedPlugins && matchedPlugin == null)
                    continue;

                var pos = window.Pos;
                var size = window.Size;

                windows.Add(new WindowInfo
                {
                    Name = name,
                    DisplayName = displayPart,
                    PluginName = matchedPlugin,
                    Pos = pos,
                    Size = size,
                    IsOffScreen = IsCompletelyOffScreen(pos, size, screenPos, screenRight, screenBottom),
                    IsPartiallyOffScreen = IsPartiallyOffScreen(pos, size, screenPos, screenRight, screenBottom)
                });
            }

            log.Debug($"Found {windows.Count} windows from ImGui context");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to get windows from ImGui context");
        }

        return windows;
    }

    /// <summary>
    /// Moves a window to the center of the screen.
    /// </summary>
    public bool MoveWindowToCenter(string windowName)
    {
        try
        {
            var mainViewport = ImGui.GetMainViewport();
            var screenCenter = mainViewport.Pos + (mainViewport.Size / 2);
            var targetPos = new Vector2(screenCenter.X - 200, screenCenter.Y - 150);

            ImGui.SetWindowPos(windowName, targetPos, ImGuiCond.Always);
            log.Info($"Moved window to center: {windowName} -> ({targetPos.X}, {targetPos.Y})");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to move window: {windowName}");
            return false;
        }
    }

    private static (string displayPart, string idPart) ParseWindowName(string name)
    {
        // ImGui uses ### or ## to separate display name from ID
        var hashIndex = name.IndexOf("###", StringComparison.Ordinal);
        var hashLen = 3;

        if (hashIndex < 0)
        {
            hashIndex = name.IndexOf("##", StringComparison.Ordinal);
            hashLen = 2;
        }

        if (hashIndex > 0)
            return (name.Substring(0, hashIndex), name.Substring(hashIndex + hashLen));

        return (name, string.Empty);
    }

    private static bool IsInternalWindow(string name)
    {
        if (name.Contains("(TESTING)", StringComparison.OrdinalIgnoreCase))
            return true;

        var lowerName = name.ToLowerInvariant();
        return InternalWindowPatterns.Any(pattern => lowerName.Contains(pattern));
    }

    private static string? FindMatchingPlugin(
        string fullName,
        string displayPart,
        string idPart,
        List<IExposedPlugin> loadedPlugins)
    {
        foreach (var plugin in loadedPlugins)
        {
            if (displayPart.StartsWith(plugin.Name, StringComparison.OrdinalIgnoreCase) ||
                displayPart.StartsWith(plugin.InternalName, StringComparison.OrdinalIgnoreCase) ||
                fullName.StartsWith(plugin.Name, StringComparison.OrdinalIgnoreCase) ||
                fullName.StartsWith(plugin.InternalName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(idPart) && idPart.StartsWith(plugin.InternalName, StringComparison.OrdinalIgnoreCase)))
            {
                return plugin.Name;
            }
        }
        return null;
    }

    private static bool IsCompletelyOffScreen(Vector2 pos, Vector2 size, Vector2 screenPos, float screenRight, float screenBottom)
    {
        // Window is off-screen if less than 50px is visible
        return pos.X + size.X < screenPos.X + 50 ||
               pos.Y + size.Y < screenPos.Y + 50 ||
               pos.X > screenRight - 50 ||
               pos.Y > screenBottom - 50;
    }

    private static bool IsPartiallyOffScreen(Vector2 pos, Vector2 size, Vector2 screenPos, float screenRight, float screenBottom)
    {
        var isCompletelyOff = IsCompletelyOffScreen(pos, size, screenPos, screenRight, screenBottom);
        if (isCompletelyOff)
            return false;

        return pos.X < screenPos.X ||
               pos.Y < screenPos.Y ||
               pos.X + size.X > screenRight ||
               pos.Y + size.Y > screenBottom;
    }
}
