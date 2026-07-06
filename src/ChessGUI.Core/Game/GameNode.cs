using ChessGUI.Core.Analysis;
using ChessGUI.Core.Moves;

namespace ChessGUI.Core.Game;

/// <summary>
/// Oyun ağacındaki tek bir düğüm. Kök düğüm başlangıç pozisyonunu temsil eder (hamlesiz).
/// Diğer her düğüm, ebeveyninin pozisyonunda oynanan bir <see cref="Move"/> taşır.
/// <see cref="Children"/>[0] ana devam; sonraki çocuklar varyantlardır.
/// </summary>
public sealed class GameNode
{
    public Move Move { get; init; }
    public string San { get; set; } = "";
    public bool IsRoot { get; init; }
    public GameNode? Parent { get; init; }
    public List<GameNode> Children { get; } = new();

    /// <summary>Hamleden sonra gelen yorum (PGN'de {...}).</summary>
    public string? Comment { get; set; }
    /// <summary>Sayısal Açıklama Glifleri (NAG), örn. 1 = "!", 2 = "?".</summary>
    public List<int> Nags { get; } = new();

    /// <summary>Bu hamlenin tam hamle numarası (ör. 15).</summary>
    public int MoveNumber { get; set; }
    /// <summary>Hamleyi beyaz mı oynadı?</summary>
    public bool WhiteMoved { get; set; }
    /// <summary>Kökten itibaren yarım hamle sayısı (kök = 0).</summary>
    public int Ply { get; set; }

    // --- Oyun raporu analizi (opsiyonel; rapor çalıştırılınca doldurulur) ---

    /// <summary>Bu düğümdeki pozisyonun beyaz bakışlı değerlendirmesi (santipiyon; mat ±100000).</summary>
    public int? EvalCp { get; set; }
    /// <summary>Bu düğümde mata kalan hamle (beyaz bakışı; varsa).</summary>
    public int? EvalMate { get; set; }
    /// <summary>Bu hamlenin kalite sınıfı (rapor sonrası).</summary>
    public MoveClassification? Quality { get; set; }

    public bool HasChildren => Children.Count > 0;
    public bool IsMainLine => IsRoot || (Parent != null && Parent.Children.Count > 0 && Parent.Children[0] == this);

    /// <summary>Ana devam düğümü (varsa).</summary>
    public GameNode? MainChild => Children.Count > 0 ? Children[0] : null;
}
