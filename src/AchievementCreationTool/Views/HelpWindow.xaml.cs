using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AchievementCreationTool.Help;

namespace AchievementCreationTool.Views;

/// <summary>The guide, beside the editor: one window, brought forward at the topic asked for</summary>
public partial class HelpWindow : Window, INotifyPropertyChanged
{
    static HelpWindow? open;
    HelpTopic topic;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal HelpWindow(string id)
    {
        topic = HelpContent.Topic(id);
        InitializeComponent();
        DataContext = this;
        Render();
        Closed += (_, _) => open = null;
    }

    /// <summary>Opens the guide at a topic, or brings it forward there</summary>
    public static void ShowTopic(Window? owner, string id)
    {
        if (open is null)
        {
            open = new HelpWindow(id);
            if (owner is not null && owner.IsVisible && owner.WindowState == WindowState.Normal)
            {
                // Over the middle of the window it was asked from; OnScreen then keeps it all on that screen
                open.WindowStartupLocation = WindowStartupLocation.Manual;
                open.Left = owner.Left + (owner.ActualWidth - open.Width) / 2;
                open.Top = owner.Top + (owner.ActualHeight - open.Height) / 2;
            }
            else
                open.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            open.Show();
            Controls.OnScreen.Fit(open);
        }
        open.Topic = HelpContent.Topic(id);
        if (open.WindowState == WindowState.Minimized)
            open.WindowState = WindowState.Normal;
        open.Activate();
    }

    /// <summary>F1 on any window opens the guide at that window's topic</summary>
    public static void HookF1(Window window, string id) =>
        window.InputBindings.Add(new KeyBinding(new Command(() => ShowTopic(window, id)), Key.F1, ModifierKeys.None));

    sealed class Command(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => run();
    }

    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public HelpTopic[] Topics => HelpContent.Topics;

    public HelpTopic Topic
    {
        get => topic;
        set
        {
            if (value is null)
                return;
            topic = value;
            Raise();
            Render();
        }
    }

    void Render()
    {
        var ink = (Brush)FindResource("Ink");
        var muted = (Brush)FindResource("Muted");
        var gold = (Brush)FindResource("Gold");
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"), FontSize = 14.5, Foreground = ink, Background = Brushes.Transparent,
            PagePadding = new Thickness(24, 20, 24, 30), LineHeight = 22, TextAlignment = TextAlignment.Left,
        };
        doc.Blocks.Add(new Paragraph(new Run(topic.Title)) { FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        doc.Blocks.Add(new Paragraph(new Run(topic.Summary)) { Foreground = muted, Margin = new Thickness(0, 0, 0, 14) });

        List? list = null;
        foreach (var block in topic.Blocks)
        {
            if (block.Kind != BlockKind.Bullet)
                list = null;
            switch (block.Kind)
            {
                case BlockKind.Heading:
                    doc.Blocks.Add(new Paragraph(new Run(block.Text)) { FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = gold, Margin = new Thickness(0, 14, 0, 6) });
                    break;
                case BlockKind.Text:
                    doc.Blocks.Add(new Paragraph(new Run(block.Text)) { Margin = new Thickness(0, 0, 0, 10) });
                    break;
                case BlockKind.Bullet:
                    if (list is null)
                    {
                        list = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(22, 0, 0, 0) };
                        doc.Blocks.Add(list);
                    }
                    list.ListItems.Add(new ListItem(new Paragraph(new Run(block.Text)) { Margin = new Thickness(0, 0, 0, 4) }));
                    break;
                case BlockKind.Example:
                    doc.Blocks.Add(new Section(new Paragraph(new Run(block.Text)) { FontFamily = new FontFamily("Consolas"), FontSize = 13, Margin = new Thickness(0) })
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x15)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2E, 0x35)),
                        BorderThickness = new Thickness(1), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 2, 0, 12),
                    });
                    break;
                case BlockKind.Tip:
                    doc.Blocks.Add(new Section(new Paragraph(new Run(block.Text)) { Margin = new Thickness(0) })
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x24, 0x18)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x6E, 0x2F)),
                        BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 2, 0, 12),
                    });
                    break;
            }
        }
        Article.Document = doc;
    }
}
