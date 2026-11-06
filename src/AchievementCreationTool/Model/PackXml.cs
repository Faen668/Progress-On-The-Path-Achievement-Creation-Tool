using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AchievementCreationTool.Model;

/// <summary>Reads and writes potp_achievements.xml, version 1, as Progress on the Path's overlay reads it. The order of
/// elements and the attributes written are the schema's; what's at its default is left out, so a file stays short.</summary>
public static class PackXml
{
    public const string FileName = "potp_achievements.xml";

    static readonly Dictionary<ConditionKind, string> Names = new()
    {
        [ConditionKind.All] = "all", [ConditionKind.Any] = "any", [ConditionKind.AtLeast] = "atLeast", [ConditionKind.Not] = "not",
        [ConditionKind.Fact] = "fact", [ConditionKind.Quest] = "quest", [ConditionKind.QuestBlock] = "questBlock", [ConditionKind.Kills] = "kills",
        [ConditionKind.Item] = "item", [ConditionKind.GwentCard] = "gwentCard", [ConditionKind.Level] = "level", [ConditionKind.Crowns] = "crowns",
        [ConditionKind.PotpEntry] = "potpEntry", [ConditionKind.PotpSection] = "potpSection", [ConditionKind.Achievement] = "achievement",
        [ConditionKind.VanillaAchievement] = "vanillaAchievement",
    };

    public static string ElementName(ConditionKind kind) => Names[kind];

    // ------------------------------------------------------------------ reading

    public sealed class ReadProblem : Exception
    {
        public ReadProblem(string message) : base(message) { }
    }

    public static Pack Load(string path)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(path, LoadOptions.SetLineInfo);
        }
        catch (XmlException e)
        {
            throw new ReadProblem($"It isn't valid XML: {e.Message}");
        }
        var root = doc.Root;
        if (root is null || root.Name.LocalName != "potpAchievements")
            throw new ReadProblem("It isn't an achievements pack: its root isn't <potpAchievements>.");
        if ((string?)root.Attribute("schemaVersion") != "1")
            throw new ReadProblem("It's for a version of the format this tool doesn't know.");

        var pack = new Pack { Id = (string?)root.Attribute("id") ?? "", FilePath = path };
        if (root.Element("about") is { } about)
        {
            ReadText(about, "title", pack.Title);
            pack.Author = (string?)about.Element("author") ?? "";
            pack.Version = (string?)about.Element("version") ?? "";
            pack.MinPotP = (string?)about.Element("minPotP") ?? "";
            pack.Url = (string?)about.Element("url") ?? "";
        }
        foreach (var node in root.Elements("achievement"))
            pack.Achievements.Add(ReadAchievement(node));
        return pack;
    }

    static void ReadText(XElement parent, string name, LocalText into)
    {
        foreach (var node in parent.Elements(name))
        {
            var lang = (string?)node.Attribute("lang");
            var key = (string?)node.Attribute("key");
            if (!string.IsNullOrEmpty(key) && into.Key.Length == 0)
                into.Key = key;
            if (lang is null)
                into.Text = node.Value;
            else if (node.Value.Length > 0)
                into.Translations.Add((lang, node.Value));
        }
    }

    static Achievement ReadAchievement(XElement node)
    {
        var a = new Achievement
        {
            Id = (string?)node.Attribute("id") ?? "",
            Icon = (string?)node.Attribute("icon") ?? "builtin:trophy",
            Ring = (string?)node.Attribute("ring") ?? "gold",
            Hidden = Bool(node, "hidden", false),
            Retroactive = Bool(node, "retroactive", true),
        };
        ReadText(node, "title", a.Title);
        ReadText(node, "description", a.Description);
        if (node.Element("unlock")?.Elements().FirstOrDefault() is { } first)
            a.Unlock = ReadCondition(first);
        if (node.Element("progress") is { } progress)
        {
            a.ProgressOf = (string?)progress.Attribute("of") ?? "";
            a.ProgressConditions = Bool(progress, "conditions", false);
        }
        if (node.Element("reward") is { } reward)
            foreach (var part in reward.Elements())
            {
                var r = part.Name.LocalName switch
                {
                    "crowns" => new Reward { Kind = RewardKind.Crowns, Amount = Int(part, "amount", 1) },
                    "experience" => new Reward { Kind = RewardKind.Experience, Amount = Int(part, "amount", 1) },
                    "item" => new Reward { Kind = RewardKind.Item, Item = (string?)part.Attribute("name") ?? "", Amount = Int(part, "quantity", 1) },
                    _ => null,
                };
                if (r is not null)
                    a.Rewards.Add(r);
            }
        return a;
    }

    static Condition ReadCondition(XElement node)
    {
        var name = node.Name.LocalName;
        var kind = Names.FirstOrDefault(kv => kv.Value == name).Key;
        if (!Names.ContainsValue(name))
            throw new ReadProblem($"Line {((IXmlLineInfo)node).LineNumber}: <{name}> isn't a condition this tool knows.");
        var c = new Condition(kind)
        {
            Op = (string?)node.Attribute("op") ?? "ge",
            Ref = (string?)node.Attribute("ref") ?? "",
            Label = (string?)node.Attribute("label") ?? "",
        };
        switch (kind)
        {
            case ConditionKind.AtLeast: c.N = Int(node, "n", 1); break;
            case ConditionKind.Fact: c.Target = (string?)node.Attribute("name") ?? ""; c.Value = Int(node, "value", 1); break;
            case ConditionKind.Quest: c.Target = (string?)node.Attribute("journal") ?? ""; c.State = (string?)node.Attribute("state") ?? "done"; break;
            case ConditionKind.QuestBlock:
                c.Target = (string?)node.Attribute("guid") ?? "";
                c.Output = (string?)node.Attribute("output") ?? "";
                c.QuestFile = (string?)node.Attribute("quest") ?? "";
                break;
            case ConditionKind.Kills: c.NameId = Int(node, "nameId", 0); c.Value = Int(node, "value", 1); break;
            case ConditionKind.Item: c.Target = (string?)node.Attribute("name") ?? ""; c.Where = (string?)node.Attribute("where") ?? "either"; c.Value = Int(node, "value", 1); break;
            case ConditionKind.GwentCard: c.Target = (string?)node.Attribute("name") ?? ""; c.Value = Int(node, "value", 1); break;
            case ConditionKind.Level:
            case ConditionKind.Crowns: c.Value = Int(node, "value", 1); break;
            case ConditionKind.PotpEntry: c.Target = (string?)node.Attribute("uuid") ?? ""; c.State = (string?)node.Attribute("state") ?? "completed"; break;
            case ConditionKind.PotpSection:
                c.Target = (string?)node.Attribute("id") ?? "";
                c.Unit = (string?)node.Attribute("unit") ?? "percent";
                c.Value = Int(node, "value", c.Unit == "percent" ? 100 : 1);
                break;
            case ConditionKind.Achievement: c.Target = (string?)node.Attribute("id") ?? ""; break;
            case ConditionKind.VanillaAchievement: c.Target = (string?)node.Attribute("name") ?? ""; break;
        }
        foreach (var child in node.Elements())
            c.Add(ReadCondition(child));
        return c;
    }

    static bool Bool(XElement node, string name, bool fallback)
    {
        var text = (string?)node.Attribute(name);
        return text is null ? fallback : text is "true" or "1";
    }

    static int Int(XElement node, string name, int fallback) => int.TryParse((string?)node.Attribute(name), out var v) ? v : fallback;

    // ------------------------------------------------------------------ writing

    public static XDocument ToXml(Pack pack, string? comment = null)
    {
        var root = new XElement("potpAchievements", new XAttribute("schemaVersion", "1"), new XAttribute("id", pack.Id));
        var about = new XElement("about");
        WriteText(about, "title", pack.Title, fallback: pack.Id);
        if (pack.Author.Length > 0) about.Add(new XElement("author", pack.Author));
        if (pack.Version.Length > 0) about.Add(new XElement("version", pack.Version));
        if (pack.MinPotP.Length > 0) about.Add(new XElement("minPotP", pack.MinPotP));
        if (pack.Url.Length > 0) about.Add(new XElement("url", pack.Url));
        root.Add(about);
        foreach (var a in pack.Achievements)
            root.Add(WriteAchievement(a));
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null));
        if (comment is not null)
            doc.Add(new XComment(" " + comment.Replace("--", "-") + " "));
        doc.Add(root);
        return doc;
    }

    public static void Save(Pack pack, string path)
    {
        var doc = ToXml(pack, "Made with Progress on the Path: Achievement Creation Tool");
        var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", Encoding = new UTF8Encoding(false), NewLineChars = "\r\n" };
        using (var writer = XmlWriter.Create(path, settings))
            doc.Save(writer);
        pack.FilePath = path;
    }

    static void WriteText(XElement parent, string name, LocalText text, string fallback = "")
    {
        var main = new XElement(name, text.Text.Length > 0 || text.Key.Length > 0 ? text.Text : fallback);
        if (text.Key.Length > 0)
            main.Add(new XAttribute("key", text.Key));
        if (text.Text.Length > 0 || text.Key.Length > 0 || text.Translations.Count == 0)
            parent.Add(main);
        foreach (var (lang, value) in text.Translations)
            parent.Add(new XElement(name, new XAttribute("lang", lang), value));
    }

    static XElement WriteAchievement(Achievement a)
    {
        var node = new XElement("achievement", new XAttribute("id", a.Id));
        if (a.Icon != "builtin:trophy") node.Add(new XAttribute("icon", a.Icon));
        if (a.Ring != "gold") node.Add(new XAttribute("ring", a.Ring));
        if (a.Hidden) node.Add(new XAttribute("hidden", "true"));
        if (!a.Retroactive) node.Add(new XAttribute("retroactive", "false"));
        WriteText(node, "title", a.Title, fallback: a.Id);
        if (!a.Description.IsEmpty)
            WriteText(node, "description", a.Description);
        if (a.Unlock is not null)
            node.Add(new XElement("unlock", WriteCondition(a.Unlock)));
        if (a.ProgressOf.Length > 0)
            node.Add(new XElement("progress", new XAttribute("of", a.ProgressOf)));
        else if (a.ProgressConditions)
            node.Add(new XElement("progress", new XAttribute("conditions", "true")));
        if (a.Rewards.Count > 0)
            node.Add(new XElement("reward", a.Rewards.Select(r => r.Kind switch
            {
                RewardKind.Crowns => new XElement("crowns", new XAttribute("amount", r.Amount)),
                RewardKind.Experience => new XElement("experience", new XAttribute("amount", r.Amount)),
                _ => r.Amount == 1
                    ? new XElement("item", new XAttribute("name", r.Item))
                    : new XElement("item", new XAttribute("name", r.Item), new XAttribute("quantity", r.Amount)),
            })));
        return node;
    }

    static XElement WriteCondition(Condition c)
    {
        var node = new XElement(Names[c.Kind]);
        void Attr(string name, object value) => node.Add(new XAttribute(name, value));
        void Compare(int defaultValue = 1)
        {
            if (c.Op != "ge") Attr("op", c.Op);
            if (c.Value != defaultValue) Attr("value", c.Value);
        }
        switch (c.Kind)
        {
            case ConditionKind.AtLeast: Attr("n", c.N); break;
            case ConditionKind.Fact: Attr("name", c.Target); Compare(); break;
            case ConditionKind.Quest: Attr("journal", c.Target); if (c.State is not ("" or "done")) Attr("state", c.State); break;
            case ConditionKind.QuestBlock:
                Attr("guid", c.Target);
                if (c.Output.Length > 0) Attr("output", c.Output);
                if (c.QuestFile.Length > 0) Attr("quest", c.QuestFile);
                break;
            case ConditionKind.Kills: Attr("nameId", c.NameId); Compare(); break;
            case ConditionKind.Item: Attr("name", c.Target); if (c.Where != "either") Attr("where", c.Where); Compare(); break;
            case ConditionKind.GwentCard: Attr("name", c.Target); Compare(); break;
            case ConditionKind.Level:
            case ConditionKind.Crowns: Compare(); break;
            case ConditionKind.PotpEntry: Attr("uuid", c.Target); if (c.State is not ("" or "completed")) Attr("state", c.State); break;
            case ConditionKind.PotpSection:
                Attr("id", c.Target);
                if (c.Unit != "percent") Attr("unit", c.Unit);
                if (c.Op != "ge") Attr("op", c.Op);
                if (c.Value != (c.Unit == "percent" ? 100 : 1)) Attr("value", c.Value);
                break;
            case ConditionKind.Achievement: Attr("id", c.Target); break;
            case ConditionKind.VanillaAchievement: Attr("name", c.Target); break;
        }
        if (c.Ref.Length > 0) Attr("ref", c.Ref);
        if (c.Label.Length > 0) Attr("label", c.Label);
        foreach (var child in c.Children)
            node.Add(WriteCondition(child));
        return node;
    }
}
