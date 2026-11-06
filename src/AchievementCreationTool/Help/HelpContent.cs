namespace AchievementCreationTool.Help;

public enum BlockKind { Heading, Text, Bullet, Example, Tip }

public sealed record HelpBlock(BlockKind Kind, string Text);

public sealed record HelpTopic(string Id, string Title, string Summary, HelpBlock[] Blocks);

/// <summary>The guide shown in the Help window: plain words, with an example wherever something could puzzle</summary>
public static class HelpContent
{
    static HelpBlock H(string t) => new(BlockKind.Heading, t);
    static HelpBlock P(string t) => new(BlockKind.Text, t);
    static HelpBlock B(string t) => new(BlockKind.Bullet, t);
    static HelpBlock Ex(string t) => new(BlockKind.Example, t);
    static HelpBlock Tip(string t) => new(BlockKind.Tip, t);

    public static readonly HelpTopic[] Topics =
    [
        new("start", "Getting started", "What the tool makes, and the steps from a new pack to the game",
        [
            P("This tool makes an achievements pack for Progress on the Path. A pack is one file, potp_achievements.xml, in a mod folder of its own. PotP's overlay finds it, shows its achievements in the Achievements tab, unlocks them when their conditions are met, and gives their rewards."),
            P("Players need Progress on the Path and its overlay installed, as with any PotP feature."),
            H("The steps"),
            B("New pack, then Pack settings: give it an ID (such as velen_adventures) and a title. The title is the section's heading in the Achievements tab."),
            B("Add achievement for each one, then fill in its title, description and icon."),
            B("Add condition: what has to happen for it to unlock. This is the part to take slowly: see Conditions."),
            B("Optionally, a progress bar and a reward."),
            B("Save. Choose an empty folder: it becomes your mod's folder, with your pictures and strings beside the pack."),
            B("Check and export: it lists anything to fix, then copies the pack into the game's mods folder."),
            Tip("Everything on the right of the window updates as you type: the preview shows the achievement as the overlay will, and Checks says what's missing."),
        ]),

        new("achievements", "An achievement's details", "Title, description, ID, ring, hidden and the rest",
        [
            H("Title and description"),
            P("What the player reads in the Achievements tab and the banner when it unlocks. Keep the title short (up to 80 characters) and say in the description what it asks, so players know what to do."),
            Ex("Title: Friend of Ravens\nDescription: Finish every witcher contract in Velen."),
            H("ID"),
            P("A short name for the file and for players' saves: lower case letters, digits and _, starting with a letter. Each achievement in the pack needs its own. Its full ID is the pack's ID, a dot, then its own: velen_adventures.friend_of_ravens."),
            Tip("Don't change an ID once the pack is out: unlocks are kept in players' saves under it, so a new ID starts the achievement over."),
            H("Ring"),
            P("The colour its icon is drawn in once it's unlocked: gold, silver or bronze, a way to show how hard it is. While locked, every icon is drawn in red, as the game's own locked achievements are."),
            H("Hidden until it's unlocked"),
            P("For a secret: the tab shows \"Hidden achievement\" with no description, icon or reward until it's unlocked. Players can still choose to see secrets in PotP's settings."),
            H("Unlock on saves that already qualify"),
            P("On: if a player installs your pack on a save that has already done what it asks, it unlocks as soon as the save loads. Off: only what happens after the pack is installed counts."),
            Ex("\"Finish the prologue\" with it on unlocks at once for a player who's past the prologue. With it off, it never unlocks for them, as the prologue can't be done again."),
        ]),

        new("conditions", "Conditions: how they work", "What a condition is, and how groups combine them",
        [
            P("A condition is one thing that's true or not in the player's game: a quest is done, a fact has a value, Geralt has an item, and so on. An achievement unlocks the moment its conditions are met."),
            H("One condition"),
            P("The simplest achievement has one condition. Add condition, pick its kind on the left, find what it's about in the list, and choose."),
            Ex("Unlocks when:\n  Quest   The Beast of White Orchard   is done"),
            H("More than one: groups"),
            P("To need more than one thing, the conditions go into a group. When you add a second condition, the tool puts both into an \"All of these\" group for you. A group's box at its top says how its conditions combine:"),
            B("All of these: every condition in it must be true."),
            B("Any of these: one is enough."),
            B("At least: a number of them, which you type in. 3 means any 3 of them."),
            B("None of this: the one condition in it must not be true."),
            Ex("Unlocks when:\n  All of these\n    Quest   The Beast of White Orchard   is done\n    Kills   Griffin   at least 3"),
            H("Groups inside groups"),
            P("A group can hold another group, for \"this, and one of those\". Select a group, then Add condition or Add group: what you add goes into the selected group. With a condition selected, it goes into the group that condition is in."),
            Ex("Unlocks when:\n  All of these\n    Quest   The Beast of White Orchard   is done\n    Any of these\n      Kills   Griffin   at least 3\n      Level   Geralt's level   at least 12"),
            P("This one needs the quest done, and either three griffins or level 12."),
            H("Changing and moving them"),
            B("Double-click a condition, or select it and press Change, to change it."),
            B("Up and Down move a condition within its group."),
            B("Remove takes out the selected condition, or a whole group with what's in it."),
            Tip("Use \"None of this\" with care: something that hasn't happened yet is true at the very start of a game. With \"Unlock on saves that already qualify\" on, it may unlock at once."),
        ]),

        new("kinds", "Which kind of condition?", "Quest, PotP entry, fact, quest block and the rest, and when each is the right one",
        [
            P("Several kinds can say the same thing in different ways. In short: use the plainest that works. A quest or a PotP entry is easier to get right than a fact or a quest block."),
            H("Quest"),
            P("A quest in the journal is done, failed, going on or started. Picked by its title. The best choice for anything about a quest as a whole."),
            H("PotP entry and PotP section"),
            P("Anything Progress on the Path tracks: quests, events, places on the map, items, Gwent cards, creatures. An entry is one of them, completed, missed or ignored; a section is one of PotP's lists, done in full or in part (\"every main quest of the prologue\"). PotP already knows when each is done, including the awkward ones."),
            H("Kill count"),
            P("How many of a creature or person Geralt has killed, as PotP's Kills tab counts them."),
            H("Item owned and Gwent card"),
            P("How many of an item he has: in his inventory, his stash, or both together. Gwent cards are items too, picked from their own list."),
            H("Level and crowns"),
            P("Geralt's level, and the crowns he carries."),
            H("Another achievement and game achievement"),
            P("Another achievement in your pack is unlocked (for chains: \"unlock all three of these\"), or one of the game's own achievements is."),
            H("Fact"),
            P("Quests keep facts: named numbers they set and check, such as how many trophies were taken or whether a choice was made. Each fact in the list shows the quest files that set it and check it, which says what it's about."),
            Tip("A fact the list says is only checked, never set, comes from somewhere else, such as a scene. It may never change. Prefer a quest or a PotP entry when one says the same."),
            P("A fact your own mod's quests set can be typed in by name."),
            H("Quest block"),
            P("A block in one of the game's quest files has run: the most precise kind, for a moment in the middle of a quest. PotP hears blocks through Native Quest Hook. Search by the quest file's name (q101) and the block's name, and choose the output it has to run through if it has more than one."),
            Tip("Only blocks from the game's own quests work. Brothers In Arms and other mods change quest files, so their blocks aren't heard. The list holds only the game's own."),
        ]),

        new("progress", "Progress bars", "Showing how far a player has got",
        [
            P("An achievement can show a bar in the Achievements tab: how far the player has got on this save. It counts one of its conditions that has a number in it, or how many of its conditions are met."),
            H("Counting one condition"),
            P("Facts, kills, items, Gwent cards, level, crowns and PotP sections have a number. Choose one in \"Progress bar in the overlay\", or tick \"Show it as the achievement's progress bar\" when adding it."),
            Ex("Kills   Griffin   at least 5   shows   3 of 5 on this save"),
            H("Counting the conditions met"),
            P("For a group, the bar can count its conditions instead: with \"All of these\" holding five quests, it shows how many of the five are done."),
        ]),

        new("rewards", "Rewards", "Crowns, items and experience, given when it unlocks",
        [
            P("An achievement can give a reward when it unlocks: crowns, items or experience, up to ten of them. The banner and the tab list what it gives."),
            B("Crowns and experience: 1 to 100,000."),
            B("An item: picked from the game's items, 1 to 100 of it. A mod's own item works if that mod is installed."),
            P("A reward is given once, when the achievement unlocks. Unlocks are kept in each save, so loading a save from before takes the unlock and the reward back with it, and they're given again when it unlocks again."),
            Tip("A hidden achievement keeps its reward secret too, until it's unlocked."),
        ]),

        new("icons", "Icons", "PotP's own icons, the game's pictures, or your own",
        [
            B("Progress on the Path's icons: drawn for PotP and free to use. They're drawn as lines in the ring's colour once unlocked, and in red while locked, so they always match."),
            B("From the game: any item's picture from the game's own files. This tool can't read the game's files, so it shows a frame in its place; the game shows the real picture."),
            B("Your own picture: a PNG or JPEG. It's copied into the pack's folder, in icons\\, and goes with the pack when it's exported. Save the pack first, so there's a folder to copy it to."),
            Tip("A square picture looks best: the overlay draws it up to 128 pixels across."),
        ]),

        new("strings", "Translations", "Showing an achievement in the player's language with your mod's .w3strings",
        [
            P("A simple pack writes its text straight into the file, in one language. A pack that's translated keeps its text in .w3strings files instead, as other mods do, and the game shows it in the player's language."),
            H("How"),
            B("Give each title and description a string key, in \"Text from the mod's own .w3strings\" under the achievement's details. The pack's own title has one in Pack settings."),
            B("Put the strings in your mod's content folder: content\\en.w3strings, content\\pl.w3strings and so on, made with your usual string tools. W3 Mod Workbench can make one from a CSV."),
            B("Keep the text above as well: it's shown when no .w3strings has the key, such as for a language you haven't translated."),
            Ex("Title's key: velen_adventures_ravens_title\nen.w3strings: velen_adventures_ravens_title = Friend of Ravens\npl.w3strings: velen_adventures_ravens_title = Przyjaciel Kruków"),
            Tip("The game reads .w3strings only when it starts. After changing them, restart the game to see it."),
        ]),

        new("export", "Saving and exporting", "Where the pack goes, and getting it to players",
        [
            H("Saving"),
            P("Save writes potp_achievements.xml into a folder you choose. Use an empty folder for each pack: it becomes your mod's folder, with your pictures in icons\\ and any .w3strings in content\\."),
            H("Exporting"),
            P("Check and export checks the whole pack, then copies the pack, its pictures and its content folder into the game's mods folder, as mods\\modYourPack. With the game running, PotP reads it again within a few seconds."),
            P("With \"Also make a .zip ready for Nexus\" ticked, it also makes modYourPack.zip beside the pack, laid out as players unpack it into the game's folder."),
            H("Problems and warnings"),
            B("A problem stops the export: the overlay would skip that achievement, or the pack. Each says what to fix."),
            B("A warning doesn't: it's something that works but may not do what you mean, such as a fact no vanilla quest sets. Fine for a fact your mod's own quest sets."),
        ]),

        new("game", "How it works in the game", "Saves, dates, and what players see",
        [
            B("Unlocks belong to the save. A new game starts with every achievement locked, and loading an older save takes back anything unlocked since."),
            B("The tab shows the date it was unlocked, as it does for the game's own."),
            B("When one unlocks, PotP shows its banner at the top of the screen, with the achievement's icon, what it asked and its reward."),
            B("PotP checks the conditions about once a second, and at once when a quest block runs."),
            B("Quests and other mods can tell an achievement is unlocked from its fact: potp_ach.yourpack.achievement_id."),
        ]),
    ];

    public static HelpTopic Topic(string id) => Topics.FirstOrDefault(t => t.Id == id) ?? Topics[0];
}
