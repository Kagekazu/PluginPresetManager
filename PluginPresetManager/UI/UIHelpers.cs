using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace PluginPresetManager.UI;

public static class UIHelpers
{
    public static void SectionHeader(string text, FontAwesomeIcon icon)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.Header))
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.Text(icon.ToIconString());
            }
            ImGui.SameLine();
            ImGui.Text(text);
        }
        ImGui.Separator();
        ImGui.Spacing();
    }

    public static void SectionHeader(string text)
    {
        ImGui.TextColored(Colors.Header, text);
        ImGui.Separator();
        ImGui.Spacing();
    }

    public static bool IconButton(FontAwesomeIcon icon, string id, string? tooltip = null, float width = 0)
    {
        bool result;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            result = width > 0
                ? ImGui.Button($"{icon.ToIconString()}##{id}", new Vector2(width, 0))
                : ImGui.Button($"{icon.ToIconString()}##{id}");
        }

        if (tooltip != null && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }

        return result;
    }

    public static bool IconTextButton(FontAwesomeIcon icon, string text, float width = 0)
    {
        string iconStr;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            iconStr = icon.ToIconString();
        }

        var label = $"{iconStr}  {text}";
        return width > 0
            ? ImGui.Button(label, new Vector2(width, 0))
            : ImGui.Button(label);
    }

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

    public static void CenteredIcon(FontAwesomeIcon icon, Vector4? color = null)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var iconStr = icon.ToIconString();
            var iconWidth = ImGui.CalcTextSize(iconStr).X;
            var availWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (availWidth - iconWidth) * 0.5f);

            if (color.HasValue)
                ImGui.TextColored(color.Value, iconStr);
            else
                ImGui.Text(iconStr);
        }
    }

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

    public static void StatusDot(bool active)
    {
        var color = active ? Colors.Active : Colors.Inactive;
        ImGui.TextColored(color, active ? "●" : "○");
    }

    public static void Badge(int count, Vector4? color = null)
    {
        var badgeColor = color ?? Colors.TextMuted;
        ImGui.SameLine();
        ImGui.TextColored(badgeColor, $"({count})");
    }

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

    public static void EndTooltip()
    {
        ImGui.EndTooltip();
    }

    public static void VerticalSpacing(float amount = Sizing.SpacingMedium)
    {
        ImGui.Dummy(new Vector2(0, amount));
    }

    /// <summary>
    /// Returns true if confirmed, false if cancelled, null if still open.
    /// </summary>
    public static bool? ConfirmationModal(string id, string title, string message, string confirmText = "Delete", string cancelText = "Cancel")
    {
        bool? result = null;

        ImGui.SetNextWindowSize(new Vector2(300, 0));
        using (var popup = ImRaii.PopupModal($"{title}##{id}", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            if (popup)
            {
                ImGui.TextWrapped(message);
                VerticalSpacing(Sizing.SpacingLarge);

                var buttonWidth = 80f;
                var spacing = 10f;
                var totalWidth = buttonWidth * 2 + spacing;
                var startX = (ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f;

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + startX);

                using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1f)))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1f)))
                {
                    if (ImGui.Button(confirmText, new Vector2(buttonWidth, 0)))
                    {
                        result = true;
                        ImGui.CloseCurrentPopup();
                    }
                }

                ImGui.SameLine(0, spacing);

                if (ImGui.Button(cancelText, new Vector2(buttonWidth, 0)))
                {
                    result = false;
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        return result;
    }

    public static void OpenConfirmationModal(string id, string title)
    {
        ImGui.OpenPopup($"{title}##{id}");
    }
}
