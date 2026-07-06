using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ChessGUI.Core.Board;
using ChessGUI.Core.Moves;
using MediaColor = System.Windows.Media.Color;
using ChessColor = ChessGUI.Core.Board.Color;

namespace ChessGUI.App.Controls;

/// <summary>
/// Satranç tahtasını özel çizimle gösteren ve fare etkileşimini (tıkla-oyna + sürükle-bırak)
/// yöneten görünüm bileşeni. Tüm satranç mantığını <see cref="IBoardInteraction"/> (DataContext)
/// üzerinden alır; kendisi kuralları bilmez. Yeniden çizim, host'un <c>Changed</c> olayıyla tetiklenir.
/// </summary>
public sealed class BoardControl : FrameworkElement
{
    // --- Tema renkleri (AppSettings'ten binding ile beslenebilir; DependencyProperty) -------
    public static readonly DependencyProperty LightSquareProperty = DependencyProperty.Register(
        nameof(LightSquare), typeof(Brush), typeof(BoardControl),
        new FrameworkPropertyMetadata(new SolidColorBrush(MediaColor.FromRgb(0xEE, 0xEE, 0xD2)),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DarkSquareProperty = DependencyProperty.Register(
        nameof(DarkSquare), typeof(Brush), typeof(BoardControl),
        new FrameworkPropertyMetadata(new SolidColorBrush(MediaColor.FromRgb(0x76, 0x96, 0x56)),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowCoordinatesProperty = DependencyProperty.Register(
        nameof(ShowCoordinates), typeof(bool), typeof(BoardControl),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PieceScaleProperty = DependencyProperty.Register(
        nameof(PieceScale), typeof(double), typeof(BoardControl),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AnimationEnabledProperty = DependencyProperty.Register(
        nameof(AnimationEnabled), typeof(bool), typeof(BoardControl),
        new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty AnimationDurationMsProperty = DependencyProperty.Register(
        nameof(AnimationDurationMs), typeof(int), typeof(BoardControl),
        new FrameworkPropertyMetadata(180));

    public Brush LightSquare { get => (Brush)GetValue(LightSquareProperty); set => SetValue(LightSquareProperty, value); }
    public Brush DarkSquare { get => (Brush)GetValue(DarkSquareProperty); set => SetValue(DarkSquareProperty, value); }
    public bool ShowCoordinates { get => (bool)GetValue(ShowCoordinatesProperty); set => SetValue(ShowCoordinatesProperty, value); }
    public double PieceScale { get => (double)GetValue(PieceScaleProperty); set => SetValue(PieceScaleProperty, value); }
    public bool AnimationEnabled { get => (bool)GetValue(AnimationEnabledProperty); set => SetValue(AnimationEnabledProperty, value); }
    public int AnimationDurationMs { get => (int)GetValue(AnimationDurationMsProperty); set => SetValue(AnimationDurationMsProperty, value); }

    public Brush LastMoveBrush { get; set; } = new SolidColorBrush(MediaColor.FromArgb(0x88, 0xF6, 0xF6, 0x69));
    public Brush SelectedBrush { get; set; } = new SolidColorBrush(MediaColor.FromArgb(0x99, 0xF6, 0xF6, 0x69));
    public Brush CheckBrush { get; set; } = new SolidColorBrush(MediaColor.FromArgb(0xB0, 0xE0, 0x4B, 0x4B));
    public Brush HintBrush { get; set; } = new SolidColorBrush(MediaColor.FromArgb(0x40, 0x10, 0x10, 0x10));

    private static readonly Brush WhiteBody = new SolidColorBrush(MediaColor.FromRgb(0xF5, 0xF5, 0xF0));
    private static readonly Brush WhiteEdge = new SolidColorBrush(MediaColor.FromRgb(0x33, 0x33, 0x33));
    private static readonly Brush BlackBody = new SolidColorBrush(MediaColor.FromRgb(0x2B, 0x2B, 0x2B));
    private static readonly Brush BlackEdge = new SolidColorBrush(MediaColor.FromRgb(0xD8, 0xD8, 0xD0));

    private static readonly Typeface PieceFont = new("Segoe UI Symbol");
    private static readonly Typeface CoordFont = new(new FontFamily("Segoe UI"),
        FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private IBoardInteraction? _host;
    private IBoardEditInteraction? _editHost;

    // Etkileşim durumu.
    private int _selected = Squares.None;
    private IReadOnlyList<int> _targets = Array.Empty<int>();
    private bool _dragging;
    private int _dragFrom = Squares.None;
    private Point _dragPoint;

    // Terfi seçici durumu.
    private bool _promotionPending;
    private int _promoFrom, _promoTo;
    private ChessColor _promoColor;
    private bool _promoWasDrag;
    private static readonly PieceType[] PromoChoices =
        { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

    // Taş kayma animasyonu durumu.
    private Move? _animMove;
    private Piece _animPiece;
    private readonly Stopwatch _animClock = new();
    private DispatcherTimer? _animTimer;
    private bool _suppressNextAnimation;

    static BoardControl()
    {
        // Freeze ile paylaşılan fırçalar performanslı ve iş parçacığı-güvenli olur.
        foreach (var b in new[] { WhiteBody, WhiteEdge, BlackBody, BlackEdge })
            ((SolidColorBrush)b).Freeze();
    }

    public BoardControl()
    {
        Focusable = true;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_host != null)
        {
            _host.Changed -= OnHostChanged;
            _host.MovePlayed -= OnMovePlayed;
        }
        if (_editHost != null) _editHost.Changed -= OnEditHostChanged;

        _host = e.NewValue as IBoardInteraction;
        _editHost = e.NewValue as IBoardEditInteraction;

        if (_host != null)
        {
            _host.Changed += OnHostChanged;
            _host.MovePlayed += OnMovePlayed;
        }
        if (_editHost != null) _editHost.Changed += OnEditHostChanged;

        ResetInteraction();
        CancelAnimation();
        InvalidateVisual();
    }

    private void OnEditHostChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnHostChanged(object? sender, EventArgs e)
    {
        // Pozisyon dışarıdan değişince (undo, yeni oyun, navigasyon, çevirme) seçim/sürükleme
        // sıfırlanır ve olası bir animasyon iptal edilir (havada taş kalmasın).
        ResetInteraction();
        CancelAnimation();
        InvalidateVisual();
    }

    private void OnMovePlayed(object? sender, Move move)
    {
        bool suppress = _suppressNextAnimation;
        _suppressNextAnimation = false;

        if (_host == null || !AnimationEnabled || AnimationDurationMs <= 0 || suppress)
        {
            CancelAnimation();
            InvalidateVisual();
            return;
        }

        // Yeni hamle animasyon ortasında gelirse basitçe yeniden başlar.
        _animMove = move;
        _animPiece = _host.Position[move.To];
        _animClock.Restart();

        if (_animTimer == null)
        {
            _animTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(15)
            };
            _animTimer.Tick += OnAnimationTick;
        }
        _animTimer.Start();
        InvalidateVisual();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_animMove is null) { _animTimer?.Stop(); return; }

        if (_animClock.Elapsed.TotalMilliseconds >= AnimationDurationMs)
        {
            _animMove = null;
            _animClock.Reset();
            _animTimer?.Stop();
        }
        InvalidateVisual();
    }

    private void CancelAnimation()
    {
        _animTimer?.Stop();
        _animMove = null;
        _animClock.Reset();
    }

    private static double EaseOutCubic(double t)
    {
        double u = 1 - t;
        return 1 - u * u * u;
    }

    private void ResetInteraction()
    {
        _selected = Squares.None;
        _targets = Array.Empty<int>();
        _dragging = false;
        _dragFrom = Squares.None;
        _promotionPending = false;
    }

    // --- Geometri -----------------------------------------------------------

    private double BoardSize => Math.Min(ActualWidth, ActualHeight);
    private double SquareSize => BoardSize / 8.0;
    private double OriginX => (ActualWidth - BoardSize) / 2.0;
    private double OriginY => (ActualHeight - BoardSize) / 2.0;

    private BoardOrientation Orientation => _host?.Orientation ?? _editHost?.Orientation ?? BoardOrientation.WhiteBottom;

    private Rect SquareRect(int square)
    {
        int file = Squares.FileOf(square), rank = Squares.RankOf(square);
        int col = Orientation == BoardOrientation.WhiteBottom ? file : 7 - file;
        int row = Orientation == BoardOrientation.WhiteBottom ? 7 - rank : rank;
        return new Rect(OriginX + col * SquareSize, OriginY + row * SquareSize, SquareSize, SquareSize);
    }

    private int SquareAt(Point p)
    {
        double s = SquareSize;
        if (s <= 0) return Squares.None;
        int col = (int)Math.Floor((p.X - OriginX) / s);
        int row = (int)Math.Floor((p.Y - OriginY) / s);
        if (col is < 0 or > 7 || row is < 0 or > 7) return Squares.None;
        int file = Orientation == BoardOrientation.WhiteBottom ? col : 7 - col;
        int rank = Orientation == BoardOrientation.WhiteBottom ? 7 - row : row;
        return Squares.Of(file, rank);
    }

    // --- Çizim --------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (BoardSize <= 0) return;

        if (_editHost != null) { RenderEditMode(dc); return; }
        if (_host == null) return;

        var pos = _host.Position;
        double s = SquareSize;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Kareler.
        for (int square = 0; square < 64; square++)
        {
            Rect r = SquareRect(square);
            int file = Squares.FileOf(square), rank = Squares.RankOf(square);
            bool dark = (file + rank) % 2 == 0;
            dc.DrawRectangle(dark ? DarkSquare : LightSquare, null, r);
        }

        // Son hamle vurgusu (karelerin üstüne, seçimin altına).
        if (_host.LastMove is { } last)
        {
            dc.DrawRectangle(LastMoveBrush, null, SquareRect(last.From));
            dc.DrawRectangle(LastMoveBrush, null, SquareRect(last.To));
        }

        // Seçili kare + şah vurgusu.
        if (_selected != Squares.None)
            dc.DrawRectangle(SelectedBrush, null, SquareRect(_selected));

        if (pos.IsSideToMoveInCheck())
            dc.DrawRectangle(CheckBrush, null, SquareRect(pos.KingSquare(pos.SideToMove)));

        // Koordinatlar (kenar karelerin köşesinde, lichess tarzı).
        if (ShowCoordinates) DrawCoordinates(dc, s, dpi);

        // Yasal hedef ipuçları.
        foreach (int t in _targets)
        {
            Rect r = SquareRect(t);
            bool capture = !pos[t].IsNone;
            Point c = new(r.X + r.Width / 2, r.Y + r.Height / 2);
            if (capture)
            {
                double rad = r.Width * 0.46;
                var pen = new Pen(HintBrush, r.Width * 0.09);
                dc.DrawEllipse(null, pen, c, rad, rad);
            }
            else
            {
                dc.DrawEllipse(HintBrush, null, c, r.Width * 0.16, r.Width * 0.16);
            }
        }

        // Taşlar (sürüklenen ve animasyonlu hedef kare hariç).
        for (int square = 0; square < 64; square++)
        {
            Piece piece = pos[square];
            if (piece.IsNone) continue;
            if (_dragging && square == _dragFrom) continue;
            if (_animMove is { } am && square == am.To) continue;
            DrawPiece(dc, piece, SquareRect(square), dpi);
        }

        // Kayma animasyonu: taş, kaynak kareden hedef kareye ease-out cubic ile enterpole edilir.
        if (_animMove is { } anim && !_animPiece.IsNone)
        {
            double t = Math.Clamp(_animClock.Elapsed.TotalMilliseconds / Math.Max(1, AnimationDurationMs), 0.0, 1.0);
            double e = EaseOutCubic(t);
            Rect from = SquareRect(anim.From);
            Rect to = SquareRect(anim.To);
            var r = new Rect(
                from.X + (to.X - from.X) * e,
                from.Y + (to.Y - from.Y) * e,
                from.Width, from.Height);
            DrawPiece(dc, _animPiece, r, dpi);
        }

        // Sürüklenen taş, imlecin altında.
        if (_dragging && _dragFrom != Squares.None)
        {
            Piece piece = pos[_dragFrom];
            if (!piece.IsNone)
            {
                var r = new Rect(_dragPoint.X - s / 2, _dragPoint.Y - s / 2, s, s);
                DrawPiece(dc, piece, r, dpi);
            }
        }

        if (_promotionPending)
            DrawPromotionChooser(dc, dpi);
    }

    /// <summary>Pozisyon düzenleme modunda çizim: sadece kareler + taşlar (hamle/vurgu/animasyon yok).</summary>
    private void RenderEditMode(DrawingContext dc)
    {
        var pos = _editHost!.Position;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (int square = 0; square < 64; square++)
        {
            Rect r = SquareRect(square);
            int file = Squares.FileOf(square), rank = Squares.RankOf(square);
            bool dark = (file + rank) % 2 == 0;
            dc.DrawRectangle(dark ? DarkSquare : LightSquare, null, r);
        }

        if (ShowCoordinates) DrawCoordinates(dc, SquareSize, dpi);

        for (int square = 0; square < 64; square++)
        {
            Piece piece = pos[square];
            if (piece.IsNone) continue;
            DrawPiece(dc, piece, SquareRect(square), dpi);
        }
    }

    private void DrawCoordinates(DrawingContext dc, double s, double dpi)
    {
        double fontSize = s * 0.20;
        for (int i = 0; i < 8; i++)
        {
            // Alt kenar: dosya harfleri.
            int fileSquare = Orientation == BoardOrientation.WhiteBottom
                ? Squares.Of(i, 0) : Squares.Of(7 - i, 0);
            Rect fr = SquareRect(fileSquare);
            char fileChar = (char)('a' + Squares.FileOf(fileSquare));
            bool fileDark = (Squares.FileOf(fileSquare) + 0) % 2 == 0;
            DrawText(dc, fileChar.ToString(), CoordFont, fontSize,
                fileDark ? LightSquare : DarkSquare,
                fr.Right - fontSize * 0.85, fr.Bottom - fontSize * 1.25, dpi);

            // Sol kenar: yatay numaraları.
            int rankSquare = Orientation == BoardOrientation.WhiteBottom
                ? Squares.Of(0, i) : Squares.Of(0, 7 - i);
            Rect rr = SquareRect(rankSquare);
            char rankChar = (char)('1' + Squares.RankOf(rankSquare));
            bool rankDark = (0 + Squares.RankOf(rankSquare)) % 2 == 0;
            DrawText(dc, rankChar.ToString(), CoordFont, fontSize,
                rankDark ? LightSquare : DarkSquare,
                rr.Left + fontSize * 0.25, rr.Top + fontSize * 0.15, dpi);
        }
    }

    private void DrawPiece(DrawingContext dc, Piece piece, Rect r, double dpi)
    {
        double size = r.Height * 0.82 * PieceScale;
        Brush body = piece.Color == ChessColor.White ? WhiteBody : BlackBody;
        Brush edge = piece.Color == ChessColor.White ? WhiteEdge : BlackEdge;

        var bodyText = MakeGlyph(ChessGlyphs.Body(piece.Type), size, body, dpi);
        var edgeText = MakeGlyph(ChessGlyphs.Edge(piece.Type), size, edge, dpi);

        double x = r.X + (r.Width - bodyText.Width) / 2;
        double y = r.Y + (r.Height - bodyText.Height) / 2;
        dc.DrawText(bodyText, new Point(x, y));
        dc.DrawText(edgeText, new Point(x, y));
    }

    private static FormattedText MakeGlyph(string glyph, double size, Brush brush, double dpi) =>
        new(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, PieceFont, size, brush, dpi);

    private static void DrawText(DrawingContext dc, string text, Typeface face, double size, Brush brush,
        double x, double y, double dpi)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            face, size, brush, dpi);
        dc.DrawText(ft, new Point(x, y));
    }

    private void DrawPromotionChooser(DrawingContext dc, double dpi)
    {
        double s = SquareSize;
        // Yarı saydam arka plan.
        dc.DrawRectangle(new SolidColorBrush(MediaColor.FromArgb(0xB0, 0x00, 0x00, 0x00)), null,
            new Rect(OriginX, OriginY, BoardSize, BoardSize));

        Rect toRect = SquareRect(_promoTo);
        // Terfi taşları hedef dosyada dikey sıralanır; taşan taraf aşağı doğru sarkar.
        for (int i = 0; i < PromoChoices.Length; i++)
        {
            double y = toRect.Y + (Orientation == BoardOrientation.WhiteBottom
                ? (_promoColor == ChessColor.White ? i * s : -i * s)
                : (_promoColor == ChessColor.White ? -i * s : i * s));
            var cell = new Rect(toRect.X, y, s, s);
            dc.DrawRectangle(new SolidColorBrush(MediaColor.FromRgb(0xF0, 0xF0, 0xF0)), null, cell);
            DrawPiece(dc, new Piece(_promoColor, PromoChoices[i]), cell, dpi);
        }
    }

    private Rect PromoCellRect(int index)
    {
        double s = SquareSize;
        Rect toRect = SquareRect(_promoTo);
        double y = toRect.Y + (Orientation == BoardOrientation.WhiteBottom
            ? (_promoColor == ChessColor.White ? index * s : -index * s)
            : (_promoColor == ChessColor.White ? -index * s : index * s));
        return new Rect(toRect.X, y, s, s);
    }

    // --- Fare ---------------------------------------------------------------

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_host == null && _editHost == null) return;
        Focus();
        Point p = e.GetPosition(this);

        if (_editHost != null)
        {
            int editSq = SquareAt(p);
            if (editSq != Squares.None)
            {
                if (_editHost.ArmedPiece is { } armed) _editHost.PlacePiece(editSq, armed);
                else _editHost.ClearSquare(editSq);
            }
            return;
        }

        if (_host == null) return;

        if (_promotionPending)
        {
            HandlePromotionClick(p);
            return;
        }

        int sq = SquareAt(p);
        if (sq == Squares.None) { ClearSelection(); return; }

        // Seçili bir taş varsa ve tıklanan yasal hedefse -> hamle (tıkla-tıkla, sürükleme değil).
        if (_selected != Squares.None && _host.IsLegalTarget(_selected, sq))
        {
            ExecuteMove(_selected, sq, isDrag: false);
            return;
        }

        // Sıradaki tarafın taşına tıklanırsa seç + sürüklemeye hazırlan.
        Piece piece = _host.Position[sq];
        if (!piece.IsNone && piece.Color == _host.Position.SideToMove)
        {
            _selected = sq;
            _targets = _host.GetLegalTargets(sq);
            _dragFrom = sq;
            _dragPoint = p;
            CaptureMouse();
            InvalidateVisual();
        }
        else
        {
            ClearSelection();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_host == null || _dragFrom == Squares.None || e.LeftButton != MouseButtonState.Pressed) return;

        Point p = e.GetPosition(this);
        if (!_dragging)
        {
            // Küçük eşik aşılınca sürükleme başlar.
            if (Math.Abs(p.X - _dragPoint.X) + Math.Abs(p.Y - _dragPoint.Y) < 4) return;
            _dragging = true;
        }
        _dragPoint = p;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (_host == null || !_dragging) { _dragFrom = Squares.None; return; }

        _dragging = false;
        int target = SquareAt(e.GetPosition(this));
        int from = _dragFrom;
        _dragFrom = Squares.None;

        if (target != Squares.None && from != target && _host.IsLegalTarget(from, target))
            ExecuteMove(from, target, isDrag: true);
        else
            InvalidateVisual(); // sürükleme geçersiz -> seçim kalır, taş yerine döner
    }

    private void ExecuteMove(int from, int to, bool isDrag)
    {
        if (_host!.IsPromotion(from, to))
        {
            _promotionPending = true;
            _promoFrom = from;
            _promoTo = to;
            _promoColor = _host.Position.SideToMove;
            _promoWasDrag = isDrag;
            _selected = Squares.None;
            _targets = Array.Empty<int>();
            InvalidateVisual();
            return;
        }

        // Sürükle-bırak ile oynanan hamlede taş zaten fareyle hedefe taşınmış oluyor;
        // bırakınca tekrar kayma animasyonu göstermek gereksiz/rahatsız edici olur.
        _suppressNextAnimation = isDrag;
        _host.TryMove(from, to);
        ClearSelection();
    }

    private void HandlePromotionClick(Point p)
    {
        for (int i = 0; i < PromoChoices.Length; i++)
        {
            if (PromoCellRect(i).Contains(p))
            {
                _promotionPending = false;
                _suppressNextAnimation = _promoWasDrag;
                _host!.TryMove(_promoFrom, _promoTo, PromoChoices[i]);
                ClearSelection();
                return;
            }
        }
        // Dışarı tıklama -> iptal.
        _promotionPending = false;
        ClearSelection();
    }

    private void ClearSelection()
    {
        _selected = Squares.None;
        _targets = Array.Empty<int>();
        InvalidateVisual();
    }
}
