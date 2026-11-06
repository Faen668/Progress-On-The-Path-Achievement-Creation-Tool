using System.IO;
using AchievementCreationTool.Data;
using AchievementCreationTool.Model;

namespace AchievementCreationTool.Logic;

/// <summary>Conditions, rewards and the rest in plain words, from the catalog's names rather than the game's IDs</summary>
public static class Describe
{
    public static readonly (string Op, string Words)[] Ops =
    [
        ("ge", "at least"), ("gt", "more than"), ("le", "at most"), ("lt", "less than"), ("eq", "exactly"), ("ne", "anything but"),
    ];

    public static string OpWords(string op) => Ops.FirstOrDefault(o => o.Op == op).Words ?? "at least";

    public static readonly (string State, string Words)[] QuestStates =
    [
        ("done", "is done"), ("failed", "has failed"), ("ended", "has ended, done or failed"), ("active", "is going on"), ("started", "has started"),
    ];

    public static readonly (string State, string Words)[] EntryStates =
    [
        ("completed", "is completed"), ("missed", "was missed"), ("ignored", "is ignored"), ("closed", "is completed, missed or ignored"),
    ];

    public static readonly (string Where, string Words)[] Wheres =
    [
        ("either", "inventory or stash"), ("inventory", "inventory"), ("stash", "stash"),
    ];

    public static string KindName(ConditionKind kind) => kind switch
    {
        ConditionKind.All => "All of these",
        ConditionKind.Any => "Any of these",
        ConditionKind.AtLeast => "At least",
        ConditionKind.Not => "None of this",
        ConditionKind.Fact => "Fact",
        ConditionKind.Quest => "Quest",
        ConditionKind.QuestBlock => "Quest block",
        ConditionKind.Kills => "Kills",
        ConditionKind.Item => "Item",
        ConditionKind.GwentCard => "Gwent card",
        ConditionKind.Level => "Level",
        ConditionKind.Crowns => "Crowns",
        ConditionKind.PotpEntry => "PotP entry",
        ConditionKind.PotpSection => "PotP section",
        ConditionKind.Achievement => "Achievement",
        ConditionKind.VanillaAchievement => "Game achievement",
        _ => kind.ToString(),
    };

    public static string KindName(Condition c) => KindName(c.Kind);

    public static string Summary(Condition c)
    {
        var cat = Catalog.Current;
        string Or(string text, string fallback) => string.IsNullOrWhiteSpace(text) ? fallback : text;
        return c.Kind switch
        {
            ConditionKind.All => $"every one of the {Count(c)} below",
            ConditionKind.Any => $"one of the {Count(c)} below is enough",
            ConditionKind.AtLeast => $"{c.N} of the {Count(c)} below",
            ConditionKind.Not => "the one below must not be true",
            ConditionKind.Fact => Or(c.Target, "(no fact chosen)"),
            ConditionKind.Quest => cat.Quest(c.Target) is { } q && q.Title.Length > 0 ? q.Title : Or(c.Target, "(no quest chosen)"),
            ConditionKind.QuestBlock => BlockSummary(c),
            ConditionKind.Kills => cat.Kill(c.NameId) is { } k ? k.Title : c.NameId > 0 ? $"name ID {c.NameId}" : "(no creature chosen)",
            ConditionKind.Item or ConditionKind.GwentCard => c.Target.Length > 0 ? cat.ItemTitle(c.Target) : "(no item chosen)",
            ConditionKind.Level => "Geralt's level",
            ConditionKind.Crowns => "Crowns he carries",
            ConditionKind.PotpEntry => cat.Entry(c.Target) is { Title.Length: > 0 } e ? e.Title : Or(c.Target, "(no entry chosen)"),
            ConditionKind.PotpSection => cat.Section(c.Target) is { Title.Length: > 0 } s ? s.Title : Or(c.Target, "(no section chosen)"),
            ConditionKind.Achievement => Or(c.Target, "(no achievement chosen)"),
            ConditionKind.VanillaAchievement => cat.Vanilla(c.Target) is { } v ? v.Title : Or(c.Target, "(no achievement chosen)"),
            _ => "",
        };
    }

    static int Count(Condition c) => c.Children.Count;

    static string BlockSummary(Condition c)
    {
        if (c.Target.Length == 0)
            return "(no quest block chosen)";
        if (Catalog.Block(c.Target) is { } found)
        {
            var name = found.Block.Name ?? found.Block.Comment ?? found.Block.Kind.Replace("CQuest", "").Replace("Block", " block");
            return $"{name} in {Path.GetFileNameWithoutExtension(found.File.File)}";
        }
        return c.QuestFile.Length > 0 ? $"{c.Target} in {Path.GetFileNameWithoutExtension(c.QuestFile)}" : c.Target;
    }

    public static string Detail(Condition c) => c.Kind switch
    {
        ConditionKind.Quest => QuestStates.FirstOrDefault(s => s.State == (c.State.Length > 0 ? c.State : "done")).Words ?? "",
        ConditionKind.PotpEntry => EntryStates.FirstOrDefault(s => s.State == (c.State.Length > 0 ? c.State : "completed")).Words ?? "",
        ConditionKind.QuestBlock => c.Output.Length > 0 ? $"has run, through {c.Output}" : "has run",
        ConditionKind.PotpSection => c.Unit == "percent" ? $"{OpWords(c.Op)} {c.Value}% done" : $"{OpWords(c.Op)} {c.Value} done",
        ConditionKind.Item => $"{OpWords(c.Op)} {c.Value}, in {Wheres.FirstOrDefault(w => w.Where == c.Where).Words ?? "inventory or stash"}",
        ConditionKind.Achievement or ConditionKind.VanillaAchievement => "is unlocked",
        _ when c.HasComparison => $"{OpWords(c.Op)} {c.Value:N0}",
        _ => "",
    };

    /// <summary>What a condition says as a label in the file, so it stays readable by hand</summary>
    public static string LabelFor(Condition c) => c.IsGroup || c.Kind is ConditionKind.Level or ConditionKind.Crowns ? "" : Summary(c);

    public static string Reward(Reward r) => r.Kind switch
    {
        RewardKind.Crowns => r.Amount == 1 ? "1 crown" : $"{r.Amount:N0} crowns",
        RewardKind.Experience => $"{r.Amount:N0} experience",
        _ => r.Amount == 1 ? Catalog.Current.ItemTitle(r.Item) : $"{Catalog.Current.ItemTitle(r.Item)} x{r.Amount}",
    };

    public static string Rewards(Achievement a) => string.Join(", ", a.Rewards.Select(Reward));
}
