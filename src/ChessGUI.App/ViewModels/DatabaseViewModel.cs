using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ChessGUI.Data;
using ChessGUI.Data.Entities;
using ChessGUI.Data.Import;
using ChessGUI.Data.Repositories;
using ChessGUI.App.Services;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Oyun veritabanı panosunun görünüm modeli: SQLite dosyası açma/oluşturma, toplu PGN içe aktarma
/// (arka planda, ilerleme ile), oyuncu/açılış/tarih/sonuç ve mevcut pozisyon (Zobrist) araması,
/// ve seçili oyunu tahtaya yükleme.
/// </summary>
public sealed partial class DatabaseViewModel : ObservableObject, IDisposable
{
    private readonly BoardViewModel _board;
    private readonly SettingsService _settings;
    private ChessDatabase? _db;
    private GameRepository? _repo;
    private BookRepository? _bookRepo;

    public ObservableCollection<GameHeader> Results { get; } = new();

    /// <summary>Açık veritabanının kitap sorgu arayüzü (yoksa <c>null</c>).</summary>
    public BookRepository? BookRepository => _bookRepo;

    /// <summary>Veritabanı açıldığında veya yeni oyunlar aktarıldığında tetiklenir.</summary>
    public event Action? RepositoryChanged;

    [ObservableProperty] private string _databasePath = "Veritabanı açık değil";
    [ObservableProperty] private string _statusText = "Bir veritabanı açın veya oluşturun, sonra PGN aktarın.";
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private bool _isImporting;
    [ObservableProperty] private string _importStatus = "";

    // Arama filtreleri.
    [ObservableProperty] private string _filterPlayer = "";
    [ObservableProperty] private string _filterWhite = "";
    [ObservableProperty] private string _filterBlack = "";
    [ObservableProperty] private string _filterEvent = "";
    [ObservableProperty] private string _filterEco = "";
    [ObservableProperty] private string _filterResult = "";
    [ObservableProperty] private GameHeader? _selectedGame;

    public string[] ResultOptions { get; } = { "", "1-0", "0-1", "1/2-1/2" };

    /// <summary>Bir oyun tahtaya yüklendiğinde tetiklenir (pencere kapatmak için).</summary>
    public event Action? GameOpened;

    public DatabaseViewModel(BoardViewModel board, SettingsService settings)
    {
        _board = board;
        _settings = settings;

        string? last = settings.Current.LastDatabasePath;
        if (!string.IsNullOrWhiteSpace(last) && File.Exists(last)) OpenPath(last);
    }

    // --- Veritabanı açma / oluşturma ---------------------------------------

    [RelayCommand]
    private void NewDatabase()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Yeni veritabanı", Filter = "Satranç DB (*.db)|*.db", FileName = "oyunlar.db", DefaultExt = ".db"
        };
        if (dialog.ShowDialog() == true) OpenPath(dialog.FileName);
    }

    [RelayCommand]
    private void OpenDatabase()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Veritabanı aç", Filter = "Satranç DB (*.db)|*.db|Tüm dosyalar (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true) OpenPath(dialog.FileName);
    }

    private void OpenPath(string path)
    {
        try
        {
            _db?.Dispose();
            _db = ChessDatabase.Open(path);
            _repo = new GameRepository(_db);
            _bookRepo = new BookRepository(_db);
            DatabasePath = path;
            IsOpen = true;
            _settings.Current.LastDatabasePath = path;
            _settings.Save();
            FilterPlayer = FilterWhite = FilterBlack = FilterEvent = FilterEco = FilterResult = "";
            RunSearch(BuildQuery());
            SearchCommand.NotifyCanExecuteChanged();
            ImportPgnCommand.NotifyCanExecuteChanged();
            SearchPositionCommand.NotifyCanExecuteChanged();
            SaveCurrentGameCommand.NotifyCanExecuteChanged();
            RepositoryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            // Açma başarısız oldu; _db zaten dispose edildi. _repo/_bookRepo'yu da temizle,
            // yoksa dispose edilmiş bağlantıya işaret ederek sonraki bir arama/kitap sorgusunda
            // ObjectDisposedException fırlatırlar.
            _db = null;
            _repo = null;
            _bookRepo = null;
            IsOpen = false;
            StatusText = $"Açılamadı: {ex.Message}";
            _settings.Current.LastDatabasePath = null;
            _settings.Save();
            SearchCommand.NotifyCanExecuteChanged();
            ImportPgnCommand.NotifyCanExecuteChanged();
            SearchPositionCommand.NotifyCanExecuteChanged();
            SaveCurrentGameCommand.NotifyCanExecuteChanged();
            RepositoryChanged?.Invoke();
        }
    }

    // --- İçe aktarma --------------------------------------------------------

    private bool CanUseDb() => IsOpen && !IsImporting;

    [RelayCommand(CanExecute = nameof(CanUseDb))]
    private async Task ImportPgn()
    {
        var dialog = new OpenFileDialog
        {
            Title = "PGN içe aktar", Filter = "PGN (*.pgn)|*.pgn|Tüm dosyalar (*.*)|*.*", Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        string path = dialog.FileName;
        ChessDatabase db = _db!;
        IsImporting = true;
        ImportPgnCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged();
        ImportStatus = "İçe aktarılıyor…";

        var progress = new Progress<ImportProgress>(p =>
            ImportStatus = $"{p.GamesImported:N0} oyun · {p.PositionsIndexed:N0} pozisyon" +
                           (p.Duplicates > 0 ? $" · {p.Duplicates} yinelenen atlandı" : "") +
                           (p.Skipped > 0 ? $" · {p.Skipped} hatalı atlandı" : ""));

        try
        {
            ImportResult result = await Task.Run(() =>
            {
                using var reader = new StreamReader(path);
                var importer = new PgnImporter(db);
                return importer.Import(reader, new ImportOptions(), progress);
            });

            StatusText = $"{result.GamesImported:N0} oyun aktarıldı " +
                         $"({result.GamesPerSecond:N0} oyun/sn, {result.PositionsIndexed:N0} pozisyon indekslendi)" +
                         (result.Duplicates > 0 ? $", {result.Duplicates:N0} yinelenen atlandı" : "") + ".";
            RefreshResults(BuildQuery()); // liste yeni aktarılan oyunları göstersin
            RepositoryChanged?.Invoke(); // kitap panosu yeni oyunlarla güncellensin
        }
        catch (PartialImportException ex)
        {
            // Önceki toplu işlemler zaten veritabanına yazıldı — kullanıcıya kısmi başarıyı bildir,
            // aksi halde "hata" mesajı yüzlerce/binlerce zaten aktarılmış oyunu gizler.
            StatusText = ex.Message;
            RefreshResults(BuildQuery());
            RepositoryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"İçe aktarma hatası: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
            ImportStatus = "";
            ImportPgnCommand.NotifyCanExecuteChanged();
            SearchCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseDb))]
    private void SaveCurrentGame()
    {
        ChessDatabase db = _db!;
        string pgn = _board.ExportPgn();

        try
        {
            using var reader = new StringReader(pgn);
            var importer = new PgnImporter(db);
            ImportResult result = importer.Import(reader, new ImportOptions());

            if (result.GamesImported > 0)
            {
                StatusText = "Oyun veritabanına eklendi.";
                if (_repo is not null) RefreshResults(BuildQuery()); // liste yeni oyunu göstersin
                RepositoryChanged?.Invoke(); // kitap panosu yeni oyunla güncellensin
            }
            else if (result.Duplicates > 0)
            {
                StatusText = "Bu oyun zaten veritabanında var.";
            }
            else
            {
                StatusText = "Kaydedilecek hamle yok.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Kaydetme hatası: {ex.Message}";
        }
    }

    // --- Arama --------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanUseDb))]
    private void Search() => RunSearch(BuildQuery());

    [RelayCommand(CanExecute = nameof(CanUseDb))]
    private void SearchPosition()
    {
        var q = BuildQuery();
        q.PositionHash = _board.Position.ZobristKey;
        RunSearch(q, $"Tahtadaki pozisyon aranıyor…");
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FilterPlayer = FilterWhite = FilterBlack = FilterEvent = FilterEco = FilterResult = "";
    }

    private GameQuery BuildQuery() => new()
    {
        Player = NullIfEmpty(FilterPlayer),
        White = NullIfEmpty(FilterWhite),
        Black = NullIfEmpty(FilterBlack),
        Event = NullIfEmpty(FilterEvent),
        Eco = NullIfEmpty(FilterEco),
        Result = NullIfEmpty(FilterResult),
        Limit = 1000
    };

    private void RunSearch(GameQuery query, string? busyText = null)
    {
        if (_repo is null) return;
        try
        {
            if (busyText != null) StatusText = busyText;
            int count = RefreshResults(query);
            StatusText = $"{count:N0} oyun bulundu" + (count >= query.Limit ? " (ilk 1000)" : "") + ".";
        }
        catch (Exception ex)
        {
            StatusText = $"Arama hatası: {ex.Message}";
        }
    }

    /// <summary>Mevcut filtrelerle sonuç listesini yeniden yükler; <see cref="StatusText"/>'e dokunmaz
    /// (içe aktarma/kaydetme sonrası özet mesajının üzerine yazılmasın diye).</summary>
    private int RefreshResults(GameQuery query)
    {
        List<GameHeader> hits = _repo!.Search(query);
        Results.Clear();
        foreach (GameHeader g in hits) Results.Add(g);
        return hits.Count;
    }

    // --- Oyun açma ----------------------------------------------------------

    [RelayCommand]
    private void OpenSelectedGame()
    {
        if (SelectedGame is null || _repo is null) return;
        string? pgn = _repo.LoadPgn(SelectedGame.Id);
        if (pgn is null) { StatusText = "Oyun bulunamadı."; return; }
        try
        {
            _board.LoadPgn(pgn);
            GameOpened?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"Oyun yüklenemedi: {ex.Message}";
        }
    }

    /// <summary>Verilen oyunları veritabanından siler (veritabanı penceresindeki tekli/toplu seçim için).</summary>
    public void DeleteGames(IEnumerable<GameHeader> games)
    {
        if (_repo is null) return;
        List<GameHeader> list = games.ToList();
        if (list.Count == 0) return;

        try
        {
            _repo.DeleteGames(list.Select(g => g.Id));
            foreach (GameHeader g in list) Results.Remove(g);
            StatusText = $"{list.Count:N0} oyun silindi.";
            if (SelectedGame is not null && list.Contains(SelectedGame)) SelectedGame = null;
            RepositoryChanged?.Invoke(); // kitap panosu güncel kalsın
        }
        catch (Exception ex)
        {
            StatusText = $"Silme hatası: {ex.Message}";
        }
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    public void Dispose()
    {
        _db?.Dispose();
        _db = null;
        _repo = null;
        _bookRepo = null;
    }
}
