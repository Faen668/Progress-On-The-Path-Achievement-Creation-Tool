using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Path = System.Windows.Shapes.Path;
using AchievementCreationTool.Data;

namespace AchievementCreationTool.Controls;

/// <summary>An achievement's icon as PotP's overlay draws it: one of PotP's own icons as lines in its ring's colour, or a
/// picture, on a dark square; while locked, the red of the game's locked achievements. A picture from the game's own files
/// can't be read here, so it's shown as a frame saying so (the game shows the real one).</summary>
public sealed class IconView : Border
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(IconView),
        new PropertyMetadata("builtin:trophy", (d, _) => ((IconView)d).Rebuild()));
    public static readonly DependencyProperty RingProperty = DependencyProperty.Register(nameof(Ring), typeof(string), typeof(IconView),
        new PropertyMetadata("gold", (d, _) => ((IconView)d).Rebuild()));
    public static readonly DependencyProperty LockedProperty = DependencyProperty.Register(nameof(Locked), typeof(bool), typeof(IconView),
        new PropertyMetadata(false, (d, _) => ((IconView)d).Rebuild()));
    public static readonly DependencyProperty FolderProperty = DependencyProperty.Register(nameof(Folder), typeof(string), typeof(IconView),
        new PropertyMetadata(null, (d, _) => ((IconView)d).Rebuild()));
    public static readonly DependencyProperty FramedProperty = DependencyProperty.Register(nameof(Framed), typeof(bool), typeof(IconView),
        new PropertyMetadata(true, (d, _) => ((IconView)d).Rebuild()));

    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Ring { get => (string)GetValue(RingProperty); set => SetValue(RingProperty, value); }
    public bool Locked { get => (bool)GetValue(LockedProperty); set => SetValue(LockedProperty, value); }
    /// <summary>The pack's folder, for a picture of its own</summary>
    public string? Folder { get => (string?)GetValue(FolderProperty); set => SetValue(FolderProperty, value); }
    /// <summary>False for just the icon, with no square behind it (the herald's medallion)</summary>
    public bool Framed { get => (bool)GetValue(FramedProperty); set => SetValue(FramedProperty, value); }

    public static Color RingColour(string ring) => ring switch
    {
        "silver" => Color.FromRgb(196, 202, 210),
        "bronze" => Color.FromRgb(198, 132, 84),
        "none" => Color.FromRgb(171, 137, 82),
        _ => Color.FromRgb(212, 168, 75),
    };

    static readonly Color LockedInk = Color.FromRgb(128, 66, 74);

    public IconView()
    {
        SnapsToDevicePixels = true;
        Rebuild();
    }

    void Rebuild()
    {
        var colour = Locked ? LockedInk : RingColour(Ring);
        if (Framed)
        {
            BorderThickness = new Thickness(1.5);
            BorderBrush = new SolidColorBrush(Locked ? Color.FromRgb(70, 58, 38) : Color.FromRgb(171, 137, 82));
            Background = Locked
                ? new RadialGradientBrush(Color.FromRgb(70, 22, 34), Color.FromRgb(22, 8, 13)) { RadiusX = 0.7, RadiusY = 0.7 }
                : new SolidColorBrush(Color.FromRgb(14, 12, 9));
        }
        else
        {
            BorderThickness = new Thickness(0);
            Background = Brushes.Transparent;
        }

        var icon = Icon ?? "";
        if (icon.StartsWith("file:") && Folder is { } folder && File.Exists(System.IO.Path.Combine(folder, icon[5..])))
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(System.IO.Path.Combine(folder, icon[5..]));
                image.EndInit();
                Child = new Grid
                {
                    Children =
                    {
                        new Image { Source = image, Stretch = Stretch.Uniform, Opacity = Locked ? 0.55 : 1 },
                        new Border { Background = new SolidColorBrush(Color.FromArgb(Locked ? (byte)110 : (byte)0, 90, 20, 32)) },
                    },
                };
                return;
            }
            catch (Exception) { }
        }
        if (icon.StartsWith("game:"))
        {
            Child = new Viewbox
            {
                Margin = new Thickness(6),
                Child = new TextBlock { Text = "GAME\nPICTURE", TextAlignment = TextAlignment.Center, Foreground = new SolidColorBrush(colour), FontSize = 9, FontWeight = FontWeights.SemiBold },
            };
            return;
        }
        var name = icon.StartsWith("builtin:") ? icon[8..] : "trophy";
        var geometry = Catalog.Current.IconGeometry(name) ?? Catalog.Current.IconGeometry("trophy");
        Child = new Viewbox
        {
            Margin = new Thickness(Framed ? 0 : 0),
            Stretch = Stretch.Uniform,
            Child = new Canvas
            {
                Width = 24, Height = 24, Margin = new Thickness(7),   // the icon fills 62% of its square, as in the overlay
                Children =
                {
                    new Path
                    {
                        Data = geometry, Stroke = new SolidColorBrush(colour), StrokeThickness = 1.7,
                        StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                    },
                },
            },
        };
    }
}
