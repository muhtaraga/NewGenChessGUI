using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChessGUI.App.Converters;

/// <summary>Bir hex renk dizesini (ör. "#769656") <see cref="SolidColorBrush"/>'a çevirir (tahta rengi ayarları için).</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex)!;
                return new SolidColorBrush(color);
            }
            catch
            {
                // Geçersiz hex -> varsayılan.
            }
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
