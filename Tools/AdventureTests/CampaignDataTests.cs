using System;
using System.Collections.Generic;
public static class CampaignDataTests
{
    static int checks;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    public static void Main()
    {
        var p = new PartyMemberProgress();
        p.GainXP(-10); Check(p.xp == 0 && p.level == 1, "Negative XP must not remove progress");
        p.GainXP(99); Check(p.level == 1 && p.xp == 99, "XP immediately below threshold");
        p.GainXP(1); Check(p.level == 2 && p.xp == 0 && p.statPoints == 3 && p.skillPoints == 1, "Exact threshold awards one level");
        p.GainXP(360); Check(p.level == 4 && p.xp == 10 && p.statPoints == 9 && p.skillPoints == 3, "Large reward handles multiple levels and remainder");
        p.GainXP(0); Check(p.level == 4 && p.statPoints == 9, "Zero XP never duplicates points");
        p.level = 99; p.xp = 0; p.GainXP(1000000); Check(p.level == 100 && p.xp <= p.RequiredXP, "Max-level cap");
        var save = new CampaignSave();
        save.AddItem("herb", 2); save.AddItem("herb", 3);
        Check(save.Count("herb") == 5 && save.inventory.Count == 1, "Repeated pickups stack");
        Check(!save.Consume("herb", 6) && save.Count("herb") == 5, "Insufficient items leave inventory unchanged");
        Check(!save.Consume("herb", -1) && save.Count("herb") == 5, "Negative consumption cannot create items");
        Check(save.Consume("herb", 5) && save.inventory.Count == 0, "Last consumed item removes stack");
        var recipe = new CraftRecipe { output = new ItemStack("potion", 1), ingredients = new List<ItemStack> { new ItemStack("herb", 2), new ItemStack("ore", 1) } };
        save.AddItem("herb", 2);
        Check(!save.TryCraft(recipe) && save.Count("herb") == 2 && save.Count("potion") == 0, "Missing ingredient must not consume other ingredients");
        save.AddItem("ore", 1);
        Check(save.TryCraft(recipe) && save.Count("potion") == 1 && save.Count("herb") == 0 && save.Count("ore") == 0, "Craft transaction consumes materials and grants output");
        Check(!save.TryCraft(recipe) && save.Count("potion") == 1, "Repeated craft cannot reuse materials");
        recipe.ingredients = new List<ItemStack> { new ItemStack("herb", 2), new ItemStack("herb", 2) };
        save.AddItem("herb", 3);
        Check(!save.TryCraft(recipe) && save.Count("herb") == 3, "Duplicate ingredient rows are summed before consuming");
        save.AddItem("herb", 1);
        Check(save.TryCraft(recipe) && save.Count("herb") == 0 && save.Count("potion") == 2, "Duplicate ingredient rows consume exact total");
        recipe.output.count = 0;
        Check(!save.TryCraft(recipe), "Invalid output rejects recipe");
        Check(p.Rank(null) == 0, "Null skill is unranked");
        p.skills.Add(new SkillRank { id = "fire", rank = 2 });
        Check(p.Rank(new ActionData { name = "fire" }) == 2 && p.Rank(new ActionData { name = "ice" }) == 0, "Skill upgrade keyed to its asset");
        var draft = new PartyMemberProgress { statPoints = 3, hp = 50 };
        Check(!draft.TryAllocateStats(new[] { 2, 2, 0, 0, 0 }, 10) && draft.statPoints == 3 && draft.hp == 50, "Over-budget confirmation is atomic");
        Check(!draft.TryAllocateStats(new[] { -1, 0, 0, 0, 0 }, 10), "Negative draft rejected");
        Check(!draft.TryAllocateStats(new int[5], 10), "Empty confirmation rejected");
        Check(!draft.TryAllocateStats(new[] { 0, 0, 0, 2, 0 }, 99) && draft.statPoints == 3, "Crit cap rejects excess points without spending");
        Check(draft.TryAllocateStats(new[] { 1, 1, 0, 1, 0 }, 99) && draft.hp == 60 && draft.atkBonus == 2 && draft.critBonus == 1 && draft.statPoints == 0, "Confirm applies the entire preview once and clamps crit");
        var unlocked = new PartyMemberProgress { skillPoints = 2 };
        var fire = new ActionData { name = "fire" };
        Check(unlocked.TryUnlock(fire, 2) && unlocked.Rank(fire) == 1 && unlocked.skillPoints == 0, "Unlock spends exact cost");
        unlocked.skillPoints = 3;
        Check(!unlocked.TryUnlock(fire, 1) && unlocked.skillPoints == 3 && unlocked.Rank(fire) == 1, "Unlocked skill cannot be upgraded or charged twice");
        Check(!unlocked.TryUnlock(new ActionData { name = "ice" }, 4), "Insufficient skill points reject unlock");
        var legacy = new CampaignSave { version = 1, party = new List<PartyMemberProgress> { new PartyMemberProgress { skills = new List<SkillRank> { new SkillRank { id = "fire", rank = 4 } } } } };
        legacy.MigrateSkillUnlocks();
        Check(legacy.version == 2 && legacy.party[0].Rank(fire) == 1 && legacy.party[0].skillPoints == 3, "Legacy ranks preserve unlock and refund extra upgrades");
        legacy.MigrateSkillUnlocks(); Check(legacy.party[0].skillPoints == 3, "Migration cannot refund twice");
        var config = new CampaignConfig();
        var ice = new ActionData { name = "ice" };
        Check(config.UnlockPrerequisites(ice).Length == 0, "Unconfigured skills do not acquire hidden index-based prerequisites");
        var onePoint = new PartyMemberProgress { skillPoints = 1 };
        Check(onePoint.TryUnlock(ice, 1) && onePoint.skillPoints == 0, "Exactly one point unlocks a one-point skill");
        config.skillUnlocks.Add(new SkillUnlockRule { skill = ice, prerequisites = new List<ActionData> { fire } });
        Check(config.UnlockPrerequisites(ice).Length == 1 && config.UnlockPrerequisites(ice)[0] == fire, "Explicit prerequisites retained");
        var loadout = new PartyMemberProgress();
        var learned = new[] { "fire", "ice", "heal", "guard", "wind" };
        Check(loadout.TryEquip(0, "fire", learned, 4) && loadout.TryEquip(1, "ice", learned, 4), "Learned skills can be equipped");
        Check(!loadout.TryEquip(4, "wind", learned, 4) && !loadout.TryEquip(-1, "wind", learned, 4), "Four-slot boundaries enforced");
        Check(!loadout.TryEquip(0, "locked-skill", learned, 4) && loadout.equippedSkills[0] == "fire", "Locked skills cannot overwrite equipped skills");
        Check(loadout.TryEquip(1, "fire", learned, 4) && loadout.equippedSkills[0] == "ice" && loadout.equippedSkills[1] == "fire", "Moving equipped skill swaps slots without duplicates");
        Check(loadout.TryEquip(1, null, learned, 4) && loadout.equippedSkills[1] == "" && loadout.loadoutInitialized, "Unequip persists intentional empty slot");
        var otherMember = new PartyMemberProgress(); otherMember.TryEquip(0, "heal", learned, 4);
        Check(loadout.equippedSkills[0] == "ice" && otherMember.equippedSkills[0] == "heal", "Members keep independent loadouts");
        var memberA = new PartyMemberProgress { statPoints = 3 };
        var memberB = new PartyMemberProgress { statPoints = 3 };
        Check(memberA.TryAllocateStats(new[] { 0, 2, 0, 0, 0 }, 10) && memberA.statPoints == 1 && memberB.statPoints == 3 && memberB.atkBonus == 0, "Spending attributes affects only the selected member");
        Check(memberB.TryAllocateStats(new[] { 0, 0, 3, 0, 0 }, 10) && memberB.statPoints == 0 && memberA.statPoints == 1 && memberA.defBonus == 0, "Other member spends their own independent pool");
        memberA.GainXP(100);
        Check(memberA.statPoints == 4 && memberB.statPoints == 0, "Level-up attribute points belong to the member who levels");
        Console.WriteLine("PASS: " + checks + " campaign data regression checks");
    }
}
