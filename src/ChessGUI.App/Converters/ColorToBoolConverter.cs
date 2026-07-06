using System.Globalization;
using System.Windows.Data;
using ChessGUI.Core.Board;

namespace ChessGUI.App.Converters;

/// <summary>
/// Bir <see cref="Color"/> değerini, verilen parametre ("White"/"Black") ile eşleşip
/// eşleşmediğine göre bool'a çevirir. Pozisyon editöründeki "Sıra" radyo düğmeleri için kullanılır.
/// </summary>
public sealed class ColorToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Color color || parameter is not string expected) return false;
        return string.Equals(expected, color.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string s && Enum.TryParse(s, true, out Color color))
            return color;
        return Binding.DoNothing;
    }
}
