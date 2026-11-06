using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AchievementCreationTool.Model;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // Every change to a pack goes through here, so the window can tell when it needs saving
    public static event Action? AnythingChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Raise(name);
        Changed(name);
        AnythingChanged?.Invoke();
        return true;
    }

    protected virtual void Changed(string? name) { }

    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A title or description: text written in the file, or a key into the mod's own .w3strings, or both
/// (the text then shows when no .w3strings has the key). Other languages' text, from a file made by hand, is kept.</summary>
public sealed class LocalText : Observable
{
    private string text = "";
    private string key = "";

    public string Text { get => text; set => Set(ref text, value ?? ""); }
    public string Key { get => key; set => Set(ref key, (value ?? "").Trim()); }
    public List<(string Lang, string Text)> Translations { get; } = new();

    public bool IsEmpty => Text.Length == 0 && Key.Length == 0 && Translations.Count == 0;
    public string Shown => Text.Length > 0 ? Text : Translations.Count > 0 ? Translations[0].Text : Key;

    protected override void Changed(string? name)
    {
        Raise(nameof(Shown));
        Raise(nameof(IsEmpty));
    }
}

public enum ConditionKind
{
    All, Any, AtLeast, Not,
    Fact, Quest, QuestBlock, Kills, Item, GwentCard, Level, Crowns,
    PotpEntry, PotpSection, Achievement, VanillaAchievement,
}

public sealed class Condition : Observable
{
    private ConditionKind kind;
    private string op = "ge";
    private int value = 1;
    private int n = 1;
    private string target = "";
    private int nameId;
    private string output = "";
    private string questFile = "";
    private string state = "";
    private string unit = "percent";
    private string where = "either";
    private string reference = "";
    private string label = "";

    public Condition(ConditionKind kind) => this.kind = kind;

    public ConditionKind Kind { get => kind; set { if (Set(ref kind, value)) Raise(nameof(IsGroup)); } }
    public ObservableCollection<Condition> Children { get; } = new();
    public Condition? Parent { get; set; }

    /// <summary>ge, gt, le, lt, eq or ne</summary>
    public string Op { get => op; set => Set(ref op, value); }
    public int Value { get => value; set => Set(ref this.value, value); }
    /// <summary>For atLeast: how many of the children</summary>
    public int N { get => n; set => Set(ref n, value); }
    /// <summary>The thing it's about: a fact's or item's name, a journal name, a GUID, a uuid, a section, an achievement ID</summary>
    public string Target { get => target; set => Set(ref target, value ?? ""); }
    /// <summary>For kills: the display name's string ID</summary>
    public int NameId { get => nameId; set => Set(ref nameId, value); }
    /// <summary>For a quest block: the output that counts, empty for any</summary>
    public string Output { get => output; set => Set(ref output, value ?? ""); }
    /// <summary>For a quest block: the quest file it's in, for people and checks</summary>
    public string QuestFile { get => questFile; set => Set(ref questFile, value ?? ""); }
    /// <summary>A quest's (done, failed, ended, active, started) or an entry's (completed, missed, ignored, closed)</summary>
    public string State { get => state; set => Set(ref state, value ?? ""); }
    /// <summary>A section's: percent or count</summary>
    public string Unit { get => unit; set => Set(ref unit, value); }
    /// <summary>An item's: inventory, stash or either</summary>
    public string Where { get => where; set => Set(ref where, value); }
    public string Ref { get => reference; set => Set(ref reference, (value ?? "").Trim()); }
    public string Label { get => label; set => Set(ref label, value ?? ""); }

    public bool IsGroup => Kind is ConditionKind.All or ConditionKind.Any or ConditionKind.AtLeast or ConditionKind.Not;

    /// <summary>For a group's choice in the tree: 0 all, 1 any, 2 at least, 3 none</summary>
    public int GroupIndex
    {
        get => Kind switch { ConditionKind.Any => 1, ConditionKind.AtLeast => 2, ConditionKind.Not => 3, _ => 0 };
        set
        {
            Kind = value switch { 1 => ConditionKind.Any, 2 => ConditionKind.AtLeast, 3 => ConditionKind.Not, _ => ConditionKind.All };
            Raise(nameof(GroupIndex));
            Raise(nameof(IsAtLeast));
        }
    }

    public bool IsAtLeast => Kind == ConditionKind.AtLeast;

    public bool IsCountable => Kind is ConditionKind.Fact or ConditionKind.Kills or ConditionKind.Item or ConditionKind.GwentCard
        or ConditionKind.Level or ConditionKind.Crowns or ConditionKind.PotpSection;

    public bool HasComparison => IsCountable;

    public void Add(Condition child, int at = -1)
    {
        child.Parent = this;
        if (at < 0 || at > Children.Count)
            Children.Add(child);
        else
            Children.Insert(at, child);
    }

    public IEnumerable<Condition> SelfAndDescendants()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var c in child.SelfAndDescendants())
                yield return c;
    }

    public Condition Copy()
    {
        var c = new Condition(Kind) { Op = Op, Value = Value, N = N, Target = Target, NameId = NameId, Output = Output, QuestFile = QuestFile, State = State, Unit = Unit, Where = Where, Ref = Ref, Label = Label };
        foreach (var child in Children)
            c.Add(child.Copy());
        return c;
    }

    // What the tree shows for it, in words (Logic/Describe.cs)
    public string KindName => Logic.Describe.KindName(this);
    public string Summary => Logic.Describe.Summary(this);
    public string Detail => Logic.Describe.Detail(this);

    protected override void Changed(string? name)
    {
        Raise(nameof(Summary));
        Raise(nameof(Detail));
        Raise(nameof(KindName));
    }

    // The tree refreshes a row by this, when the catalog's quest blocks have arrived
    public void Touch() => Raise(string.Empty);
}

public enum RewardKind { Crowns, Item, Experience }

public sealed class Reward : Observable
{
    private RewardKind kind;
    private int amount = 1;
    private string item = "";

    public RewardKind Kind { get => kind; set => Set(ref kind, value); }
    public int Amount { get => amount; set => Set(ref amount, value); }
    public string Item { get => item; set => Set(ref item, value ?? ""); }

    public bool IsItem => Kind == RewardKind.Item;
    public string ItemTitle => Item.Length == 0 ? "Choose an item…" : Data.Catalog.Current.ItemTitle(Item);
    public static RewardKind[] Kinds { get; } = Enum.GetValues<RewardKind>();

    protected override void Changed(string? name)
    {
        Raise(nameof(IsItem));
        Raise(nameof(ItemTitle));
    }
}

public sealed class Achievement : Observable
{
    private string id = "";
    private string icon = "builtin:trophy";
    private string ring = "gold";
    private bool hidden;
    private bool retroactive = true;
    private Condition? unlock;
    private string progressOf = "";
    private bool progressConditions;

    public string Id { get => id; set => Set(ref id, (value ?? "").Trim()); }
    public LocalText Title { get; } = new();
    public LocalText Description { get; } = new();
    /// <summary>builtin:name, game:path or file:path</summary>
    public string Icon { get => icon; set => Set(ref icon, value); }
    /// <summary>gold, silver, bronze or none</summary>
    public string Ring { get => ring; set => Set(ref ring, value); }
    public bool Hidden { get => hidden; set => Set(ref hidden, value); }
    public bool Retroactive { get => retroactive; set => Set(ref retroactive, value); }
    public Condition? Unlock { get => unlock; set => Set(ref unlock, value); }
    /// <summary>The ref of the countable condition the progress bar shows</summary>
    public string ProgressOf { get => progressOf; set => Set(ref progressOf, value ?? ""); }
    /// <summary>Or: the bar counts how many of the top group's conditions are met</summary>
    public bool ProgressConditions { get => progressConditions; set => Set(ref progressConditions, value); }
    public ObservableCollection<Reward> Rewards { get; } = new();

    public IEnumerable<Condition> AllConditions() => Unlock?.SelfAndDescendants() ?? Enumerable.Empty<Condition>();
}

public sealed class Pack : Observable
{
    private string id = "";
    private string author = "";
    private string version = "";
    private string minPotP = "";
    private string url = "";

    public string Id { get => id; set => Set(ref id, (value ?? "").Trim()); }
    public LocalText Title { get; } = new();
    public string Author { get => author; set => Set(ref author, value ?? ""); }
    public string Version { get => version; set => Set(ref version, value ?? ""); }
    public string MinPotP { get => minPotP; set => Set(ref minPotP, value ?? ""); }
    public string Url { get => url; set => Set(ref url, value ?? ""); }
    public ObservableCollection<Achievement> Achievements { get; } = new();

    /// <summary>Where it was opened from or saved to, null for a new one</summary>
    public string? FilePath { get; set; }
    public string? Folder => FilePath is null ? null : Path.GetDirectoryName(FilePath);
}
