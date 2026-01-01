using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using PluginPresetManager.UI;

namespace PluginPresetManager.Windows.Tabs;

public class HelpTab
{
    public void Draw()
    {
        UIHelpers.SectionHeader("Commands", FontAwesomeIcon.Terminal);

        DrawCommand("/ppreset", "Open this window");
        DrawCommand("/ppm", "Toggle this window");
        DrawCommand("/ppm <name>", "Apply preset by name");
        DrawCommand("/ppm alwayson", "Disable all except always-on");

        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);

        UIHelpers.SectionHeader("Tips", FontAwesomeIcon.Lightbulb);

        ImGui.BulletText("Set a default preset to auto-apply on login");
        ImGui.BulletText("Use always-on for essential plugins");
        ImGui.BulletText("Right-click presets in Manage for quick actions");

        UIHelpers.VerticalSpacing(Sizing.SpacingLarge);

        UIHelpers.SectionHeader("Links", FontAwesomeIcon.ExternalLinkAlt);

        if (ImGui.Button("GitHub", new Vector2(Sizing.ButtonMedium, 0)))
        {
            Dalamud.Utility.Util.OpenLink("https://github.com/Brappp/PluginPresetManager");
        }
    }

    private static void DrawCommand(string cmd, string desc)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            ImGui.TextColored(Colors.Primary, FontAwesomeIcon.ChevronRight.ToIconString());
        }
        ImGui.SameLine();
        ImGui.TextColored(Colors.Warning, cmd);
        ImGui.SameLine(140);
        ImGui.TextColored(Colors.TextMuted, desc);
    }
}
