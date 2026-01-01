using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace PluginPresetManager.UI;

/// <summary>
/// Helper methods for consistent UI rendering.
/// </summary>
public static class UIHelpers
{
    /// <summary>
    /// Draws a section header with icon.
    /// </summary>
    public static void SectionHeader(string text, FontAwesomeIcon icon)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Colors.Header);
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(icon.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.Text(text);
        ImGui.PopStyleColor();
        ImGui.Separator();
        ImGui.Spacing();
    }

    /// <summary>
    /// Draws a section header without icon.
    /// </summary>
    public static void SectionHeader(string text)
    {
        ImGui.TextColored(Colors.Header, text);
        ImGui.Separator();
        ImGui.Spacing();
    }

    /// <summary>
    /// Draws an icon button with consistent sizing.
    /// </summary>
    public static bool IconButton(FontAwesomeIcon icon, string id, string? tooltip = null, float width = 0)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var result = width > 0
            ? ImGui.Button($"{icon.ToIconString()}##{id}", new Vector2(width, 0))
            : ImGui.Button($"{icon.ToIconString()}##{id}");
        ImGui.PopFont();

        if (tooltip != null && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }

        return result;
    }

    /// <summary>
    /// Draws a button with icon and text.
    /// </summary>
    public static bool IconTextButton(FontAwesomeIcon icon, string text, float width = 0)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var iconStr = icon.ToIconString();
        ImGui.PopFont();

        var label = $"{iconStr}  {text}";
        return width > 0
            ? ImGui.Button(label, new Vector2(width, 0))
            : ImGui.Button(label);
    }

    /// <summary>
    /// Draws centered text.
    /// </summary>
    public static void CenteredText(string text, Vector4? color = null)
    {
        var textWidth = ImGui.CalcTextSize(text).X;
        var availWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (availWidth - textWidth) * 0.5f);

        if (color.HasValue)
            ImGui.TextColored(color.Value, text);
        else
            ImGui.Text(text);
    }

    /// <summary>
    /// Draws a centered icon.
    /// </summary>
    public static void CenteredIcon(FontAwesomeIcon icon, Vector4? color = null)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var iconStr = icon.ToIconString();
        var iconWidth = ImGui.CalcTextSize(iconStr).X;
        var availWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (availWidth - iconWidth) * 0.5f);

        if (color.HasValue)
            ImGui.TextColored(color.Value, iconStr);
        else
            ImGui.Text(iconStr);

        ImGui.PopFont();
    }

    /// <summary>
    /// Draws an empty state with icon and message.
    /// </summary>
    public static bool EmptyState(FontAwesomeIcon icon, string message, string? buttonText = null)
    {
        var result = false;
        var availHeight = ImGui.GetContentRegionAvail().Y;

        ImGui.Dummy(new Vector2(0, availHeight * 0.2f));

        CenteredIcon(icon, Colors.TextMuted);
        ImGui.Spacing();
        CenteredText(message, Colors.TextMuted);

        if (buttonText != null)
        {
            ImGui.Spacing();
            ImGui.Spacing();

            var buttonWidth = ImGui.CalcTextSize(buttonText).X + 20;
            var availWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (availWidth - buttonWidth) * 0.5f);

            if (ImGui.Button(buttonText))
            {
                result = true;
            }
        }

        return result;
    }

    /// <summary>
    /// Draws a status indicator dot.
    /// </summary>
    public static void StatusDot(bool active)
    {
        var color = active ? Colors.Active : Colors.Inactive;
        ImGui.TextColored(color, active ? "●" : "○");
    }

    /// <summary>
    /// Draws a small badge with count.
    /// </summary>
    public static void Badge(int count, Vector4? color = null)
    {
        var badgeColor = color ?? Colors.TextMuted;
        ImGui.SameLine();
        ImGui.TextColored(badgeColor, $"({count})");
    }

    /// <summary>
    /// Begins a tooltip with consistent styling.
    /// </summary>
    public static void BeginTooltip(string? header = null)
    {
        ImGui.BeginTooltip();
        if (header != null)
        {
            ImGui.TextColored(Colors.Header, header);
            ImGui.Separator();
            ImGui.Spacing();
        }
    }

    /// <summary>
    /// Ends a tooltip.
    /// </summary>
    public static void EndTooltip()
    {
        ImGui.EndTooltip();
    }

    /// <summary>
    /// Adds vertical spacing.
    /// </summary>
    public static void VerticalSpacing(float amount = Sizing.SpacingMedium)
    {
        ImGui.Dummy(new Vector2(0, amount));
    }
}
