namespace ChessGUI.App.ViewModels;

/// <summary>Yaygın NAG (Sayısal Açıklama Glifi) kodlarını sembollere çevirir.</summary>
public static class NagGlyphs
{
    public static string ToSymbol(int nag) => nag switch
    {
        1 => "!",
        2 => "?",
        3 => "!!",
        4 => "??",
        5 => "!?",
        6 => "?!",
        _ => ""
    };

    public static string ForNags(IEnumerable<int> nags)
    {
        string s = "";
        foreach (int n in nags) s += ToSymbol(n);
        return s;
    }
}
