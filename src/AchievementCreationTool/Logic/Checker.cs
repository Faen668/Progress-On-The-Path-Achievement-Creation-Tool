using System.IO;
using System.Text.RegularExpressions;
using AchievementCreationTool.Data;
using AchievementCreationTool.Model;

namespace AchievementCreationTool.Logic;

public enum Severity { Problem, Warning }

/// <summary>Something to fix before export (a problem: the overlay would skip it) or to look at (a warning: it works,
/// but may not do what's meant, such as a fact no vanilla quest sets, which a mod's own quests might)</summary>
public sealed record Finding(Severity Severity, string Text, Achievement? Achievement = null, Condition? Condition = null)
{
    public bool IsProblem => Severity == Severity.Problem;
}

public static partial class Checker
{
    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$")]
    private static partial Regex GuidPattern();

    [GeneratedRegex("^[A-Za-z0-9_.\\-]{1,128}$")]
    private static partial Regex KeyPattern();

    public static bool ValidId(string id, int longest) => id.Length > 0 && id.Length <= longest && IdPattern().IsMatch(id);

    public static List<Finding> Check(Pack pack)
    {
        var found = new List<Finding>();
        void Pack(Severity s, string text) => found.Add(new Finding(s, text));

        if (!ValidId(pack.Id, 32))
            Pack(Severity.Problem, "The pack needs an ID: lower case letters, digits and _, starting with a letter, up to 32 characters (Pack settings).");
        if (pack.Title.IsEmpty)
            Pack(Severity.Warning, "The pack has no title: the tab will show its ID instead (Pack settings).");
        if (pack.Title.Key.Length > 0 && !KeyPattern().IsMatch(pack.Title.Key))
            Pack(Severity.Problem, "The pack title's string key can only have letters, digits, _, . and -.");
        if (pack.Achievements.Count == 0)
            Pack(Severity.Problem, "The pack has no achievements yet.");
        if (pack.Achievements.Count > 500)
            Pack(Severity.Problem, "A pack can have up to 500 achievements.");

        var ids = pack.Achievements.GroupBy(a => a.Id).Where(g => g.Key.Length > 0 && g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var a in pack.Achievements)
            found.AddRange(Check(pack, a, ids.Contains(a.Id)));
        return found;
    }

    public static List<Finding> Check(Pack pack, Achievement a, bool duplicate = false)
    {
        var found = new List<Finding>();
        var name = a.Title.Shown.Length > 0 ? a.Title.Shown : a.Id.Length > 0 ? a.Id : "An achievement";
        void Add(Severity s, string text, Condition? c = null) => found.Add(new Finding(s, $"{name}: {text}", a, c));

        if (!ValidId(a.Id, 48))
            Add(Severity.Problem, "its ID must be lower case letters, digits and _, starting with a letter, up to 48 characters.");
        if (duplicate)
            Add(Severity.Problem, "another achievement in the pack has the same ID.");
        if (a.Title.Text.Length == 0 && a.Title.Key.Length == 0 && a.Title.Translations.Count == 0)
            Add(Severity.Problem, "it needs a title, or a string key for one.");
        if (a.Title.Text.Length > 80)
            Add(Severity.Problem, "its title is longer than 80 characters.");
        if (a.Description.Text.Length > 300)
            Add(Severity.Problem, "its description is longer than 300 characters.");
        if (a.Description.IsEmpty)
            Add(Severity.Warning, "it has no description, so players won't know what it asks.");
        foreach (var key in new[] { a.Title.Key, a.Description.Key })
            if (key.Length > 0 && !KeyPattern().IsMatch(key))
                Add(Severity.Problem, $"the string key \"{key}\" can only have letters, digits, _, . and -.");

        // The icon
        if (a.Icon.StartsWith("builtin:"))
        {
            if (Catalog.Current.IconGeometry(a.Icon[8..]) is null)
                Add(Severity.Problem, $"PotP has no icon called {a.Icon[8..]}.");
        }
        else if (a.Icon.StartsWith("file:"))
        {
            var rel = a.Icon[5..];
            if (rel.Contains(".."))
                Add(Severity.Problem, "its picture must be inside the pack's folder.");
            else if (pack.Folder is null)
                Add(Severity.Warning, "save the pack first, so its picture can be found beside it.");
            else if (!File.Exists(Path.Combine(pack.Folder, rel)))
                Add(Severity.Problem, $"its picture {rel} isn't in the pack's folder.");
        }
        else if (!a.Icon.StartsWith("game:"))
            Add(Severity.Problem, "its icon must be one of PotP's, a picture from the game, or your own.");

        // The conditions
        if (a.Unlock is null)
            Add(Severity.Problem, "it has no conditions yet, so it could never unlock.");
        else
        {
            var refs = new HashSet<string>();
            foreach (var c in a.AllConditions())
                CheckCondition(pack, a, c, refs, Add);
        }

        // The progress bar
        if (a.ProgressOf.Length > 0)
        {
            var of = a.AllConditions().FirstOrDefault(c => c.Ref == a.ProgressOf);
            if (of is null || !of.IsCountable)
                Add(Severity.Problem, "its progress bar counts a condition that isn't there any more: choose another.");
        }

        // The reward
        if (a.Rewards.Count > 10)
            Add(Severity.Problem, "a reward can have up to 10 parts.");
        foreach (var r in a.Rewards)
        {
            if (r.Kind == RewardKind.Item)
            {
                if (r.Item.Length == 0)
                    Add(Severity.Problem, "one of its rewards has no item chosen.");
                else if (Catalog.Current.Item(r.Item) is null)
                    Add(Severity.Warning, $"the game has no item called {r.Item} (a mod's own item works if that mod is installed).");
                if (r.Amount is < 1 or > 100)
                    Add(Severity.Problem, "an item reward gives 1 to 100 of it.");
            }
            else if (r.Amount is < 1 or > 100000)
                Add(Severity.Problem, $"its {(r.Kind == RewardKind.Crowns ? "crowns" : "experience")} reward must be 1 to 100,000.");
        }
        return found;
    }

    static void CheckCondition(Pack pack, Achievement a, Condition c, HashSet<string> refs, Action<Severity, string, Condition?> add)
    {
        var cat = Catalog.Current;
        void Add(Severity s, string text) => add(s, text, c);
        var what = $"\"{Describe.Summary(c)}\"";

        if (c.Ref.Length > 0 && !refs.Add(c.Ref))
            Add(Severity.Problem, $"two of its conditions have the name {c.Ref}.");

        switch (c.Kind)
        {
            case ConditionKind.All:
            case ConditionKind.Any:
                if (c.Children.Count == 0)
                    Add(Severity.Problem, $"a \"{Describe.KindName(c)}\" group is empty.");
                break;
            case ConditionKind.AtLeast:
                if (c.Children.Count == 0)
                    Add(Severity.Problem, "an \"At least\" group is empty.");
                else if (c.N < 1 || c.N > c.Children.Count)
                    Add(Severity.Problem, $"an \"At least\" group asks for {c.N} of only {c.Children.Count}.");
                break;
            case ConditionKind.Not:
                if (c.Children.Count != 1)
                    Add(Severity.Problem, "a \"None of this\" group holds exactly one condition.");
                else if (a.Retroactive)
                    Add(Severity.Warning, "a \"None of this\" group is met at the start of every game, so with \"Unlock on saves that already qualify\" it may unlock at once.");
                break;
            case ConditionKind.Fact:
                if (c.Target.Length == 0)
                    Add(Severity.Problem, "a fact condition has no fact chosen.");
                else if (cat.Fact(c.Target) is null)
                    Add(Severity.Warning, $"no vanilla quest uses the fact {c.Target} (fine if a mod's own quest sets it).");
                else if (cat.Fact(c.Target) is { SetIn.Length: 0 })
                    Add(Severity.Warning, $"the vanilla quests only check the fact {c.Target}, they never set it.");
                break;
            case ConditionKind.Quest:
                if (c.Target.Length == 0)
                    Add(Severity.Problem, "a quest condition has no quest chosen.");
                else if (cat.Quest(c.Target) is null)
                    Add(Severity.Warning, $"the game's journal has no quest called {c.Target}.");
                break;
            case ConditionKind.QuestBlock:
                if (!GuidPattern().IsMatch(c.Target))
                    Add(Severity.Problem, "a quest block condition needs the block's GUID.");
                else if (Catalog.QuestBlocksReady && Catalog.Block(c.Target) is null)
                    Add(Severity.Warning, $"{c.Target} isn't a block in the game's own quests. Only vanilla blocks work, never Brothers In Arms ones.");
                else if (Catalog.Block(c.Target) is { } b && c.Output.Length > 0 && b.Block.Outputs is { } outs && !outs.Contains(c.Output))
                    Add(Severity.Problem, $"the block has no output called {c.Output}.");
                break;
            case ConditionKind.Kills:
                if (c.NameId <= 0)
                    Add(Severity.Problem, "a kills condition has no creature chosen.");
                else if (cat.Kill(c.NameId) is null)
                    Add(Severity.Warning, $"PotP doesn't count kills of name ID {c.NameId}.");
                break;
            case ConditionKind.Item:
            case ConditionKind.GwentCard:
                if (c.Target.Length == 0)
                    Add(Severity.Problem, $"an {(c.Kind == ConditionKind.Item ? "item" : "Gwent card")} condition has nothing chosen.");
                else if (cat.Item(c.Target) is null)
                    Add(Severity.Warning, $"the game has no item called {c.Target} (a mod's own item works if that mod is installed).");
                break;
            case ConditionKind.PotpEntry:
                if (c.Target.Length == 0)
                    Add(Severity.Problem, "a PotP entry condition has no entry chosen.");
                else if (cat.Entry(c.Target) is null)
                    Add(Severity.Warning, $"PotP has no entry {c.Target}.");
                break;
            case ConditionKind.PotpSection:
                if (c.Target.Length == 0)
                    Add(Severity.Problem, "a PotP section condition has no section chosen.");
                else if (cat.Section(c.Target) is null)
                    Add(Severity.Warning, $"PotP has no section {c.Target}.");
                if (c.Unit == "percent" && c.Value is < 0 or > 100)
                    Add(Severity.Problem, "a section's percentage goes from 0 to 100.");
                break;
            case ConditionKind.Achievement:
                if (c.Target.Length == 0)
                    Add(Severity.Problem, "an achievement condition has no achievement chosen.");
                else if (!c.Target.Contains('.'))
                {
                    if (c.Target == a.Id)
                        Add(Severity.Problem, "it can't need itself.");
                    else if (pack.Achievements.All(x => x.Id != c.Target))
                        Add(Severity.Problem, $"the pack has no achievement {c.Target}.");
                }
                else
                    Add(Severity.Warning, $"{c.Target} is in another pack, which players need installed too.");
                break;
            case ConditionKind.VanillaAchievement:
                if (cat.Vanilla(c.Target) is null)
                    Add(Severity.Problem, $"the game has no achievement {c.Target}.");
                break;
        }
        if (c.HasComparison && c.Value is < -2000000000 or > 2000000000)
            Add(Severity.Problem, $"{what}'s number is out of range.");
    }
}
