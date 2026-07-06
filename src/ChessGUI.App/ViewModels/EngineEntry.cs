namespace ChessGUI.App.ViewModels;

/// <summary>Kayıtlı bir motor (görünen ad + çalıştırılabilir yol). ComboBox'ta listelenir.</summary>
public sealed record EngineEntry(string Name, string Path)
{
    public override string ToString() => Name;
}
