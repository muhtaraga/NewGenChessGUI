using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChessGUI.App.Converters;

/// <summary>Boş/null dizeyi <see cref="Visibility.Collapsed"/>e, doluyu <see cref="Visibility.Visible"/>e çevirir.</summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
