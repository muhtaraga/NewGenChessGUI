using System.Text;
using ChessGUI.Core.Board;
using ChessGUI.Core.Notation;

namespace ChessGUI.App.Services;

/// <summary>Motorun UCI baş varyantını (PV) okunur SAN metnine çevirir, örn. "15... Nf6 16. e5 Nd5".</summary>
public static class PvFormatter
{
    public static string ToSan(string fen, IReadOnlyList<string> uciMoves, int maxPlies = 12)
    {
        Position pos;
        try { pos = Position.FromFen(fen); }
        catch { return ""; }

        var sb = new StringBuilder();
        int count = Math.Min(uciMoves.Count, maxPlies);

        for (int i = 0; i < count; i++)
        {
            var move = UciMove.Parse(pos, uciMoves[i]);
            if (move is null) break;

            bool white = pos.SideToMove == Color.White;
            if (white) sb.Append(pos.FullmoveNumber).Append(". ");
            else if (i == 0) sb.Append(pos.FullmoveNumber).Append("... ");

            sb.Append(San.ToSan(pos, move.Value)).Append(' ');
            pos.MakeMove(move.Value);
        }

        return sb.ToString().TrimEnd();
    }
}
