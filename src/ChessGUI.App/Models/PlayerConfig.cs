namespace ChessGUI.App.Models;

public enum PlayerKind
{
    Human,
    Engine
}

/// <summary>Bir oyuncunun (beyaz veya siyah) türü ve — bilgisayarsa — motor profili.</summary>
public sealed class PlayerConfig
{
    public PlayerKind Kind { get; set; } = PlayerKind.Human;
    public EngineProfile? Engine { get; set; }
    public string DisplayName => Kind == PlayerKind.Human ? "İnsan" : (Engine?.Name ?? "Motor");

    public static PlayerConfig Human() => new() { Kind = PlayerKind.Human };
    public static PlayerConfig ForEngine(EngineProfile profile) => new() { Kind = PlayerKind.Engine, Engine = profile };
}
