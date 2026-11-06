using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AchievementCreationTool.Data;
using AchievementCreationTool.Logic;
using AchievementCreationTool.Model;
using Condition = AchievementCreationTool.Model.Condition;
using Microsoft.Win32;

namespace AchievementCreationTool.Views;

/// <summary>One choice of what the progress bar counts</summary>
public sealed record ProgressChoice(string Label, Condition? Of, bool Conditions);

public partial class MainWindow : Window, INotifyPropertyChanged
{
    Pack pack = NewPack();
    Achievement? selected;
    Condition? selectedCondition;
    ProgressChoice? selectedProgress;
    bool dirty;
    bool refreshQueued;
    bool settingProgress;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Observable.AnythingChanged += () =>
        {
            dirty = true;
            QueueRefresh();
        };
        Catalog.QuestBlocks().ContinueWith(_ => Dispatcher.BeginInvoke(() =>
        {
            foreach (var a in pack.Achievements)
                foreach (var c in a.AllConditions())
                    c.Touch();
            QueueRefresh();
        }));
        Closing += OnClosing;
        HelpWindow.HookF1(this, "start");
        Refresh();
    }

    void OnHelp(object sender, RoutedEventArgs e) => HelpWindow.ShowTopic(this, (sender as FrameworkElement)?.Tag as string ?? "start");

    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ------------------------------------------------------------------ what the window shows

    public Pack Pack
    {
        get => pack;
        private set
        {
            pack = value;
            Raise();
            Selected = pack.Achievements.FirstOrDefault();
            dirty = false;
            Refresh();
        }
    }

    public Achievement? Selected
    {
        get => selected;
        set
        {
            selected = value;
            selectedCondition = null;
            Raise();
            Refresh();
        }
    }

    public ObservableCollection<Condition> RootConditions { get; } = new();
    public ObservableCollection<Finding> Findings { get; } = new();
    public ObservableCollection<ProgressChoice> ProgressChoices { get; } = new();

    public ProgressChoice? SelectedProgress
    {
        get => selectedProgress;
        set
        {
            selectedProgress = value;
            Raise();
            if (settingProgress || selected is null || value is null)
                return;
            if (value.Of is { } of)
            {
                if (of.Ref.Length == 0)
                    of.Ref = NewRef(selected);
                selected.ProgressOf = of.Ref;
                selected.ProgressConditions = false;
            }
            else
            {
                selected.ProgressOf = "";
                selected.ProgressConditions = value.Conditions;
            }
        }
    }

    public string WindowTitle => $"{(dirty ? "* " : "")}{PackTitle} - Progress on the Path: Achievement Creation Tool";
    public string PackTitle => pack.Title.Shown.Length > 0 ? pack.Title.Shown : pack.Id.Length > 0 ? pack.Id : "New pack";
    public string PackLine => $"Progress on the Path · {PackTitle} · {pack.Achievements.Count} achievement{(pack.Achievements.Count == 1 ? "" : "s")}{(pack.FilePath is null ? " · not saved yet" : dirty ? " · not saved" : " · saved")}";
    public string StatusLine => (pack.FilePath ?? "Not saved yet") + "   ·   " + CatalogLine;
    public string FullId => selected is null ? "" : $"Its full ID: {(pack.Id.Length > 0 ? pack.Id : "pack")}.{selected.Id}, which other packs use";
    public string PreviewTitle => selected is null ? "" : selected.Hidden ? "Hidden Achievement" : selected.Title.Shown;
    public string PreviewIcon => selected is null ? "builtin:trophy" : selected.Hidden ? "builtin:medal" : selected.Icon;
    public string PreviewLockedLine => selected is null ? "" : selected.Hidden ? "Unlock it to see what it asks." : ProgressLine() ?? selected.Description.Shown;
    public string PreviewDate => "Unlocked " + DateTime.Now.ToString("d MMM yyyy");
    public string PreviewReward => selected is null || selected.Rewards.Count == 0 ? "" : "Reward: " + Describe.Rewards(selected);
    public bool SelectionIsClean => selected is not null && Findings.Count == 0;
    public string PackCheckLine { get; private set; } = "";

    static string CatalogLine
    {
        get
        {
            var c = Catalog.Current;
            return c.Problem ?? $"Catalog: {c.Quests.Length} quests, {c.Facts.Length:N0} facts, {c.Items.Length:N0} items, {c.Kills.Length} creatures, {c.Potp.Entries.Length:N0} PotP entries, {c.Icons.Length} icons";
        }
    }

    string? ProgressLine()
    {
        if (selected is null)
            return null;
        if (selected.ProgressConditions && selected.Unlock is { IsGroup: true } top)
        {
            var target = top.Kind == ConditionKind.AtLeast ? top.N : top.Kind == ConditionKind.Any ? 1 : top.Children.Count;
            return $"0 of {target} on this save";
        }
        var of = selected.AllConditions().FirstOrDefault(c => c.Ref.Length > 0 && c.Ref == selected.ProgressOf);
        if (of is null)
            return null;
        if (of.Kind == ConditionKind.PotpSection && of.Unit == "percent")
            return "0 of the section's entries on this save";
        return $"0 of {of.Value:N0} on this save";
    }

    // Everything that follows from the pack: put off to the end of the change, so typing stays quick
    void QueueRefresh()
    {
        if (refreshQueued)
            return;
        refreshQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            refreshQueued = false;
            Refresh();
        });
    }

    void Refresh()
    {
        // The tree
        var root = selected?.Unlock;
        if (RootConditions.Count != (root is null ? 0 : 1) || (root is not null && RootConditions[0] != root))
        {
            RootConditions.Clear();
            if (root is not null)
                RootConditions.Add(root);
        }

        // The progress bar's choices
        settingProgress = true;
        ProgressChoices.Clear();
        ProgressChoices.Add(new ProgressChoice("No progress bar", null, false));
        if (root is { IsGroup: true, Kind: not ConditionKind.Not })
            ProgressChoices.Add(new ProgressChoice("How many of its conditions are met", null, true));
        if (selected is not null)
            foreach (var c in selected.AllConditions().Where(c => c.IsCountable))
                ProgressChoices.Add(new ProgressChoice($"{Describe.KindName(c)}: {Describe.Summary(c)} ({Describe.Detail(c)})", c, false));
        SelectedProgress = selected is null ? null
            : selected.ProgressConditions ? ProgressChoices.FirstOrDefault(p => p.Conditions)
            : ProgressChoices.FirstOrDefault(p => p.Of is not null && p.Of.Ref == selected.ProgressOf && selected.ProgressOf.Length > 0) ?? ProgressChoices[0];
        settingProgress = false;

        // The checks
        var all = Checker.Check(pack);
        Findings.Clear();
        foreach (var f in all.Where(f => f.Achievement is not null && f.Achievement == selected).OrderBy(f => f.Severity))
            Findings.Add(f);
        var problems = all.Count(f => f.IsProblem);
        var warnings = all.Count - problems;
        PackCheckLine = problems == 0 && warnings == 0 ? "The whole pack is ready to export."
            : $"The whole pack: {problems} problem{(problems == 1 ? "" : "s")} to fix, {warnings} warning{(warnings == 1 ? "" : "s")}. Check and export lists them all.";

        foreach (var name in new[] { nameof(WindowTitle), nameof(PackTitle), nameof(PackLine), nameof(StatusLine), nameof(FullId), nameof(PreviewTitle), nameof(PreviewIcon),
                     nameof(PreviewLockedLine), nameof(PreviewDate), nameof(PreviewReward), nameof(SelectionIsClean), nameof(PackCheckLine) })
            Raise(name);
    }

    // ------------------------------------------------------------------ the pack

    static Pack NewPack()
    {
        var p = new Pack { Id = "my_pack" };
        p.Title.Text = "My Achievements";
        return p;
    }

    bool OkToLeave()
    {
        if (!dirty)
            return true;
        var answer = MessageBox.Show(this, $"Save the changes to {PackTitle} first?", "Achievement Creation Tool", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer == MessageBoxResult.No || (answer == MessageBoxResult.Yes && Save(false));
    }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!OkToLeave())
            e.Cancel = true;
        else
            AppSettings.Current.Save();
    }

    void OnNew(object sender, RoutedEventArgs e)
    {
        if (OkToLeave())
            Pack = NewPack();
    }

    void OnOpen(object sender, RoutedEventArgs e)
    {
        if (!OkToLeave())
            return;
        var dialog = new OpenFileDialog { Title = "Open an achievements pack", Filter = "Achievements pack|potp_achievements.xml|Any XML file|*.xml" };
        if (dialog.ShowDialog(this) == true)
            OpenFile(dialog.FileName);
    }

    public void OpenFile(string path)
    {
        try
        {
            Pack = PackXml.Load(path);
            AppSettings.Current.LastPack = path;
        }
        catch (Exception ex) when (ex is PackXml.ReadProblem or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"{path} couldn't be opened.\n\n{ex.Message}", "Achievement Creation Tool", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void OnSave(object sender, RoutedEventArgs e) => Save(false);
    void OnSaveAs(object sender, RoutedEventArgs e) => Save(true);

    bool Save(bool choose)
    {
        var path = pack.FilePath;
        if (choose || path is null)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save the pack: its folder becomes the mod's folder, with its pictures beside it",
                FileName = PackXml.FileName,
                Filter = "Achievements pack|potp_achievements.xml",
            };
            if (dialog.ShowDialog(this) != true)
                return false;
            path = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, PackXml.FileName);
        }
        try
        {
            foreach (var a in pack.Achievements)
                foreach (var c in a.AllConditions())
                    c.Label = Describe.LabelFor(c);
            PackXml.Save(pack, path);
            AppSettings.Current.LastPack = path;
            dirty = false;
            Raise(nameof(Pack));
            Refresh();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"It couldn't be saved to {path}.\n\n{ex.Message}", "Achievement Creation Tool", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    void OnPackSettings(object sender, RoutedEventArgs e)
    {
        new PackSettingsWindow(pack) { Owner = this }.ShowDialog();
        Refresh();
    }

    void OnExport(object sender, RoutedEventArgs e)
    {
        if (pack.FilePath is null || dirty)
        {
            if (MessageBox.Show(this, "The pack is saved first, as export copies it from its folder.", "Achievement Creation Tool", MessageBoxButton.OKCancel) != MessageBoxResult.OK || !Save(false))
                return;
        }
        new ExportWindow(pack) { Owner = this }.ShowDialog();
    }

    // ------------------------------------------------------------------ achievements

    void OnAddAchievement(object sender, RoutedEventArgs e)
    {
        var n = pack.Achievements.Count + 1;
        while (pack.Achievements.Any(a => a.Id == $"achievement_{n}"))
            n++;
        var a = new Achievement { Id = $"achievement_{n}" };
        a.Title.Text = "New Achievement";
        pack.Achievements.Insert(selected is null ? pack.Achievements.Count : pack.Achievements.IndexOf(selected) + 1, a);
        dirty = true;
        Selected = a;
    }

    void OnDuplicate(object sender, RoutedEventArgs e)
    {
        if (selected is null)
            return;
        var copy = new Achievement { Id = selected.Id + "_copy", Icon = selected.Icon, Ring = selected.Ring, Hidden = selected.Hidden, Retroactive = selected.Retroactive,
            Unlock = selected.Unlock?.Copy(), ProgressOf = selected.ProgressOf, ProgressConditions = selected.ProgressConditions };
        copy.Title.Text = selected.Title.Text + " (copy)";
        copy.Title.Key = selected.Title.Key;
        copy.Description.Text = selected.Description.Text;
        copy.Description.Key = selected.Description.Key;
        foreach (var r in selected.Rewards)
            copy.Rewards.Add(new Reward { Kind = r.Kind, Amount = r.Amount, Item = r.Item });
        pack.Achievements.Insert(pack.Achievements.IndexOf(selected) + 1, copy);
        dirty = true;
        Selected = copy;
    }

    void OnDeleteAchievement(object sender, RoutedEventArgs e)
    {
        if (selected is null)
            return;
        if (MessageBox.Show(this, $"Delete {selected.Title.Shown}?", "Achievement Creation Tool", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        var at = pack.Achievements.IndexOf(selected);
        pack.Achievements.Remove(selected);
        dirty = true;
        Selected = pack.Achievements.Count == 0 ? null : pack.Achievements[Math.Min(at, pack.Achievements.Count - 1)];
    }

    void OnAchievementUp(object sender, RoutedEventArgs e) => MoveAchievement(-1);
    void OnAchievementDown(object sender, RoutedEventArgs e) => MoveAchievement(1);

    void MoveAchievement(int by)
    {
        if (selected is null)
            return;
        var at = pack.Achievements.IndexOf(selected);
        var to = at + by;
        if (to < 0 || to >= pack.Achievements.Count)
            return;
        var keep = selected;
        pack.Achievements.Move(at, to);
        dirty = true;
        AchievementList.SelectedItem = keep;
        Refresh();
    }

    void OnChangeIcon(object sender, RoutedEventArgs e)
    {
        if (selected is null)
            return;
        var picker = new IconPickerWindow(pack, selected.Icon, selected.Ring) { Owner = this };
        if (picker.ShowDialog() == true)
        {
            selected.Icon = picker.Chosen;
            selected.Ring = picker.Ring;
        }
    }

    // ------------------------------------------------------------------ conditions

    void OnConditionSelected(object sender, RoutedPropertyChangedEventArgs<object> e) => selectedCondition = e.NewValue as Condition;

    void OnConditionDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (selectedCondition is { IsGroup: false })
            OnEditCondition(sender, e);
    }

    // Where a new condition goes: into the selected group, or the group the selected one is in
    Condition? TargetGroup() => selectedCondition switch
    {
        { IsGroup: true } g => g,
        { Parent: { } p } => p,
        _ => selected?.Unlock is { IsGroup: true } root ? root : null,
    };

    void Insert(Condition added)
    {
        if (selected is null)
            return;
        if (selected.Unlock is null)
            selected.Unlock = added;
        else if (TargetGroup() is { } group)
            group.Add(added);
        else
        {
            // One condition there already: both go into an "All of these" group
            var all = new Condition(ConditionKind.All);
            all.Add(selected.Unlock);
            all.Add(added);
            selected.Unlock = all;
        }
        selectedCondition = added;
        dirty = true;
        Refresh();
    }

    void OnAddCondition(object sender, RoutedEventArgs e)
    {
        if (selected is null)
            return;
        var window = new ConditionWindow(pack, selected, null) { Owner = this };
        if (window.ShowDialog() == true && window.Result is { } c)
        {
            Insert(c);
            if (window.UseForProgress)
            {
                if (c.Ref.Length == 0)
                    c.Ref = NewRef(selected);
                selected.ProgressOf = c.Ref;
                selected.ProgressConditions = false;
            }
        }
    }

    void OnAddGroup(object sender, RoutedEventArgs e) => Insert(new Condition(ConditionKind.All));

    void OnEditCondition(object sender, RoutedEventArgs e)
    {
        if (selected is null || selectedCondition is not { IsGroup: false } old)
            return;
        var window = new ConditionWindow(pack, selected, old) { Owner = this };
        if (window.ShowDialog() != true || window.Result is not { } c)
            return;
        c.Ref = old.Ref;
        if (old.Parent is { } parent)
        {
            var at = parent.Children.IndexOf(old);
            parent.Children.RemoveAt(at);
            parent.Add(c, at);
        }
        else
            selected.Unlock = c;
        if (window.UseForProgress)
        {
            if (c.Ref.Length == 0)
                c.Ref = NewRef(selected);
            selected.ProgressOf = c.Ref;
            selected.ProgressConditions = false;
        }
        selectedCondition = c;
        Refresh();
    }

    void OnRemoveCondition(object sender, RoutedEventArgs e)
    {
        if (selected is null || selectedCondition is not { } c)
            return;
        if (c.Parent is { } parent)
            parent.Children.Remove(c);
        else
            selected.Unlock = null;
        if (c.SelfAndDescendants().Any(x => x.Ref.Length > 0 && x.Ref == selected.ProgressOf))
            selected.ProgressOf = "";
        selectedCondition = null;
        dirty = true;
        Refresh();
    }

    void OnConditionUp(object sender, RoutedEventArgs e) => MoveCondition(-1);
    void OnConditionDown(object sender, RoutedEventArgs e) => MoveCondition(1);

    void MoveCondition(int by)
    {
        if (selectedCondition is not { Parent: { } parent } c)
            return;
        var at = parent.Children.IndexOf(c);
        var to = at + by;
        if (to >= 0 && to < parent.Children.Count)
        {
            parent.Children.Move(at, to);
            dirty = true;
            Refresh();
        }
    }

    static string NewRef(Achievement a)
    {
        var n = 1;
        while (a.AllConditions().Any(c => c.Ref == $"count{n}"))
            n++;
        return $"count{n}";
    }

    // ------------------------------------------------------------------ the reward

    void OnAddReward(object sender, RoutedEventArgs e)
    {
        if (selected is null)
            return;
        if (selected.Rewards.Count >= 10)
        {
            MessageBox.Show(this, "A reward can have up to 10 parts.", "Achievement Creation Tool");
            return;
        }
        selected.Rewards.Add(new Reward { Kind = RewardKind.Crowns, Amount = 100 });
        dirty = true;
        Refresh();
    }

    void OnRemoveReward(object sender, RoutedEventArgs e)
    {
        if (selected is not null && (sender as FrameworkElement)?.DataContext is Reward r)
        {
            selected.Rewards.Remove(r);
            dirty = true;
            Refresh();
        }
    }

    void OnPickRewardItem(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Reward r)
            return;
        var rows = Catalog.Current.Items.Select(i => new PickRow(i.Title.Length > 0 ? i.Title : i.Name, $"{i.Name} · {i.Category}{(i.Dlc != "base" ? " · " + i.Dlc : "")}", i.Name));
        var picker = new SearchWindow("Choose the item to give", "Search the game's items by name or category", rows, r.Item) { Owner = this };
        if (picker.ShowDialog() == true && picker.Picked is { } row)
            r.Item = row.Key;
    }
}
