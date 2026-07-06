using System.Windows;
using System.Windows.Media;

namespace ChessGUI.App.Services;

/// <summary>
/// Koyu/açık tema renk paletlerini tutar ve <see cref="Apply"/> ile uygular. XAML'de tanımlı fırçalar
/// yükleme sırasında WPF tarafından dondurulduğu (salt okunur) için <c>.Color</c> değiştirilemez;
/// bu yüzden ilgili anahtarın kaynağı uygulama düzeyinde <b>yeni</b> bir <see cref="SolidColorBrush"/>
/// ile değiştirilir. Tema anahtarları XAML'de <c>DynamicResource</c> ile bağlandığından bu değişim
/// tüm tüketicilere anında yansır.
/// </summary>
public sealed class ThemeService
{
    private static readonly Dictionary<string, Color> DarkPalette = new()
    {
        ["WindowBackground"] = Color.FromRgb(0x1E, 0x1E, 0x22),
        ["PanelBackground"] = Color.FromRgb(0x26, 0x26, 0x2B),
        ["PanelBorder"] = Color.FromRgb(0x3A, 0x3A, 0x42),
        ["ForegroundBrush"] = Color.FromRgb(0xE6, 0xE6, 0xE6),
        ["MutedForeground"] = Color.FromRgb(0x9A, 0x9A, 0xA5),
        ["AccentBrush"] = Color.FromRgb(0x4A, 0x9E, 0xDA),
        ["AccentHover"] = Color.FromRgb(0x5C, 0xB0, 0xEC),
        ["ButtonBackground"] = Color.FromRgb(0x32, 0x32, 0x3A),
        ["ButtonHover"] = Color.FromRgb(0x3D, 0x3D, 0x47),
    };

    private static readonly Dictionary<string, Color> LightPalette = new()
    {
        ["WindowBackground"] = Color.FromRgb(0xF4, 0xF4, 0xF6),
        ["PanelBackground"] = Color.FromRgb(0xFF, 0xFF, 0xFF),
        ["PanelBorder"] = Color.FromRgb(0xD0, 0xD0, 0xD8),
        ["ForegroundBrush"] = Color.FromRgb(0x1E, 0x1E, 0x22),
        ["MutedForeground"] = Color.FromRgb(0x6A, 0x6A, 0x75),
        ["AccentBrush"] = Color.FromRgb(0x2A, 0x7D, 0xBE),
        ["AccentHover"] = Color.FromRgb(0x3A, 0x8F, 0xD0),
        ["ButtonBackground"] = Color.FromRgb(0xE8, 0xE8, 0xEC),
        ["ButtonHover"] = Color.FromRgb(0xDA, 0xDA, 0xDF),
    };

    /// <summary>Belirtilen temayı ("Dark" veya "Light") canlı olarak uygular.</summary>
    public void Apply(string theme)
    {
        Dictionary<string, Color> palette = theme == "Light" ? LightPalette : DarkPalette;
        foreach (var (key, color) in palette)
        {
            // Donmuş fırçanın rengi değiştirilemez; kaynağı uygulama düzeyinde yeni (donmamış)
            // bir fırçayla değiştir. DynamicResource tüketicileri bu değişimi anında alır.
            Application.Current.Resources[key] = new SolidColorBrush(color);
        }
    }
}
