using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AchievementCreationTool.Controls;

/// <summary>Grey text in an empty text box, saying what goes in it: c:Placeholder.Text="Such as Friend of Ravens".
/// It stays while the box has focus, until something's typed.</summary>
public static class Placeholder
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached("Text", typeof(string), typeof(Placeholder),
        new PropertyMetadata(null, OnTextChanged));

    public static string? GetText(DependencyObject d) => (string?)d.GetValue(TextProperty);
    public static void SetText(DependencyObject d, string? value) => d.SetValue(TextProperty, value);

    static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
            return;
        box.Loaded -= Update;
        box.TextChanged -= Update;
        box.IsVisibleChanged -= VisibilityChanged;
        box.Loaded += Update;
        box.TextChanged += Update;
        box.IsVisibleChanged += VisibilityChanged;
        Update(box, null);
    }

    static void Update(object sender, RoutedEventArgs? e)
    {
        if (sender is not TextBox box || AdornerLayer.GetAdornerLayer(box) is not { } layer)
            return;
        var shown = layer.GetAdorners(box)?.OfType<HintAdorner>().FirstOrDefault();
        // The adorner layer draws above everything, so a box that's hidden, or inside something hidden, takes its hint with it
        var want = box.IsVisible && box.Text.Length == 0 && GetText(box) is { Length: > 0 };
        if (want && shown is null)
            layer.Add(new HintAdorner(box));
        else if (!want && shown is not null)
            layer.Remove(shown);
        else
            shown?.InvalidateVisual();
    }

    static void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => Update(sender, null);

    sealed class HintAdorner : Adorner
    {
        readonly TextBox box;

        public HintAdorner(TextBox box) : base(box)
        {
            this.box = box;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            var text = GetText(box);
            if (string.IsNullOrEmpty(text))
                return;
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(box.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), box.FontSize,
                new SolidColorBrush(Color.FromRgb(0x7A, 0x80, 0x88)), dpi)
            {
                MaxTextWidth = Math.Max(10, box.ActualWidth - box.Padding.Left - box.Padding.Right - 16),
                MaxLineCount = box.AcceptsReturn || box.TextWrapping != TextWrapping.NoWrap ? 3 : 1,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            // Where the text box puts its own text: its padding, its border, and the caret's room
            var left = box.Padding.Left + box.BorderThickness.Left + 2;
            var top = box.TextWrapping != TextWrapping.NoWrap || box.AcceptsReturn
                ? box.Padding.Top + box.BorderThickness.Top + 1
                : (box.ActualHeight - formatted.Height) / 2;
            dc.DrawText(formatted, new Point(left, top));
        }
    }
}
