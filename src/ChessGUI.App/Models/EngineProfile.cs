namespace ChessGUI.App.Models;

/// <summary>
/// Kalıcı olarak kaydedilmiş bir UCI motoru: yol + kalıcı seçenek değerleri (Threads, Hash, Ponder…)
/// ve motordan ilk eklemede okunup önbelleğe alınan seçenek tanımları (ad/tip/default/min/max/vars).
/// Önbellek sayesinde ayar arayüzü motoru yeniden başlatmadan çizilebilir.
/// </summary>
public sealed class EngineProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public Dictionary<string, string> OptionValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<UciOptionInfo> Options { get; set; } = new();
}
