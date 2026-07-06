namespace ChessGUI.App.Models;

/// <summary>
/// Kullanıcı ayarları: tahta renkleri/tema, taş stili/boyutu, ses, animasyon,
/// davranış (oto-çevir) ve Oyna/Analiz tahta senkronu.
/// Makul varsayılanlarla; <c>%AppData%\ChessGUI\settings.json</c> içinde saklanır.
/// </summary>
public sealed class AppSettings
{
    // --- Tahta renkleri (hex) -----------------------------------------------
    public string LightSquareColor { get; set; } = "#EEEED2";
    public string DarkSquareColor { get; set; } = "#769656";
    public string AccentColor { get; set; } = "#4A9EDA";

    // --- Tema ----------------------------------------------------------------
    public string Theme { get; set; } = "Dark"; // "Dark" | "Light" (Light stretch/opsiyonel)

    // --- Taş / tahta görünümü ------------------------------------------------
    public double PieceScale { get; set; } = 1.0;
    public bool ShowCoordinates { get; set; } = true;

    // --- Ses -------------------------------------------------------------
    public bool SoundEnabled { get; set; } = true;

    // --- Animasyon -------------------------------------------------------
    public bool AnimationEnabled { get; set; } = true;
    public int AnimationDurationMs { get; set; } = 180;

    // --- Oyun davranışı ----------------------------------------------------
    public bool AutoFlipBoard { get; set; } = true;

    // --- Oyna/Analiz tahta senkronu -----------------------------------------
    public bool SyncPlayAndAnalysisBoards { get; set; } = false;
}
