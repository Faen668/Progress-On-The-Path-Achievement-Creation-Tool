using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace AchievementCreationTool.Views;

/// <summary>One thing to pick: what it's called, a line about it, and what's written to the file for it</summary>
public sealed record PickRow(string Title, string Sub, string Key, object? Data = null);

/// <summary>Finds one thing in a long list by typing part of its name: items, mostly</summary>
public partial class SearchWindow : Window, INotifyPropertyChanged
{
    readonly List<PickRow> all;
    string query = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public SearchWindow(string heading, string hint, IEnumerable<PickRow> rows, string? current = null)
    {
        Heading = heading;
        Hint = hint.Length > 0 ? hint : "Search";
        all = rows.ToList();
        InitializeComponent();
        DataContext = this;
        Filter();
        if (current is { Length: > 0 } && all.FirstOrDefault(r => r.Key == current) is { } now)
        {
            Results.SelectedItem = Rows.Contains(now) ? now : null;
            Results.ScrollIntoView(now);
        }
        Loaded += (_, _) => Search.Focus();
    }

    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string Heading { get; }
    public string Hint { get; }
    public ObservableCollection<PickRow> Rows { get; } = new();
    public PickRow? Picked { get; private set; }
    public string CountLine { get; private set; } = "";

    public string Query
    {
        get => query;
        set
        {
            query = value;
            Raise();
            Filter();
        }
    }

    void Filter()
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var found = all.Where(r => words.All(w => r.Title.Contains(w, StringComparison.OrdinalIgnoreCase) || r.Sub.Contains(w, StringComparison.OrdinalIgnoreCase)
            || r.Key.Contains(w, StringComparison.OrdinalIgnoreCase))).ToList();
        Rows.Clear();
        foreach (var r in found.Take(400))
            Rows.Add(r);
        CountLine = found.Count > 400 ? $"{found.Count:N0} match: the first 400 are shown, so type a little more" : $"{found.Count:N0} of {all.Count:N0}";
        Raise(nameof(CountLine));
    }

    void OnChoose(object sender, RoutedEventArgs e)
    {
        if (Results.SelectedItem is PickRow row)
        {
            Picked = row;
            DialogResult = true;
        }
    }
}
