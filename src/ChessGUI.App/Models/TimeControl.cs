namespace ChessGUI.App.Models;

/// <summary>Süre kontrolü türü.</summary>
public enum TimeControlKind
{
    Unlimited,
    Fischer,      // taban + artırım
    FixedPerMove  // hamle başına sabit süre
}

/// <summary>
/// Bir oyunun süre kontrolü. Presetler: 1+0, 3+2, 5+0, 10+0, 15+10, Sınırsız + Özel.
/// </summary>
public sealed class TimeControl
{
    public TimeControlKind Kind { get; set; } = TimeControlKind.Fischer;
    public int BaseMinutes { get; set; } = 5;
    public int IncrementSeconds { get; set; } = 0;
    /// <summary>Yalnızca <see cref="TimeControlKind.FixedPerMove"/> için: hamle başına milisaniye.</summary>
    public int MoveTimeMs { get; set; } = 5000;

    public long BaseMs => BaseMinutes * 60_000L;
    public long IncrementMs => IncrementSeconds * 1000L;

    public override string ToString() => Kind switch
    {
        TimeControlKind.Unlimited => "Sınırsız",
        TimeControlKind.FixedPerMove => $"Hamle başına {MoveTimeMs / 1000.0:0.#} sn",
        _ => IncrementSeconds > 0 ? $"{BaseMinutes}+{IncrementSeconds}" : $"{BaseMinutes} dakika"
    };

    public static TimeControl Unlimited() => new() { Kind = TimeControlKind.Unlimited };
    public static TimeControl Fischer(int baseMinutes, int incrementSeconds) =>
        new() { Kind = TimeControlKind.Fischer, BaseMinutes = baseMinutes, IncrementSeconds = incrementSeconds };
    public static TimeControl FixedMoveTime(int ms) =>
        new() { Kind = TimeControlKind.FixedPerMove, MoveTimeMs = ms };

    /// <summary>Standart presetler.</summary>
    public static IReadOnlyList<TimeControl> Presets { get; } = new List<TimeControl>
    {
        Fischer(1, 0),
        Fischer(3, 2),
        Fischer(5, 0),
        Fischer(10, 0),
        Fischer(15, 10),
        Unlimited()
    };

    public TimeControl Clone() => new()
    {
        Kind = Kind,
        BaseMinutes = BaseMinutes,
        IncrementSeconds = IncrementSeconds,
        MoveTimeMs = MoveTimeMs
    };
}
