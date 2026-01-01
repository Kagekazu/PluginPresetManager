using System.Numerics;

namespace PluginPresetManager.UI;

/// <summary>
/// Centralized UI constants for consistent styling across the plugin.
/// </summary>
public static class Colors
{
    // Primary colors
    public static readonly Vector4 Primary = new(0.4f, 0.7f, 1f, 1f);
    public static readonly Vector4 PrimaryHover = new(0.5f, 0.8f, 1f, 1f);

    // Status colors
    public static readonly Vector4 Success = new(0.4f, 1f, 0.6f, 1f);
    public static readonly Vector4 Warning = new(1f, 0.8f, 0.4f, 1f);
    public static readonly Vector4 Error = new(1f, 0.4f, 0.4f, 1f);

    // Text colors
    public static readonly Vector4 TextNormal = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 TextMuted = new(0.5f, 0.5f, 0.5f, 1f);
    public static readonly Vector4 TextDisabled = new(0.4f, 0.4f, 0.4f, 1f);

    // Section headers
    public static readonly Vector4 Header = new(0.7f, 0.9f, 1f, 1f);

    // Special
    public static readonly Vector4 Star = new(1f, 0.85f, 0.3f, 1f);
    public static readonly Vector4 Active = Success;
    public static readonly Vector4 Inactive = new(0.6f, 0.6f, 0.6f, 1f);

    // Tags
    public static readonly Vector4 TagDev = new(1f, 0.4f, 1f, 1f);
    public static readonly Vector4 TagThirdParty = new(1f, 1f, 0.4f, 1f);
}

public static class Sizing
{
    // Button widths
    public const float ButtonSmall = 60f;
    public const float ButtonMedium = 80f;
    public const float ButtonLarge = 100f;
    public const float ButtonWide = 120f;

    // Spacing
    public const float SpacingSmall = 4f;
    public const float SpacingMedium = 8f;
    public const float SpacingLarge = 16f;

    // Input widths
    public const float InputSmall = 100f;
    public const float InputMedium = 150f;
    public const float InputLarge = 200f;

    // Panel widths
    public const float LeftPanelWidth = 180f;
}
