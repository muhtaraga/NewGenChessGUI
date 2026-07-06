using System.Diagnostics;
using Microsoft.Data.Sqlite;
using ChessGUI.Core.Board;
using ChessGUI.Core.Game;
using ChessGUI.Core.Notation;
using ChessGUI.Openings;

namespace ChessGUI.Data.Import;

/// <summary>
/// Çok oyunlu PGN akışını SQLite veritabanına toplu olarak aktarır. Akış tabanlı (düşük bellek),
/// hazırlanmış (prepared) komutlar, oyuncu/etkinlik adı önbelleği ve toplu transaction kullanır.
/// İsteğe bağlı olarak pozisyon araması için her oyunun ana hattını Zobrist ile indeksler.
/// PGN'de ECO etiketi eksikse kendi <see cref="EcoClassifier"/>'ımızla otomatik doldurur.
/// </summary>
public sealed class PgnImporter
{
    private readonly ChessDatabase _db;
    private readonly Dictionary<string, long> _players = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _events = new(StringComparer.Ordinal);
    private readonly EcoClassifier _eco = EcoClassifier.LoadDefault();

    public PgnImporter(ChessDatabase db) => _db = db;

    /// <summary>Bir PGN akışını içe aktarır. Uzun sürer — arka planda çağırın.</summary>
    public ImportResult Import(TextReader reader, ImportOptions options,
        IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        int imported = 0, indexed = 0, skipped = 0;

        SqliteConnection conn = _db.Connection;
        _db.BeginBulkLoad();

        using SqliteCommand nameCmd = CreateNameCommand(conn);
        using SqliteCommand gameCmd = CreateGameCommand(conn);
        using SqliteCommand posCmd = CreatePositionCommand(conn);

        SqliteTransaction tx = conn.BeginTransaction();
        Bind(nameCmd, gameCmd, posCmd, tx);

        try
        {
            foreach (string raw in PgnSplitter.Split(reader))
            {
                ct.ThrowIfCancellationRequested();

                if (!TryImportGame(raw, options, nameCmd, gameCmd, posCmd, ref indexed))
                {
                    skipped++;
                    continue;
                }

                imported++;
                if (imported % options.BatchSize == 0)
                {
                    tx.Commit();
                    tx.Dispose();
                    progress?.Report(new ImportProgress(imported, indexed, skipped));
                    tx = conn.BeginTransaction();
                    Bind(nameCmd, gameCmd, posCmd, tx);
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
        finally
        {
            tx.Dispose();
            _db.EndBulkLoad();
        }

        progress?.Report(new ImportProgress(imported, indexed, skipped));
        return new ImportResult(imported, indexed, skipped, sw.Elapsed);
    }

    private bool TryImportGame(string raw, ImportOptions options,
        SqliteCommand nameCmd, SqliteCommand gameCmd, SqliteCommand posCmd, ref int indexed)
    {
        GameTree tree;
        try { tree = Pgn.Parse(raw); }
        catch { return false; }

        // En az bir hamlesi olan oyunları al.
        if (tree.Root.MainChild is null) return false;

        string white = Tag(tree, "White", "?");
        string black = Tag(tree, "Black", "?");
        string eventName = Tag(tree, "Event", "");

        long whiteId = GetOrAddName(nameCmd, "Players", _players, white);
        long blackId = GetOrAddName(nameCmd, "Players", _players, black);
        long? eventId = eventName.Length > 0 ? GetOrAddName(nameCmd, "Events", _events, eventName) : null;

        int plyCount = CountMainLinePly(tree);
        string? startFen = tree.StartFen != Position.StartFen ? tree.StartFen : null;

        string eco = Tag(tree, "ECO", "");
        if (eco.Length == 0 && startFen is null) // gömülü tablo standart başlangıcı varsayar
            eco = _eco.Classify(tree)?.Eco ?? "";

        gameCmd.Parameters["@white"].Value = whiteId;
        gameCmd.Parameters["@black"].Value = blackId;
        gameCmd.Parameters["@event"].Value = (object?)eventId ?? DBNull.Value;
        gameCmd.Parameters["@result"].Value = Tag(tree, "Result", "*");
        gameCmd.Parameters["@date"].Value = NormalizeDate(Tag(tree, "Date", ""));
        gameCmd.Parameters["@round"].Value = Tag(tree, "Round", "");
        gameCmd.Parameters["@eco"].Value = eco;
        gameCmd.Parameters["@welo"].Value = ParseElo(Tag(tree, "WhiteElo", ""));
        gameCmd.Parameters["@belo"].Value = ParseElo(Tag(tree, "BlackElo", ""));
        gameCmd.Parameters["@ply"].Value = plyCount;
        gameCmd.Parameters["@fen"].Value = (object?)startFen ?? DBNull.Value;
        gameCmd.Parameters["@pgn"].Value = raw.Trim();

        long gameId = Convert.ToInt64(gameCmd.ExecuteScalar());

        if (options.IndexPositions)
            indexed += IndexPositions(tree, gameId, options.MaxIndexPly, posCmd);

        return true;
    }

    /// <summary>
    /// Her ana-hat pozisyonunu, o pozisyondan oynanan bir sonraki hamleyle (<c>NextMove</c>, UCI)
    /// birlikte indeksler. Bu sütun hem pozisyon aramasında hem de açılış kitabı istatistiğinde
    /// ("bu pozisyondan sonra en çok oynanan hamleler") kullanılır.
    /// </summary>
    private static int IndexPositions(GameTree tree, long gameId, int maxPly, SqliteCommand posCmd)
    {
        Position pos = tree.CreateStartPosition();
        int count = 0;

        void Record(int ply, GameNode? nextNode)
        {
            posCmd.Parameters["@hash"].Value = unchecked((long)pos.ZobristKey);
            posCmd.Parameters["@game"].Value = gameId;
            posCmd.Parameters["@ply"].Value = ply;
            posCmd.Parameters["@next"].Value = (object?)nextNode?.Move.ToUci() ?? DBNull.Value;
            posCmd.ExecuteNonQuery();
            count++;
        }

        GameNode root = tree.Root;
        Record(0, root.MainChild); // başlangıç pozisyonu
        int p = 0;
        for (GameNode? n = root.MainChild; n != null; n = n.MainChild)
        {
            pos.MakeMove(n.Move);
            p++;
            Record(p, n.MainChild);
            if (maxPly > 0 && p >= maxPly) break;
        }
        return count;
    }

    // --- Ad tablosu (Players/Events) yardımcıları ---------------------------

    private long GetOrAddName(SqliteCommand cmd, string table, Dictionary<string, long> cache, string name)
    {
        if (cache.TryGetValue(name, out long id)) return id;

        cmd.CommandText =
            $"INSERT INTO {table}(Name) VALUES(@n) ON CONFLICT(Name) DO UPDATE SET Name=excluded.Name RETURNING Id;";
        cmd.Parameters["@n"].Value = name;
        id = Convert.ToInt64(cmd.ExecuteScalar());
        cache[name] = id;
        return id;
    }

    private static SqliteCommand CreateNameCommand(SqliteConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.Parameters.Add("@n", SqliteType.Text);
        return cmd;
    }

    private static SqliteCommand CreateGameCommand(SqliteConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Games
                (WhiteId, BlackId, EventId, Result, Date, Round, Eco, WhiteElo, BlackElo, PlyCount, StartFen, Pgn)
            VALUES
                (@white, @black, @event, @result, @date, @round, @eco, @welo, @belo, @ply, @fen, @pgn)
            RETURNING Id;
            """;
        foreach (string p in new[] { "@white", "@black", "@event", "@result", "@date", "@round",
                                     "@eco", "@welo", "@belo", "@ply", "@fen", "@pgn" })
            cmd.Parameters.Add(p, SqliteType.Text);
        cmd.Prepare();
        return cmd;
    }

    private static SqliteCommand CreatePositionCommand(SqliteConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO PositionIndex(Hash, GameId, Ply, NextMove) VALUES(@hash, @game, @ply, @next);";
        cmd.Parameters.Add("@hash", SqliteType.Integer);
        cmd.Parameters.Add("@game", SqliteType.Integer);
        cmd.Parameters.Add("@ply", SqliteType.Integer);
        cmd.Parameters.Add("@next", SqliteType.Text);
        cmd.Prepare();
        return cmd;
    }

    private static void Bind(SqliteCommand a, SqliteCommand b, SqliteCommand c, SqliteTransaction tx)
    {
        a.Transaction = tx;
        b.Transaction = tx;
        c.Transaction = tx;
    }

    // --- Etiket okuma / normalize ------------------------------------------

    private static string Tag(GameTree tree, string key, string fallback) =>
        tree.Tags.TryGetValue(key, out string? v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : fallback;

    private static int CountMainLinePly(GameTree tree)
    {
        int n = 0;
        for (GameNode? node = tree.Root.MainChild; node != null; node = node.MainChild) n++;
        return n;
    }

    private static object ParseElo(string s) =>
        int.TryParse(s, out int elo) && elo > 0 ? elo : DBNull.Value;

    /// <summary>PGN "YYYY.MM.DD" → "YYYY-MM-DD" (bilinmeyen alanlar 0). Sıralanabilir hale getirir.</summary>
    private static string NormalizeDate(string pgnDate)
    {
        if (string.IsNullOrWhiteSpace(pgnDate)) return "";
        string[] parts = pgnDate.Split('.');
        string y = parts.Length > 0 ? parts[0].Replace('?', '0') : "0000";
        string m = parts.Length > 1 ? parts[1].Replace('?', '0') : "00";
        string d = parts.Length > 2 ? parts[2].Replace('?', '0') : "00";
        return $"{y}-{m}-{d}";
    }
}
