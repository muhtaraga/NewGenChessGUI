using System;
using System.IO;
using Microsoft.Data.Sqlite;
using ChessGUI.Data.Import;
using ChessGUI.Data.Repositories;

namespace ChessGUI.Data.Tests;

/// <summary>MovesHash sütunu eklenmeden önce oluşturulmuş veritabanlarının geriye dönük yükseltilmesini doğrular.</summary>
public class MigrationTests
{
    private const string LegacyGamePgn = """
        [White "Old"]
        [Black "Timer"]
        [Result "1-0"]

        1. e4 e5 2. Nf3 Nc6 1-0
        """;

    [Fact]
    public void OpeningLegacyDatabase_BackfillsMovesHash_SoDuplicatesAreCaught()
    {
        string path = Path.Combine(Path.GetTempPath(), $"chessgui-legacy-{Guid.NewGuid():N}.db");
        try
        {
            CreateLegacyDatabase(path);

            using var db = ChessDatabase.Open(path); // MovesHash sütunu burada eklenip geriye dönük doldurulmalı
            var repo = new GameRepository(db);
            Assert.Equal(1, repo.GameCount());

            var result = new PgnImporter(db).Import(new StringReader(LegacyGamePgn), new ImportOptions());

            Assert.Equal(0, result.GamesImported);
            Assert.Equal(1, result.Duplicates); // eski kayıt geriye dönük hashlendiği için yakalandı
            Assert.Equal(1, repo.GameCount());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            // WAL modunda dosya tanıtıcıları hemen serbest kalmayabilir; temizlik en iyi çaba ile yapılır.
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
            try { File.Delete(path + "-wal"); } catch (IOException) { }
            try { File.Delete(path + "-shm"); } catch (IOException) { }
        }
    }

    private static void CreateLegacyDatabase(string path)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate };
        using var conn = new SqliteConnection(builder.ConnectionString);
        conn.Open();

        using (var ddl = conn.CreateCommand())
        {
            ddl.CommandText = """
                CREATE TABLE Players (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE);
                CREATE TABLE Events (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE);
                CREATE TABLE Games (
                    Id       INTEGER PRIMARY KEY,
                    WhiteId  INTEGER NOT NULL REFERENCES Players(Id),
                    BlackId  INTEGER NOT NULL REFERENCES Players(Id),
                    EventId  INTEGER REFERENCES Events(Id),
                    Result   TEXT,
                    Date     TEXT,
                    Round    TEXT,
                    Eco      TEXT,
                    WhiteElo INTEGER,
                    BlackElo INTEGER,
                    PlyCount INTEGER NOT NULL DEFAULT 0,
                    StartFen TEXT,
                    Pgn      TEXT NOT NULL
                );
                CREATE TABLE PositionIndex (
                    Hash   INTEGER NOT NULL,
                    GameId INTEGER NOT NULL REFERENCES Games(Id),
                    Ply    INTEGER NOT NULL
                );
                INSERT INTO Players(Id, Name) VALUES (1, 'Old'), (2, 'Timer');
                """;
            ddl.ExecuteNonQuery();
        }

        using (var insertGame = conn.CreateCommand())
        {
            insertGame.CommandText =
                "INSERT INTO Games(Id, WhiteId, BlackId, Result, PlyCount, Pgn) VALUES (1, 1, 2, '1-0', 4, @pgn);";
            insertGame.Parameters.AddWithValue("@pgn", LegacyGamePgn);
            insertGame.ExecuteNonQuery();
        }
    }
}
