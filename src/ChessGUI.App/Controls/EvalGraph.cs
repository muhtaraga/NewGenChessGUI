using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ChessGUI.App.Controls;

/// <summary>
/// Oyun boyunca beyaz-bakışlı kazanma yüzdesini (0..100) gösteren alan grafiği. Beyaz avantaj
/// bölgesi açık renkle dolar. Bir noktaya tıklamak o hamleye gitmek için komutu tetikler.
/// </summary>
public sealed class EvalGraph : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(nameof(Points), typeof(IEnumerable), typeof(EvalGraph),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public static readonly DependencyProperty CurrentIndexProperty =
        DependencyProperty.Register(nameof(CurrentIndex), typeof(int), typeof(EvalGraph),
            new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty NavigateCommandProperty =
        DependencyProperty.Register(nameof(NavigateCommand), typeof(ICommand), typeof(EvalGraph));

    public IEnumerable? Points { get => (IEnumerable?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public int CurrentIndex { get => (int)GetValue(CurrentIndexProperty); set => SetValue(CurrentIndexProperty, value); }
    public ICommand? NavigateCommand { get => (ICommand?)GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    private static readonly Brush Background = Freeze(Color.FromRgb(0x1B, 0x1B, 0x1F));
    private static readonly Brush WhiteArea = Freeze(Color.FromRgb(0xC9, 0xC9, 0xD0));
    private static readonly Pen LinePen = FreezePen(Color.FromRgb(0x0E, 0x0E, 0x12), 1.2);
    private static readonly Pen MidPen = FreezePen(Color.FromArgb(0x60, 0x88, 0x88, 0x90), 0.8);
    private static readonly Pen CurrentPen = FreezePen(Color.FromRgb(0x4A, 0x9E, 0xDA), 1.4);

    private static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    private static Pen FreezePen(Color c, double w) { var p = new Pen(Freeze(c), w); p.Freeze(); return p; }

    public EvalGraph() => Cursor = Cursors.Hand;

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var graph = (EvalGraph)d;
        if (e.OldValue is INotifyCollectionChanged oldC) oldC.CollectionChanged -= graph.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newC) newC.CollectionChanged += graph.OnCollectionChanged;
        graph.InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    private double[] Snapshot()
    {
        if (Points == null) return Array.Empty<double>();
        var list = new List<double>();
        foreach (object o in Points) list.Add(Convert.ToDouble(o));
        return list.ToArray();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));
        dc.DrawLine(MidPen, new Point(0, h / 2), new Point(w, h / 2));

        double[] pts = Snapshot();
        if (pts.Length < 2) return;

        double step = w / (pts.Length - 1);
        double Y(double win) => h - (win / 100.0) * h; // 100 -> üst (beyaz iyi)

        // Beyaz avantaj alanı (eğrinin altı).
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(0, h), true, true);
            g.LineTo(new Point(0, Y(pts[0])), true, false);
            for (int i = 1; i < pts.Length; i++)
                g.LineTo(new Point(i * step, Y(pts[i])), true, false);
            g.LineTo(new Point((pts.Length - 1) * step, h), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(WhiteArea, null, geo);

        // Eğri çizgisi.
        for (int i = 1; i < pts.Length; i++)
            dc.DrawLine(LinePen, new Point((i - 1) * step, Y(pts[i - 1])), new Point(i * step, Y(pts[i])));

        // Geçerli hamle imleci.
        if (CurrentIndex >= 0 && CurrentIndex < pts.Length)
        {
            double x = CurrentIndex * step;
            dc.DrawLine(CurrentPen, new Point(x, 0), new Point(x, h));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        double[] pts = Snapshot();
        if (pts.Length < 2 || ActualWidth <= 0) return;

        double step = ActualWidth / (pts.Length - 1);
        int index = (int)Math.Round(e.GetPosition(this).X / step);
        index = Math.Clamp(index, 0, pts.Length - 1);

        if (NavigateCommand?.CanExecute(index) == true)
            NavigateCommand.Execute(index);
    }
}
