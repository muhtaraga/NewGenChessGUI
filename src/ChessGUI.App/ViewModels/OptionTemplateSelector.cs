using System.Windows;
using System.Windows.Controls;

namespace ChessGUI.App.ViewModels;

/// <summary>Seçili motorun UCI seçeneğinin tipine (check/spin/combo/string/button) göre <see cref="DataTemplate"/> seçer.</summary>
public sealed class OptionTemplateSelector : DataTemplateSelector
{
    public DataTemplate? CheckTemplate { get; set; }
    public DataTemplate? SpinTemplate { get; set; }
    public DataTemplate? ComboTemplate { get; set; }
    public DataTemplate? StringTemplate { get; set; }
    public DataTemplate? ButtonTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not OptionEditorViewModel opt) return base.SelectTemplate(item, container);
        return opt.Type switch
        {
            "check" => CheckTemplate,
            "spin" => SpinTemplate,
            "combo" => ComboTemplate,
            "button" => ButtonTemplate,
            _ => StringTemplate
        };
    }
}
