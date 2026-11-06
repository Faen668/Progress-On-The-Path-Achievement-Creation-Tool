using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AchievementCreationTool.Data;
using AchievementCreationTool.Model;
using AchievementCreationTool.Views;

namespace AchievementCreationTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Catalog.Load(Path.Combine(AppContext.BaseDirectory, "catalog"));

        // Every window, the main one and each dialog, fits on the screen it opens on
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            if (sender is Window w)
                w.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Controls.OnScreen.Fit(w));
        }));

        var args = e.Args;

        // --roundtrip <in> <out>: reads a pack and writes it again, to check nothing's lost on the way
        var round = Array.IndexOf(args, "--roundtrip");
        if (round >= 0 && round + 2 < args.Length)
        {
            try
            {
                PackXml.Save(PackXml.Load(args[round + 1]), args[round + 2]);
            }
            catch (Exception ex)
            {
                File.WriteAllText(args[round + 2] + ".error.txt", ex.ToString());
            }
            Shutdown();
            return;
        }

        // --shots <folder> [pack]: a picture of every window, off screen, for checking how they look
        var shots = Array.IndexOf(args, "--shots");
        if (shots >= 0 && shots + 1 < args.Length)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shots(args[shots + 1], shots + 2 < args.Length ? args[shots + 2] : null);
            Shutdown();
            return;
        }

        var main = new MainWindow();
        main.Show();
        var open = args.FirstOrDefault(a => a.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        if (open is not null)
            main.OpenFile(open);
        else if (AppSettings.Current.LastPack is { } last && File.Exists(last))
            main.OpenFile(last);
        if (Catalog.Current.Problem is { } problem)
            MessageBox.Show(main, problem + "\n\nThe pickers will be empty. Reinstall the tool to put the catalog back.", "Achievement Creation Tool", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // ------------------------------------------------------------------ pictures of every window

    static void Settle()
    {
        for (var i = 0; i < 6; ++i)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
    }

    static void Save(Window window, string folder, string name)
    {
        Settle();
        // The window's own root, not just its content: the adorner layer, where placeholders are drawn, is above the content
        if ((VisualTreeHelper.GetChildrenCount(window) > 0 ? VisualTreeHelper.GetChild(window, 0) : window.Content) is not FrameworkElement content)
            return;
        var dpi = VisualTreeHelper.GetDpi(content);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY),
            96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        var ground = new DrawingVisual();
        using (var dc = ground.RenderOpen())
            dc.DrawRectangle(window.Background, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        bitmap.Render(ground);
        bitmap.Render(content);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(folder, name + ".png"));
        png.Save(file);
    }

    static void Offscreen(Window w)
    {
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = -30000;
        w.Top = 0;
        w.ShowActivated = false;
        w.ShowInTaskbar = false;
    }

    static void Shots(string folder, string? packPath)
    {
        Directory.CreateDirectory(folder);
        var main = new MainWindow();
        Offscreen(main);
        main.Show();
        Save(main, folder, "00-main-empty");
        if (packPath is not null)
            main.OpenFile(packPath);
        Catalog.QuestBlocks().Wait();
        Save(main, folder, "01-main");

        var pack = main.Pack;
        for (var i = 1; i < pack.Achievements.Count && i < 4; ++i)
        {
            main.Selected = pack.Achievements[i];
            Save(main, folder, $"02-main-{i}");
        }
        main.Height = 640;
        Save(main, folder, "03-main-short");
        main.Height = 900;

        var a = main.Selected ?? pack.Achievements.FirstOrDefault() ?? new Achievement { Id = "x" };
        void Dialog(Window w, string name, Action? before = null)
        {
            Offscreen(w);
            w.Owner = null;
            w.Show();
            before?.Invoke();
            Save(w, folder, name);
            w.Close();
        }

        Dialog(new ConditionWindow(pack, a, null), "10-condition-quest");
        var facts = new ConditionWindow(pack, a, null);
        Dialog(facts, "11-condition-fact", () => { facts.Kind = facts.Kinds.First(k => k.Kind == ConditionKind.Fact); facts.Query = "griffin"; });
        var blocks = new ConditionWindow(pack, a, null);
        Dialog(blocks, "12-condition-block", () =>
        {
            blocks.Kind = blocks.Kinds.First(k => k.Kind == ConditionKind.QuestBlock);
            Settle();
            blocks.Query = "q002";
            blocks.Picked = blocks.Rows.FirstOrDefault(r => r.Sub.Contains("Out"));
        });
        var section = new ConditionWindow(pack, a, null);
        Dialog(section, "13-condition-section", () => section.Kind = section.Kinds.First(k => k.Kind == ConditionKind.PotpSection));
        var level = new ConditionWindow(pack, a, null);
        Dialog(level, "14-condition-level", () => level.Kind = level.Kinds.First(k => k.Kind == ConditionKind.Level));
        Dialog(new IconPickerWindow(pack, a.Icon, a.Ring), "20-icons");
        Dialog(new SearchWindow("Choose the item to give", "", Catalog.Current.Items.Select(i => new PickRow(i.Title.Length > 0 ? i.Title : i.Name, i.Name, i.Name))), "21-item");
        Dialog(new PackSettingsWindow(pack), "22-pack-settings");
        Dialog(new ExportWindow(pack), "23-export");
        Dialog(new HelpWindow("conditions"), "30-help-conditions");
        Dialog(new HelpWindow("kinds"), "31-help-kinds");
        Dialog(new HelpWindow("strings"), "32-help-strings");
        var empty = new ConditionWindow(pack, a, null);
        Dialog(empty, "15-condition-empty", () => { empty.Kind = empty.Kinds.First(k => k.Kind == ConditionKind.Item); });
        var settings = new PackSettingsWindow(new Pack());
        Dialog(settings, "24-pack-settings-empty");
        main.Hide();   // not closed: closing asks about unsaved changes
    }
}
