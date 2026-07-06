using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessGUI.Core.Analysis;
using ChessGUI.Core.Board;
using ChessGUI.Core.Game;
using ChessGUI.Engine;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Ana hattı motorla baştan sona tarayıp her hamleyi sınıflandırır (blunder/mistake/inaccuracy…),
/// taraf başına doğruluk (accuracy) skorunu ve beyaz-bakışlı değerlendirme eğrisini üretir.
/// Kendi <see cref="EngineAnalyzer"/> alt-sürecini kullanır; etkileşimli analizden bağımsızdır.
/// </summary>
public sealed partial class GameReportViewModel : ObservableObject
{
    private readonly BoardViewModel _board;
    private readonly EngineViewModel _engine;
    private CancellationTokenSource? _cts;
    private readonly List<GameNode> _graphNodes = new();

    /// <summary>Beyaz bakışlı kazanma yüzdeleri (0..100), her ana-hat düğümü için (kök dahil).</summary>
    public ObservableCollection<double> GraphPoints { get; } = new();

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _hasReport;
    [ObservableProperty] private int _progressPercent;
    [ObservableProperty] private string _statusText = "Rapor için oyunu tara.";
    [ObservableProperty] private string _whiteAccuracy = "—";
    [ObservableProperty] private string _blackAccuracy = "—";
    [ObservableProperty] private double _reportSeconds = 1.0;
    [ObservableProperty] private int _currentIndex = -1;

    public double[] TimeOptions { get; } = { 0.2, 0.5, 1, 2, 3, 5 };

    public GameReportViewModel(BoardViewModel board, EngineViewModel engine)
    {
        _board = board;
        _engine = engine;
        _board.Changed += (_, _) => UpdateCurrentIndex();
    }

    [RelayCommand]
    private async Task Run()
    {
        if (IsRunning) { _cts?.Cancel(); return; }

        string? path = _engine.SelectedEnginePath;
        if (path is null) { StatusText = "Önce bir motor ekleyin (+ Ekle)."; return; }

        _engine.StopForReport();
        _cts = new CancellationTokenSource();
        CancellationToken ct = _cts.Token;

        IsRunning = true;
        HasReport = false;
        ProgressPercent = 0;
        WhiteAccuracy = BlackAccuracy = "…";

        var analyzer = new EngineAnalyzer();
        try
        {
            // Ana hattın pozisyonlarını topla.
            GameTree tree = _board.Tree;
            var nodes = new List<GameNode>();
            var positions = new List<Position>();
            Position pos = tree.CreateStartPosition();
            nodes.Add(tree.Root);
            positions.Add(pos.Clone());
            for (GameNode? n = tree.Root.MainChild; n != null; n = n.MainChild)
            {
                pos.MakeMove(n.Move);
                nodes.Add(n);
                positions.Add(pos.Clone());
            }

            int total = nodes.Count;
            if (total < 2) { StatusText = "Analiz edilecek hamle yok."; IsRunning = false; return; }

            // Üç-tekrar tespiti için: her pozisyonun anahtarı, sırayla (bir kez hesaplanır).
            var zobristKeys = new ulong[total];
            for (int k = 0; k < total; k++) zobristKeys[k] = positions[k].ZobristKey;

            await analyzer.StartAsync(path, Math.Max(1, Environment.ProcessorCount - 1), 128, ct);

            var winWhite = new double[total];
            var bestMoves = new string?[total];
            int moveTimeMs = (int)Math.Round(ReportSeconds * 1000);

            int done = 0;
            for (int i = total - 1; i >= 0; i--)
            {
                ct.ThrowIfCancellationRequested();
                Position p = positions[i];
                bool whiteToMove = p.SideToMove == Color.White;

                var historyUpToHere = new ArraySegment<ulong>(zobristKeys, 0, i + 1);
                switch (GameEnd.Evaluate(p, historyUpToHere))
                {
                    case GameStatus.Checkmate:
                        winWhite[i] = whiteToMove ? 0 : 100;
                        nodes[i].EvalMate = 0;
                        nodes[i].EvalCp = whiteToMove ? -100000 : 100000;
                        break;
                    case GameStatus.Stalemate:
                    case GameStatus.FiftyMoveRule:
                    case GameStatus.InsufficientMaterial:
                    case GameStatus.Repetition:
                        winWhite[i] = 50;
                        nodes[i].EvalCp = 0;
                        break;
                    default:
                        SearchResult sr = await analyzer.EvaluateAsync(p.ToFen(), moveTimeMs, ct);
                        bestMoves[i] = sr.BestMove;
                        if (sr.ScoreMate is int m)
                        {
                            int mw = whiteToMove ? m : -m;
                            nodes[i].EvalMate = mw;
                            nodes[i].EvalCp = Math.Sign(mw) * 100000;
                            winWhite[i] = Evaluation.WinPercentFromScore(null, mw);
                        }
                        else
                        {
                            int cpw = whiteToMove ? (sr.ScoreCp ?? 0) : -(sr.ScoreCp ?? 0);
                            nodes[i].EvalCp = cpw;
                            nodes[i].EvalMate = null;
                            winWhite[i] = Evaluation.WinPercentFromScore(cpw, null);
                        }
                        break;
                }

                done++;
                ProgressPercent = (int)(done * 100.0 / total);
                StatusText = $"Analiz ediliyor… {done}/{total}  (süre {ReportSeconds:0.#} sn)";
            }

            // Hamleleri sınıflandır + accuracy topla.
            var whiteAccs = new List<double>();
            var blackAccs = new List<double>();
            for (int i = 1; i < total; i++)
            {
                bool moverWhite = positions[i - 1].SideToMove == Color.White;
                double before = Evaluation.OrientToMover(winWhite[i - 1], moverWhite);
                double after = Evaluation.OrientToMover(winWhite[i], moverWhite);
                bool best = bestMoves[i - 1] != null &&
                            string.Equals(bestMoves[i - 1], nodes[i].Move.ToUci(), StringComparison.OrdinalIgnoreCase);

                nodes[i].Quality = MoveClassifier.Classify(before, after, best);
                double acc = Evaluation.MoveAccuracy(before, after);
                (moverWhite ? whiteAccs : blackAccs).Add(acc);
            }

            WhiteAccuracy = whiteAccs.Count > 0 ? $"{whiteAccs.Average():0.0}%" : "—";
            BlackAccuracy = blackAccs.Count > 0 ? $"{blackAccs.Average():0.0}%" : "—";

            _graphNodes.Clear();
            _graphNodes.AddRange(nodes);
            GraphPoints.Clear();
            foreach (double w in winWhite) GraphPoints.Add(w);

            HasReport = true;
            StatusText = "Rapor tamamlandı.";
            _board.RaiseChanged(); // hamle listesindeki kalite sembollerini göster
            UpdateCurrentIndex();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Rapor iptal edildi.";
        }
        catch (Exception ex)
        {
            StatusText = $"Rapor hatası: {ex.Message}";
        }
        finally
        {
            analyzer.Dispose();
            IsRunning = false;
            _cts = null;
        }
    }

    [RelayCommand]
    private void GraphClick(int index)
    {
        if (index >= 0 && index < _graphNodes.Count)
            _board.GoTo(_graphNodes[index]);
    }

    private void UpdateCurrentIndex()
    {
        CurrentIndex = _graphNodes.IndexOf(_board.CurrentNode);
    }
}
