using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ChessGUI.App.Controls;

/// <summary>
/// Dikey değerlendirme çubuğu: beyazın kazanma olasılığını (0..1) alttan yukarı beyaz dolguyla
/// gösterir. Beyaz her zaman altta (standart). Ortadaki metin kısa değerlendirmeyi yazar.
/// </summary>
public sealed class EvalBar : FrameworkElement
{
    public static readonly DependencyProperty WhiteWinProbabilityProperty =
        DependencyProperty.Register(nameof(WhiteWinProbability), typeof(double), typeof(EvalBar),
            new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(nameof(Caption), typeof(string), typeof(EvalBar),
            new FrameworkPropertyMetadata("0.0", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WhiteFavoredProperty =
        DependencyProperty.Register(nameof(WhiteFavored), typeof(bool), typeof(EvalBar),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public double WhiteWinProbability
    {
        get => (double)GetValue(WhiteWinProbabilityProperty);
        set => SetValue(WhiteWinProbabilityProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>Değerlendirme beyaz lehine mi? (Metnin hangi uçta gösterileceğini belirler.)</summary>
    public bool WhiteFavored
    {
        get => (bool)GetValue(WhiteFavoredProperty);
        set => SetValue(WhiteFavoredProperty, value);
    }

    private static readonly Brush WhiteFill = Freeze(Color.FromRgb(0xEC, 0xEC, 0xEC));
    private static readonly Brush BlackFill = Freeze(Color.FromRgb(0x30, 0x30, 0x36));
    private static readonly Typeface Font = new("Segoe UI");

    private const double BarWidth = 22;
    private const double BadgeGap = 4;
    private const double BadgePadX = 5;
    private const double BadgePadY = 2;

    private static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double barW = Math.Min(BarWidth, w);
        double p = Math.Clamp(WhiteWinProbability, 0, 1);
        double whiteHeight = h * p;
        double boundaryY = h - whiteHeight;

        dc.DrawRectangle(BlackFill, null, new Rect(0, 0, barW, boundaryY));
        dc.DrawRectangle(WhiteFill, null, new Rect(0, boundaryY, barW, whiteHeight));

        // Değerlendirme, çubuğun hemen yanında, siyah/beyaz kesişim hizasında sabit
        // renkli bir rozetin (badge) içinde gösterilir; böylece hangi tema veya hangi
        // taraf favori olursa olsun her zaman okunur kalır.
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var ft = new FormattedText(Caption, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Font, 12, WhiteFill, dpi);

        double badgeW = ft.Width + BadgePadX * 2;
        double badgeH = ft.Height + BadgePadY * 2;
        double cy = Math.Clamp(boundaryY, badgeH / 2, Math.Max(badgeH / 2, h - badgeH / 2));
        var badgeRect = new Rect(barW + BadgeGap, cy - badgeH / 2, badgeW, badgeH);

        dc.DrawRoundedRectangle(BlackFill, null, badgeRect, 3, 3);
        dc.DrawText(ft, new Point(badgeRect.X + BadgePadX, badgeRect.Y + BadgePadY));
    }
}
