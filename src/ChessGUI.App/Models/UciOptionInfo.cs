using ChessGUI.Engine;

namespace ChessGUI.App.Models;

/// <summary>
/// <see cref="UciOption"/>'ın serileştirilebilir kopyası. Motor bir kez başlatılıp seçenekleri
/// okunduktan sonra <see cref="EngineProfile"/> içinde önbelleğe alınır; böylece ayar arayüzü
/// motoru yeniden başlatmadan çizilebilir.
/// </summary>
public sealed class UciOptionInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // check, spin, combo, button, string
    public string? Default { get; set; }
    public long? Min { get; set; }
    public long? Max { get; set; }
    public List<string> Vars { get; set; } = new();

    public static UciOptionInfo FromOption(UciOption o) => new()
    {
        Name = o.Name,
        Type = o.Type,
        Default = o.Default,
        Min = o.Min,
        Max = o.Max,
        Vars = new List<string>(o.Vars)
    };
}
