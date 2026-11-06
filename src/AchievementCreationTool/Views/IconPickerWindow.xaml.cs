using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using AchievementCreationTool.Data;
using AchievementCreationTool.Model;
using Microsoft.Win32;

namespace AchievementCreationTool.Views;

public sealed record BuiltinIcon(string Name, string Icon);

public partial class IconPickerWindow : Window, INotifyPropertyChanged
{
    readonly Pack pack;
    readonly List<PickRow> gameAll;
    string icon;
    string ring;
    string query = "";
    string gameQuery = "";
    string gamePath = "";
    PickRow? gamePicked;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IconPickerWindow(Pack pack, string icon, string ring)
    {
        this.pack = pack;
        this.icon = icon;
        this.ring = ring;
        gameAll = Catalog.Current.Items.Where(i => i.Icon.Length > 0)
            .GroupBy(i => i.Icon).Select(g => g.First())
            .Select(i => new PickRow(i.Title.Length > 0 ? i.Title : i.Name, i.Icon, i.Icon)).ToList();
        if (icon.StartsWith("game:"))
            gamePath = icon[5..];
        InitializeComponent();
        DataContext = this;
        FilterBuiltins();
        FilterGame();
        HelpWindow.HookF1(this, "icons");
    }

    void OnHelp(object sender, RoutedEventArgs e) => HelpWindow.ShowTopic(this, (sender as FrameworkElement)?.Tag as string ?? "icons");

    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ObservableCollection<BuiltinIcon> Builtins { get; } = new();
    public ObservableCollection<PickRow> GameRows { get; } = new();
    public string? Folder => pack.Folder;
    public string OwnProblem { get; private set; } = "";

    public string Chosen { get => icon; private set { icon = value; Raise(); } }
    public string Ring { get => ring; set { ring = value; Raise(); } }

    public string Query
    {
        get => query;
        set { query = value; Raise(); FilterBuiltins(); }
    }

    public string GameQuery
    {
        get => gameQuery;
        set { gameQuery = value; Raise(); FilterGame(); }
    }

    public string GamePath
    {
        get => gamePath;
        set
        {
            gamePath = (value ?? "").Trim().Replace('\\', '/');
            Raise();
            if (gamePath.Length > 0)
                Chosen = "game:" + gamePath;
        }
    }

    public PickRow? GamePicked
    {
        get => gamePicked;
        set
        {
            gamePicked = value;
            Raise();
            if (value is not null)
                GamePath = value.Key;
        }
    }

    void FilterBuiltins()
    {
        Builtins.Clear();
        foreach (var i in Catalog.Current.Icons.Where(i => i.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)))
            Builtins.Add(new BuiltinIcon(i.Name, "builtin:" + i.Name));
    }

    void FilterGame()
    {
        GameRows.Clear();
        var words = gameQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var r in gameAll.Where(r => words.All(w => r.Title.Contains(w, StringComparison.OrdinalIgnoreCase) || r.Sub.Contains(w, StringComparison.OrdinalIgnoreCase))).Take(400))
            GameRows.Add(r);
    }

    void OnBuiltin(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BuiltinIcon b)
            Chosen = b.Icon;
    }

    void OnBrowse(object sender, RoutedEventArgs e)
    {
        if (pack.Folder is null)
        {
            OwnProblem = "Save the pack first: the picture is copied into its folder.";
            Raise(nameof(OwnProblem));
            return;
        }
        var dialog = new OpenFileDialog { Title = "Choose a picture", Filter = "Pictures|*.png;*.jpg;*.jpeg" };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            var icons = Path.Combine(pack.Folder, "icons");
            Directory.CreateDirectory(icons);
            var name = Path.GetFileName(dialog.FileName);
            var target = Path.Combine(icons, name);
            if (!string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                File.Copy(dialog.FileName, target, true);
            Chosen = "file:icons/" + name;
            OwnProblem = "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            OwnProblem = "It couldn't be copied: " + ex.Message;
        }
        Raise(nameof(OwnProblem));
    }

    void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
