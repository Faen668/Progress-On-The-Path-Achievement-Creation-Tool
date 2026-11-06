using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using AchievementCreationTool.Logic;
using AchievementCreationTool.Model;
using Microsoft.Win32;

namespace AchievementCreationTool.Views;

/// <summary>Checks the whole pack, then copies it into the game's mods folder as a mod of its own: the pack's file, its
/// pictures, and its content folder if it has one (the mod's own .w3strings); and a .zip with the same, for Nexus.</summary>
public partial class ExportWindow : Window, INotifyPropertyChanged
{
    readonly Pack pack;
    string gameFolder;
    string modFolder;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExportWindow(Pack pack)
    {
        this.pack = pack;
        gameFolder = AppSettings.FindGame() ?? "";
        modFolder = "mod" + string.Concat(pack.Id.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        foreach (var f in Checker.Check(pack).OrderBy(f => f.Severity))
            Findings.Add(f);
        InitializeComponent();
        DataContext = this;
        HelpWindow.HookF1(this, "export");
    }

    void OnHelp(object sender, RoutedEventArgs e) => HelpWindow.ShowTopic(this, (sender as FrameworkElement)?.Tag as string ?? "export");

    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ObservableCollection<Finding> Findings { get; } = new();
    int Problems => Findings.Count(f => f.IsProblem);
    public bool CanExport => Problems == 0 && Directory.Exists(Path.Combine(gameFolder, "bin")) && modFolder.Length > 3 && pack.Folder is not null;
    public string Done { get; private set; } = "";

    public string Summary => Problems > 0
        ? $"{pack.Achievements.Count} achievements: fix {Problems} problem{(Problems == 1 ? "" : "s")} before exporting. The overlay would skip what has them."
        : Findings.Count > 0
            ? $"{pack.Achievements.Count} achievements, ready to export. The warnings below are worth a look, but don't stop it."
            : $"All {pack.Achievements.Count} achievements are ready to export.";
    public Brush SummaryGround => (Brush)FindResource(Problems > 0 ? "BadGround" : "GoodGround");
    public Brush SummaryLine => new SolidColorBrush(Problems > 0 ? Color.FromRgb(0x6A, 0x30, 0x30) : Color.FromRgb(0x2D, 0x4A, 0x36));

    public string GameFolder
    {
        get => gameFolder;
        set { gameFolder = value.Trim(); Raise(); Raise(nameof(GameNote)); Raise(nameof(CanExport)); }
    }

    public string GameNote => Directory.Exists(Path.Combine(gameFolder, "bin")) ? $"The pack goes in {Path.Combine(gameFolder, "mods", modFolder)}." : "That isn't The Witcher 3's folder: it has no bin folder.";

    public string ModFolder
    {
        get => modFolder;
        set
        {
            var name = new string((value ?? "").Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
            modFolder = name.StartsWith("mod") ? name : "mod" + name;
            Raise(); Raise(nameof(Layout)); Raise(nameof(GameNote)); Raise(nameof(CanExport));
        }
    }

    public bool MakeZip
    {
        get => AppSettings.Current.MakeZip;
        set { AppSettings.Current.MakeZip = value; Raise(); }
    }

    // What goes with the pack: its file, the pictures its achievements use, and its content folder
    List<string> Files()
    {
        var files = new List<string> { PackXml.FileName };
        foreach (var a in pack.Achievements.Where(a => a.Icon.StartsWith("file:")))
            files.Add(a.Icon[5..].Replace('/', '\\'));
        if (pack.Folder is { } folder && Directory.Exists(Path.Combine(folder, "content")))
            files.AddRange(Directory.GetFiles(Path.Combine(folder, "content"), "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(folder, f)));
        return files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public string Layout
    {
        get
        {
            var text = new StringBuilder($"mods\\{modFolder}\\\n");
            foreach (var f in Files())
                text.Append("  ").Append(f).Append(f == PackXml.FileName ? $"   {pack.Achievements.Count} achievements" : "").Append('\n');
            return text.ToString().TrimEnd();
        }
    }

    void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "The Witcher 3's folder, the one with bin and content in it" };
        if (dialog.ShowDialog(this) == true)
            GameFolder = dialog.FolderName;
    }

    void OnExport(object sender, RoutedEventArgs e)
    {
        if (pack.Folder is not { } folder)
            return;
        try
        {
            var target = Path.Combine(gameFolder, "mods", modFolder);
            foreach (var f in Files())
            {
                var to = Path.Combine(target, f);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(Path.Combine(folder, f), to, true);
            }
            AppSettings.Current.GameFolder = gameFolder;
            var said = $"Exported to {target}.";
            if (MakeZip)
            {
                var zip = Path.Combine(folder, modFolder + ".zip");
                if (File.Exists(zip))
                    File.Delete(zip);
                using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);
                foreach (var f in Files())
                    archive.CreateEntryFromFile(Path.Combine(folder, f), $"mods/{modFolder}/{f.Replace('\\', '/')}");
                said += $" The zip is {zip}.";
            }
            AppSettings.Current.Save();
            Done = said + " The overlay reads it within a few seconds if the game is running.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Done = "It couldn't be exported: " + ex.Message;
        }
        Raise(nameof(Done));
    }
}
