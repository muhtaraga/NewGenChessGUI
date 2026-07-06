using CommunityToolkit.Mvvm.ComponentModel;
using ChessGUI.App.Models;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Tek bir UCI seçeneğinin (check/spin/combo/string/button) düzenleyicisi. Motor tipi hangi
/// seçeneği bildiriyorsa XAML tarafında ilgili <c>DataTemplate</c> (DataType bazlı) seçilir.
/// Değer değiştiğinde ilgili <see cref="EngineProfile.OptionValues"/> girdisi güncellenir.
/// </summary>
public sealed partial class OptionEditorViewModel : ObservableObject
{
    public UciOptionInfo Info { get; }
    private readonly EngineProfile _profile;

    public string Name => Info.Name;
    public string Type => Info.Type;
    public bool IsCheck => Type == "check";
    public bool IsSpin => Type == "spin";
    public bool IsCombo => Type == "combo";
    public bool IsString => Type == "string";
    public bool IsButton => Type == "button";

    public long Min => Info.Min ?? 0;
    public long Max => Info.Max ?? 100;
    public IReadOnlyList<string> Vars => Info.Vars;

    [ObservableProperty] private string _stringValue = "";
    [ObservableProperty] private bool _checkValue;
    [ObservableProperty] private long _spinValue;

    public OptionEditorViewModel(UciOptionInfo info, EngineProfile profile)
    {
        Info = info;
        _profile = profile;

        string current = profile.OptionValues.TryGetValue(info.Name, out string? v) ? v : (info.Default ?? "");
        _stringValue = current;
        _checkValue = bool.TryParse(current, out bool b) && b;
        _spinValue = long.TryParse(current, out long l) ? l : (info.Min ?? 0);
    }

    partial void OnStringValueChanged(string value) => Persist(value);
    partial void OnCheckValueChanged(bool value) => Persist(value ? "true" : "false");
    partial void OnSpinValueChanged(long value) => Persist(value.ToString());

    private void Persist(string value) => _profile.OptionValues[Info.Name] = value;
}
