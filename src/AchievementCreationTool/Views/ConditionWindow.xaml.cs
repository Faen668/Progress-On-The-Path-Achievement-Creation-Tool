using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using AchievementCreationTool.Data;
using AchievementCreationTool.Logic;
using AchievementCreationTool.Model;
using Condition = AchievementCreationTool.Model.Condition;

namespace AchievementCreationTool.Views;

public sealed record Choice(string Key, string Words);

public sealed record KindChoice(ConditionKind Kind, string Name, string Count, string About);

/// <summary>Adds a condition, or changes one: its kind on the left, what it's about picked from the catalog on the right</summary>
public partial class ConditionWindow : Window, INotifyPropertyChanged
{
    readonly Pack pack;
    readonly Achievement achievement;
    KindChoice kind;
    string query = "";
    PickRow? picked;
    List<PickRow> all = new();
    string op = "ge";
    string value = "1";
    string state = "";
    string unit = "percent";
    string where = "either";
    string output = AnyOutput;
    string typed = "";
    const string AnyOutput = "any output";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ConditionWindow(Pack pack, Achievement achievement, Condition? editing)
    {
        this.pack = pack;
        this.achievement = achievement;
        var cat = Catalog.Current;
        Kinds =
        [
            new(ConditionKind.Quest, "Quest", $"{cat.Quests.Length}", "A quest in the journal: done, failed, going on or started. Picked by its title; the file gets its journal name."),
            new(ConditionKind.PotpEntry, "PotP entry", $"{cat.Potp.Entries.Length:N0}", "One of Progress on the Path's entries: a quest, an event, a place on the map, an item, a Gwent card or a creature, as PotP tracks it."),
            new(ConditionKind.PotpSection, "PotP section", $"{cat.Potp.Sections.Length}", "One of PotP's sections, done in full or in part. It can be the progress bar: done of total."),
            new(ConditionKind.Fact, "Fact", $"{cat.Facts.Length:N0}", "A fact the quests keep, with a number. Each shows the quest files that set it and check it. A mod's own fact can be typed in."),
            new(ConditionKind.QuestBlock, "Quest block", "", "A block in the game's own quests has run, heard through Native Quest Hook. Only vanilla blocks, never Brothers In Arms ones."),
            new(ConditionKind.Kills, "Kill count", $"{cat.Kills.Length}", "How many of a creature or person Geralt has killed, as PotP's Kills tab counts them."),
            new(ConditionKind.Item, "Item owned", $"{cat.Items.Length:N0}", "How many of an item he has, in his inventory, the stash or both. A mod's own item can be typed in."),
            new(ConditionKind.GwentCard, "Gwent card", $"{cat.GwentCards.Count()}", "How many copies of a Gwent card he has."),
            new(ConditionKind.Level, "Level", "", "Geralt's level."),
            new(ConditionKind.Crowns, "Crowns", "", "The crowns he carries."),
            new(ConditionKind.Achievement, "Another achievement", $"{pack.Achievements.Count - 1}", "Another achievement in this pack is unlocked. One in another pack can be typed in as pack.id."),
            new(ConditionKind.VanillaAchievement, "Game achievement", $"{cat.Achievements.Length}", "One of the game's own achievements is unlocked, on the player's store account."),
        ];
        kind = Kinds[0];
        InitializeComponent();
        DataContext = this;
        Heading = editing is null ? "Add a condition" : "Change a condition";
        OkText = editing is null ? "Add condition" : "Change condition";

        if (editing is not null)
        {
            Kind = Kinds.FirstOrDefault(k => k.Kind == editing.Kind) ?? Kinds[0];
            op = editing.Op;
            value = editing.Value.ToString();
            state = editing.State;
            unit = editing.Unit;
            where = editing.Where;
            output = editing.Output.Length > 0 ? editing.Output : AnyOutput;
            UseForProgress = editing.Ref.Length > 0 && editing.Ref == achievement.ProgressOf;
            var key = editing.Kind == ConditionKind.Kills ? editing.NameId.ToString() : editing.Target;
            if (all.FirstOrDefault(r => r.Key == key) is { } row)
            {
                Picked = row;
                Query = row.Title;
            }
            else
                Typed = key;
        }
        else
            Kind = Kinds[0];
        Loaded += (_, _) => Search.Focus();
        HelpWindow.HookF1(this, "kinds");
    }

    void OnHelp(object sender, RoutedEventArgs e) => HelpWindow.ShowTopic(this, (sender as FrameworkElement)?.Tag as string ?? "kinds");

    public string SearchHint => kind.Kind switch
    {
        ConditionKind.Quest => "Search the quests by title, such as Beast of White Orchard",
        ConditionKind.PotpEntry => "Search PotP's entries by name or section, such as Frying Pan",
        ConditionKind.PotpSection => "Search PotP's sections, such as Main Quests or Velen",
        ConditionKind.Fact => "Search the facts by name or quest file, such as griffin or q101",
        ConditionKind.QuestBlock => "Type part of a quest file's name (q101), a block's name, or a GUID",
        ConditionKind.Kills => "Search the creatures and people, such as Griffin or Drowner",
        ConditionKind.Item => "Search the items by name or category, such as Swallow or steelsword",
        ConditionKind.GwentCard => "Search the Gwent cards, such as Foltest",
        ConditionKind.Achievement => "Search this pack's achievements",
        ConditionKind.VanillaAchievement => "Search the game's achievements, such as Lilac and Gooseberries",
        _ => "Search",
    };

    public string TypeHint => kind.Kind switch
    {
        ConditionKind.Fact => "such as mymod_quest_done",
        ConditionKind.Item or ConditionKind.GwentCard => "such as mymod_sword",
        ConditionKind.Achievement => "such as other_pack.some_achievement",
        ConditionKind.QuestBlock => "such as 6f0e3b1c-1a2b-4c3d-9e8f-0a1b2c3d4e5f",
        _ => "",
    };

    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string Heading { get; }
    public string OkText { get; }
    public KindChoice[] Kinds { get; }
    public ObservableCollection<PickRow> Rows { get; } = new();
    public ObservableCollection<string> Outputs { get; } = new();
    // WPF binds to properties, not a tuple's fields
    public Choice[] Ops { get; } = Describe.Ops.Select(o => new Choice(o.Op, o.Words)).ToArray();
    public Choice[] QuestStates { get; } = Describe.QuestStates.Select(o => new Choice(o.State, o.Words)).ToArray();
    public Choice[] EntryStates { get; } = Describe.EntryStates.Select(o => new Choice(o.State, o.Words)).ToArray();
    public Choice[] Wheres { get; } = Describe.Wheres.Select(o => new Choice(o.Where, o.Words)).ToArray();
    public bool UseForProgress { get; set; }
    public Condition? Result { get; private set; }
    public string CountLine { get; private set; } = "";
    public string Problem { get; private set; } = "";

    public KindChoice Kind
    {
        get => kind;
        set
        {
            if (value is null)
                return;
            kind = value;
            state = kind.Kind == ConditionKind.Quest ? "done" : kind.Kind == ConditionKind.PotpEntry ? "completed" : "";
            if (kind.Kind == ConditionKind.PotpSection)
                value_ = unit == "percent" ? "100" : "1";
            Raise();
            LoadRows();
            foreach (var name in new[] { nameof(HasList), nameof(HasComparison), nameof(IsQuest), nameof(IsEntry), nameof(IsSection), nameof(IsItem), nameof(IsBlock),
                         nameof(CanType), nameof(TypeLabel), nameof(CompareLabel), nameof(State), nameof(Value), nameof(SearchHint), nameof(TypeHint) })
                Raise(name);
        }
    }

    string value_ { set { this.value = value; Raise(nameof(Value)); } }

    public bool HasList => kind.Kind is not (ConditionKind.Level or ConditionKind.Crowns);
    public bool HasComparison => kind.Kind is ConditionKind.Fact or ConditionKind.Kills or ConditionKind.Item or ConditionKind.GwentCard
        or ConditionKind.Level or ConditionKind.Crowns or ConditionKind.PotpSection;
    public bool IsQuest => kind.Kind == ConditionKind.Quest;
    public bool IsEntry => kind.Kind == ConditionKind.PotpEntry;
    public bool IsSection => kind.Kind == ConditionKind.PotpSection;
    public bool IsItem => kind.Kind == ConditionKind.Item;
    public bool IsBlock => kind.Kind == ConditionKind.QuestBlock;
    public bool CanType => kind.Kind is ConditionKind.Fact or ConditionKind.Item or ConditionKind.GwentCard or ConditionKind.Achievement or ConditionKind.QuestBlock;
    public string TypeLabel => kind.Kind switch
    {
        ConditionKind.Fact => "Or type a fact's name, such as one a mod's own quest sets",
        ConditionKind.Item or ConditionKind.GwentCard => "Or type an item's name, such as one a mod adds",
        ConditionKind.Achievement => "Or type an achievement in another pack, as pack_id.achievement_id",
        ConditionKind.QuestBlock => "Or type a block's GUID",
        _ => "",
    };
    public string CompareLabel => kind.Kind switch
    {
        ConditionKind.Fact => "True when the fact is",
        ConditionKind.Kills => "True when he's killed",
        ConditionKind.Item or ConditionKind.GwentCard => "True when he has",
        ConditionKind.Level => "True when his level is",
        ConditionKind.Crowns => "True when he carries",
        ConditionKind.PotpSection => "True when it's done",
        _ => "",
    };

    public string Op { get => op; set { op = value; Raise(); } }
    public string Value { get => value; set { this.value = value; Raise(); } }
    public string State { get => state; set { state = value ?? ""; Raise(); } }
    public string Where { get => where; set { where = value; Raise(); } }
    public string Output { get => output; set { output = value ?? AnyOutput; Raise(); } }

    public string Unit
    {
        get => unit;
        set
        {
            if (unit == value)
                return;
            unit = value;
            this.value = unit == "percent" ? "100" : "1";
            Raise();
            Raise(nameof(Value));
        }
    }

    public string Typed
    {
        get => typed;
        set
        {
            typed = value ?? "";
            Raise();
            if (typed.Length > 0 && picked is not null)
                Picked = null;
        }
    }

    public PickRow? Picked
    {
        get => picked;
        set
        {
            picked = value;
            Raise();
            Outputs.Clear();
            if (value?.Data is BlockInfo block)
            {
                Outputs.Add(AnyOutput);
                foreach (var o in block.Outputs ?? [])
                    Outputs.Add(o);
                if (!Outputs.Contains(output))
                    output = AnyOutput;
                Raise(nameof(Output));
            }
        }
    }

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

    // ------------------------------------------------------------------ the list for each kind

    static string Stem(string path) => Path.GetFileNameWithoutExtension(path);

    static string Files(string[] files, string verb)
    {
        if (files.Length == 0)
            return "";
        var first = string.Join(", ", files.Take(2).Select(Stem));
        return $"{verb} {first}{(files.Length > 2 ? $" and {files.Length - 2} more" : "")}";
    }

    void LoadRows()
    {
        var cat = Catalog.Current;
        all = kind.Kind switch
        {
            ConditionKind.Quest => cat.Quests.Select(q => new PickRow(q.Title.Length > 0 ? q.Title : q.Journal, $"{q.Journal} · {q.Type}{(q.Dlc != "base" ? " · " + q.Dlc : "")}", q.Journal)).ToList(),
            ConditionKind.PotpEntry => cat.Potp.Entries.Select(e => new PickRow(e.Title.Length > 0 ? e.Title : e.Uuid,
                $"{cat.Section(e.Section)?.Title ?? e.Section} · {e.Kind} · {e.Uuid}", e.Uuid)).ToList(),
            ConditionKind.PotpSection => cat.Potp.Sections.Select(s => new PickRow(s.Title.Length > 0 ? s.Title : s.Id, $"{s.Book} · {s.Id}", s.Id)).ToList(),
            ConditionKind.Fact => cat.Facts.Select(f => new PickRow(f.Name,
                string.Join(" · ", new[] { Files(f.SetIn, "set in"), Files(f.CheckedIn, "checked in") }.Where(t => t.Length > 0)), f.Name)).ToList(),
            ConditionKind.Kills => cat.Kills.Select(k => new PickRow(k.Title, $"name ID {k.NameId}{(k.Unique ? " · someone in particular" : "")}", k.NameId.ToString())).ToList(),
            ConditionKind.Item => cat.Items.Where(i => i.Category != "gwint").Select(i => new PickRow(i.Title.Length > 0 ? i.Title : i.Name,
                $"{i.Name} · {i.Category}{(i.Dlc != "base" ? " · " + i.Dlc : "")}", i.Name)).ToList(),
            ConditionKind.GwentCard => cat.GwentCards.Select(i => new PickRow(i.Title.Length > 0 ? i.Title : i.Name, i.Name, i.Name)).ToList(),
            ConditionKind.Achievement => pack.Achievements.Where(a => a != achievement).Select(a => new PickRow(a.Title.Shown, a.Id, a.Id)).ToList(),
            ConditionKind.VanillaAchievement => cat.Achievements.Select(a => new PickRow(a.Title, $"{a.Description} · {a.Name}", a.Name)).ToList(),
            ConditionKind.QuestBlock => BlockRows(),
            _ => new List<PickRow>(),
        };
        query = "";
        Raise(nameof(Query));
        Filter();
    }

    List<PickRow> BlockRows()
    {
        var task = Catalog.QuestBlocks();
        if (!task.IsCompleted)
        {
            task.ContinueWith(_ => Dispatcher.BeginInvoke(() =>
            {
                if (kind.Kind == ConditionKind.QuestBlock)
                    LoadRows();
            }));
            return new List<PickRow>();
        }
        var rows = new List<PickRow>();
        foreach (var file in task.Result)
        {
            var stem = Stem(file.File);
            foreach (var b in file.Blocks)
            {
                var name = b.Name ?? b.Comment ?? b.Kind.Replace("CQuest", "").Replace("Block", " block");
                var outs = b.Outputs is { Length: > 0 } o ? " · outputs " + string.Join(", ", o) : "";
                rows.Add(new PickRow(name, $"{stem} · {b.Kind}{outs} · {b.Guid}", b.Guid, b));
            }
        }
        return rows;
    }

    void Filter()
    {
        Rows.Clear();
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (kind.Kind == ConditionKind.QuestBlock)
        {
            if (!Catalog.QuestBlocksReady)
                CountLine = "Reading the game's quest blocks...";
            else if (query.Length < 3)
                CountLine = $"{all.Count:N0} blocks: type part of a quest file's name (q101), a block's name, or a GUID";
            if (!Catalog.QuestBlocksReady || query.Length < 3)
            {
                Raise(nameof(CountLine));
                return;
            }
        }
        var found = words.Length == 0 ? all : all.Where(r => words.All(w => r.Title.Contains(w, StringComparison.OrdinalIgnoreCase)
            || r.Sub.Contains(w, StringComparison.OrdinalIgnoreCase) || r.Key.Contains(w, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var r in found.Take(400))
            Rows.Add(r);
        if (picked is not null && !Rows.Contains(picked) && all.Contains(picked))
            Rows.Insert(0, picked);
        CountLine = found.Count > 400 ? $"{found.Count:N0} match: the first 400 are shown, so type a little more" : $"{found.Count:N0} of {all.Count:N0}";
        Raise(nameof(CountLine));
    }

    // ------------------------------------------------------------------ done

    void OnOk(object sender, RoutedEventArgs e)
    {
        var key = picked?.Key ?? typed.Trim();
        if (HasList && key.Length == 0)
        {
            Say("Choose one from the list first.");
            return;
        }
        var c = new Condition(kind.Kind) { Op = op, State = state, Unit = unit, Where = where };
        if (HasComparison)
        {
            if (!int.TryParse(value.Replace(",", ""), out var number))
            {
                Say("The number isn't a whole number.");
                return;
            }
            c.Value = number;
        }
        switch (kind.Kind)
        {
            case ConditionKind.Kills:
                if (!int.TryParse(key, out var id))
                {
                    Say("Choose a creature from the list.");
                    return;
                }
                c.NameId = id;
                break;
            case ConditionKind.QuestBlock:
                c.Target = key.ToLowerInvariant();
                if (picked?.Data is BlockInfo)
                {
                    var found = Catalog.Block(c.Target);
                    c.QuestFile = found?.File.File ?? "";
                }
                c.Output = output == AnyOutput ? "" : output;
                break;
            case ConditionKind.Level:
            case ConditionKind.Crowns:
                break;
            default:
                c.Target = key;
                break;
        }
        Result = c;
        DialogResult = true;
    }

    void Say(string text)
    {
        Problem = text;
        Raise(nameof(Problem));
    }
}
