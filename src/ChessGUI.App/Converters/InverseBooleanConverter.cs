using System.Globalization;
using System.Windows.Data;

namespace ChessGUI.App.Converters;

/// <summary>Bir bool değerini tersine çevirir (ör. "özel süre" işaretliyken preset ComboBox'ı kapatmak için).</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !(value is bool b && b);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !(value is bool b && b);
}
