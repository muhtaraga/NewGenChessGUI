using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessGUI.Core.Board;
using ChessGUI.Core.Moves;
using ChessGUI.Core.Notation;
using ChessGUI.Data.Entities;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Açılış kitabı panosunun görünüm modeli. Dış bir .bin dosyası kullanmaz — kendi SQLite
/// veritabanımızdan (<see cref="DatabaseViewModel"/>), mevcut pozisyondan sonra en çok oynanan
/// hamleleri oyun sayısı ve sonuç istatistiğiyle gösterir. Tahta veya veritabanı değiştikçe
/// otomatik yenilenir; bir satıra tıklamak o hamleyi tahtada oynatır.
/// </summary>
public sealed partial class BookViewModel : ObservableObject
{
    private readonly BoardViewModel _board;
    private readonly DatabaseViewModel _database;

    public ObservableCollection<BookEntry> Entries { get; } = new();

    [ObservableProperty] private bool _hasEntries;
    [ObservableProperty] private string _statusText = "";

    public BookViewModel(BoardViewModel board, DatabaseViewModel database)
    {
        _board = board;
        _database = database;
        _board.Changed += (_, _) => Refresh();
        _database.RepositoryChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        Entries.Clear();

        var bookRepo = _database.BookRepository;
        if (bookRepo is null)
        {
            HasEntries = false;
            StatusText = "";
            return;
        }

        Position pos = _board.Position;
        List<BookMoveStat> stats = bookRepo.GetMoves(pos.ZobristKey);
        if (stats.Count == 0)
        {
            HasEntries = false;
            StatusText = "Bu pozisyon için kitap verisi yok.";
            return;
        }

        int totalGames = stats.Sum(s => s.Total);
        foreach (BookMoveStat s in stats.OrderByDescending(s => s.Total).Take(10))
        {
            Move? move = UciMove.Parse(pos, s.Uci);
            if (move is null) continue; // veritabanındaki eski/uyumsuz kayıt — sessizce atla
            string san = San.ToSan(pos, move.Value);
            Entries.Add(new BookEntry(san, move.Value, s.Total, totalGames, s.WhiteWins, s.Draws, s.BlackWins));
        }

        HasEntries = Entries.Count > 0;
        StatusText = HasEntries ? $"{totalGames:N0} oyun" : "Bu pozisyon için kitap verisi yok.";
    }

    [RelayCommand]
    private void PlayMove(BookEntry? entry)
    {
        if (entry is null) return;
        Move m = entry.Move;
        _board.TryMove(m.From, m.To, m.IsPromotion ? m.Promotion : PieceType.Queen);
    }
}
