using ChessGUI.Core.Board;

namespace ChessGUI.Core.Moves;

/// <summary>
/// Perft (performance test): verilen derinlikteki yaprak düğüm sayısını sayar.
/// Hamle üretimi + make/unmake doğruluğunun altın standardı; bilinen referans
/// sayılarla karşılaştırılarak çekirdeğin hatasız olduğu kanıtlanır.
/// </summary>
public static class Perft
{
    public static long Count(Position pos, int depth)
    {
        if (depth == 0) return 1;

        var moves = MoveGenerator.GeneratePseudoLegal(pos);
        Color us = pos.SideToMove;
        long nodes = 0;

        foreach (Move m in moves)
        {
            var undo = pos.MakeMove(m);
            if (!pos.IsInCheck(us))
                nodes += depth == 1 ? 1 : Count(pos, depth - 1);
            pos.UnmakeMove(m, undo);
        }
        return nodes;
    }

    /// <summary>Kök hamle bazında düğüm dağılımı (divide) — hata ayıklama için.</summary>
    public static IReadOnlyList<(Move Move, long Nodes)> Divide(Position pos, int depth)
    {
        var result = new List<(Move, long)>();
        Color us = pos.SideToMove;
        foreach (Move m in MoveGenerator.GeneratePseudoLegal(pos))
        {
            var undo = pos.MakeMove(m);
            if (!pos.IsInCheck(us))
                result.Add((m, depth <= 1 ? 1 : Count(pos, depth - 1)));
            pos.UnmakeMove(m, undo);
        }
        return result;
    }
}
