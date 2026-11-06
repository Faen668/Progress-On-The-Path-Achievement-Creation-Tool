using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace AchievementCreationTool.Data;

// What the catalog files hold: made from the game's files by Progress on the Path's
// overlay/tools/gen_achievement_catalog.py, and baked into the exe as resources named catalog/<file>.json.

public sealed record QuestInfo(string Journal, string Title, string Type, int World, string Guid, string Dlc, string File);
public sealed record FactInfo(string Name, [property: JsonPropertyName("set_in")] string[] SetIn, [property: JsonPropertyName("checked_in")] string[] CheckedIn);
public sealed record BlockInfo(string Guid, string Kind, string? Name, string? Comment, string[]? Outputs);
public sealed record BlockFile(string File, string Dlc, BlockInfo[] Blocks);
public sealed record ItemInfo(string Name, string Category, string Title, string Icon, string Dlc, string File);
public sealed record KillInfo(int NameId, string Title, bool Unique);
public sealed record PotpSection(string Id, string Book, string Title, string File);
public sealed record PotpEntry(string Uuid, string Kind, string Section, string Title, string? Journal);
public sealed record PotpLists(PotpSection[] Sections, PotpEntry[] Entries);
public sealed record VanillaAchievement(string Name, string Store, string Group, bool Hidden, string Title, string Description);
public sealed record IconInfo(string Name, string Path);

public sealed class Catalog
{
    public static Catalog Current { get; private set; } = new();

    public QuestInfo[] Quests { get; private set; } = [];
    public FactInfo[] Facts { get; private set; } = [];
    public ItemInfo[] Items { get; private set; } = [];
    public KillInfo[] Kills { get; private set; } = [];
    public PotpLists Potp { get; private set; } = new([], []);
    public VanillaAchievement[] Achievements { get; private set; } = [];
    public IconInfo[] Icons { get; private set; } = [];
    public string? Problem { get; private set; }

    Dictionary<string, QuestInfo> questByJournal = new();
    Dictionary<string, FactInfo> factByName = new();
    Dictionary<string, ItemInfo> itemByName = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<int, KillInfo> killById = new();
    Dictionary<string, PotpEntry> entryByUuid = new();
    Dictionary<string, PotpSection> sectionById = new();
    Dictionary<string, VanillaAchievement> vanillaByName = new();
    Dictionary<string, Geometry> iconGeometry = new();

    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static void Load()
    {
        var c = new Catalog();
        try
        {
            c.Quests = Read<QuestInfo[]>("quests");
            c.Facts = Read<FactInfo[]>("facts");
            c.Items = Read<ItemInfo[]>("items");
            c.Kills = Read<KillInfo[]>("kills");
            c.Potp = Read<PotpLists>("potp");
            c.Achievements = Read<VanillaAchievement[]>("achievements");
            c.Icons = Read<IconInfo[]>("icons");
        }
        catch (Exception e)
        {
            c.Problem = $"The catalog built into the tool couldn't be read: {e.Message}";
        }
        c.questByJournal = c.Quests.GroupBy(q => q.Journal).ToDictionary(g => g.Key, g => g.First());
        c.factByName = c.Facts.GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First());
        c.itemByName = c.Items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        c.killById = c.Kills.GroupBy(k => k.NameId).ToDictionary(g => g.Key, g => g.First());
        c.entryByUuid = c.Potp.Entries.GroupBy(e => e.Uuid).ToDictionary(g => g.Key, g => g.First());
        c.sectionById = c.Potp.Sections.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
        c.vanillaByName = c.Achievements.GroupBy(a => a.Name).ToDictionary(g => g.Key, g => g.First());
        foreach (var icon in c.Icons)
        {
            try
            {
                var g = Geometry.Parse(icon.Path);
                g.Freeze();
                c.iconGeometry[icon.Name] = g;
            }
            catch (FormatException) { }
        }
        Current = c;
    }

    // From the exe itself: the catalog is baked in, so nothing can go missing or be changed beside it
    static T Read<T>(string name)
    {
        using var stream = typeof(Catalog).Assembly.GetManifestResourceStream("catalog/" + name + ".json")
            ?? throw new FileNotFoundException(name + ".json isn't in the tool");
        return JsonSerializer.Deserialize<T>(stream, Options) ?? throw new InvalidDataException(name + ".json is empty");
    }

    public QuestInfo? Quest(string journal) => questByJournal.GetValueOrDefault(journal);
    public FactInfo? Fact(string name) => factByName.GetValueOrDefault(name);
    public ItemInfo? Item(string name) => itemByName.GetValueOrDefault(name);
    public KillInfo? Kill(int id) => killById.GetValueOrDefault(id);
    public PotpEntry? Entry(string uuid) => entryByUuid.GetValueOrDefault(uuid);
    public PotpSection? Section(string id) => sectionById.GetValueOrDefault(id);
    public VanillaAchievement? Vanilla(string name) => vanillaByName.GetValueOrDefault(name);
    public Geometry? IconGeometry(string name) => iconGeometry.GetValueOrDefault(name);

    public IEnumerable<ItemInfo> GwentCards => Items.Where(i => i.Category == "gwint");

    /// <summary>The name of an item as the game shows it in English, else its own name</summary>
    public string ItemTitle(string name) => Item(name) is { Title.Length: > 0 } i ? i.Title : name;

    // ------------------------------------------------------------------ quest blocks: big, so read when first needed

    static Task<BlockFile[]>? blocks;
    static Dictionary<string, (BlockFile File, BlockInfo Block)>? blockByGuid;

    public static Task<BlockFile[]> QuestBlocks()
    {
        return blocks ??= Task.Run(() =>
        {
            var files = Read<BlockFile[]>("quest_blocks");
            var index = new Dictionary<string, (BlockFile, BlockInfo)>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
                foreach (var b in f.Blocks)
                    index.TryAdd(b.Guid, (f, b));
            blockByGuid = index;
            return files;
        });
    }

    /// <summary>Null while the quest blocks haven't been read, or for a GUID no vanilla quest file has</summary>
    public static (BlockFile File, BlockInfo Block)? Block(string guid) =>
        blockByGuid is not null && blockByGuid.TryGetValue(guid, out var found) ? found : null;

    public static bool QuestBlocksReady => blockByGuid is not null;
}
