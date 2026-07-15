using ChessGUI.App.Controls;
using ChessGUI.Core.Board;
using ChessGUI.Core.Game;
using ChessGUI.Core.Moves;
using ChessGUI.Core.Notation;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Oyunun durumunu bir <see cref="GameTree"/> + geçerli düğüm olarak tutar ve
/// <see cref="IBoardInteraction"/> sözleşmesini karşılar. Hamle oynama ağaca ekler
/// (varsa varyant açar), navigasyon düğümler arasında gezinir. PGN yükleme/dışa aktarma sağlar.
/// </summary>
public sealed class BoardViewModel : IBoardInteraction
{
    private GameTree _tree = new();
    private GameNode _current;
    private Position _position;
    private List<Move> _legal;
    private BoardOrientation _orientation = BoardOrientation.WhiteBottom;

    public BoardViewModel()
    {
        _current = _tree.Root;
        _position = _tree.CreateStartPosition();
        _legal = MoveGenerator.GenerateLegal(_position);
    }

    public Position Position => _position;
    public BoardOrientation Orientation => _orientation;
    public Move? LastMove => _current.IsRoot ? null : _current.Move;
    public GameTree Tree => _tree;
    public GameNode CurrentNode => _current;

    public bool CanBack => !_current.IsRoot;
    public bool CanForward => _current.HasChildren;

    /// <summary>Motor sırasındayken insan girdisini kilitlemek için; kilitliyken <see cref="TryMove"/> her zaman false döner.</summary>
    public bool InputLocked { get; set; }

    public event EventHandler? Changed;

    /// <summary><see cref="TryMove"/> içinde bir hamle başarıyla oynandığında tetiklenir (Play katmanı dinler).</summary>
    public event EventHandler<Move>? MovePlayed;

    // --- IBoardInteraction --------------------------------------------------

    public IReadOnlyList<int> GetLegalTargets(int fromSquare) =>
        _legal.Where(m => m.From == fromSquare).Select(m => m.To).Distinct().ToList();

    public bool IsLegalTarget(int from, int to) =>
        _legal.Any(m => m.From == from && m.To == to);

    public bool IsPromotion(int from, int to) =>
        _legal.Any(m => m.From == from && m.To == to && m.IsPromotion);

    public bool TryMove(int from, int to, PieceType promotion = PieceType.Queen)
    {
        if (InputLocked) return false;

        Move? found = null;
        foreach (Move m in _legal)
        {
            if (m.From != from || m.To != to) continue;
            if (m.IsPromotion && m.Promotion != promotion) continue;
            found = m;
            break;
        }
        if (found is null) return false;

        GameNode node = GameTree.AddMove(_current, found.Value, _position);
        _position.MakeMove(found.Value);
        _current = node;
        RefreshLegal();
        MovePlayed?.Invoke(this, found.Value);
        return true;
    }

    // --- Navigasyon ---------------------------------------------------------

    public void GoTo(GameNode node)
    {
        _current = node;
        _position = _tree.PositionAt(node);
        RefreshLegal();
    }

    public void Back() { if (CanBack) GoTo(_current.Parent!); }
    public void Forward() { if (_current.MainChild is { } next) GoTo(next); }
    public void GoToStart() => GoTo(_tree.Root);

    public void GoToEnd()
    {
        GameNode n = _current;
        while (n.MainChild is { } next) n = next;
        GoTo(n);
    }

    /// <summary>Geçerli hamleyi (yaprak ise) siler ve ebeveyne döner.</summary>
    public void DeleteCurrent()
    {
        if (_current.IsRoot) return;
        GameNode parent = _current.Parent!;
        GameTree.Remove(_current);
        GoTo(parent);
    }

    /// <summary>Belirtilen düğümü (ve alt ağacını) siler. Geçerli konum bu düğümün altındaysa ebeveyne döner.</summary>
    public void DeleteNode(GameNode node)
    {
        if (node.IsRoot) return;
        bool affectsCurrent = IsAncestorOrSelf(node, _current);
        GameTree.Remove(node);
        if (affectsCurrent) GoTo(node.Parent!);
        else RaiseChanged();
    }

    /// <summary>Düğümün hattını kökten itibaren ana hat yapar.</summary>
    public void PromoteNode(GameNode node)
    {
        if (node.IsRoot) return;
        GameTree.PromoteToMainLine(node);
        RaiseChanged();
    }

    private static bool IsAncestorOrSelf(GameNode ancestor, GameNode node)
    {
        for (GameNode? n = node; n != null; n = n.Parent)
            if (n == ancestor) return true;
        return false;
    }

    // --- Oyun / PGN ---------------------------------------------------------

    public void NewGame() => LoadGame(new GameTree());

    public void LoadGame(GameTree tree)
    {
        _tree = tree;
        _current = tree.Root;
        _position = tree.CreateStartPosition();
        RefreshLegal();
    }

    public void LoadPgn(string pgn) => LoadGame(Pgn.Parse(pgn));

    /// <summary>Konumu bir FEN dizesinden yükler. Geçersiz FEN, tahtaya dokunmadan bir istisna fırlatır.</summary>
    public void LoadFen(string fen)
    {
        Position.FromFen(fen); // doğrula (geçersizse fırlatır, tahtaya dokunmadan)
        LoadGame(new GameTree(fen));
    }

    public string ExportPgn() => Pgn.Write(_tree);

    public void Flip()
    {
        _orientation = _orientation == BoardOrientation.WhiteBottom
            ? BoardOrientation.BlackBottom
            : BoardOrientation.WhiteBottom;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pozisyonu değiştirmeden görünümleri yeniler (ör. rapor kalite sembolleri gelince).</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void RefreshLegal()
    {
        _legal = MoveGenerator.GenerateLegal(_position);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
