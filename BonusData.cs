using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// Data for the Bonus Shop, taken from the registry extracted out of Registry.as.
// Order is exactly the order BonusShopEngine.as pushes slots in.
public sealed class BonusItemState
{
    public string Id = "";
    public string Name = "";
    public int MaxLevel;
    public int Level;
    public int BonusUnlockedLevel;
    public bool Locked;
    public string Description = "";
    public int OverLevelsAvailable;
    public int UpgradeLimit => MaxLevel + OverLevelsAvailable;
}

public sealed class DisplayStat
{
    public int Index;
    public string Name = "";
    public string StatKey = "";
}

public static class BonusData
{
    // BonusShopEngine.as:61-100 - slot creation order, which is also display order.
    private static readonly string[] Order =
    {
        "bonusMoney", "bonusInterest", "bonusLaunchPower", "bonusStagePower", "bonusBoostPower",
        "bonusStageFuel", "bonusBoostFuel", "bonusDrag", "bonusWind", "bonusImpact",
        "bonusBetterStart", "bonusLessObstacles", "bonusOverBodiesAndLaunchers",
        "bonusOverStages", "bonusOverBoosts", "bonusOver", "bonusSpeed", "bonusGravity",
        "bonusBpPerDay", "bonusVCR"
    };

    private static List<OriginalEntry>? _entries;
    private static Dictionary<string, OriginalEntry>? _byId;
    private static readonly Dictionary<string, List<DisplayStat>> _displayCache = new();

    private static void EnsureLoaded()
    {
        if (_entries != null) return;
        _entries = OriginalCatalog.Load();
        _byId = _entries.ToDictionary(e => e.id, e => e);
    }

    public static OriginalEntry? Entry(string id)
    {
        EnsureLoaded();
        return _byId!.TryGetValue(id, out var e) ? e : null;
    }

    // RegistryEntry.setDisplayStat(index, name, stat, decimals) - parsed back out of the
    // preserved source block so the stat labels are the game's own strings.
    public static List<DisplayStat> DisplayStats(string id)
    {
        if (_displayCache.TryGetValue(id, out var cached)) return cached;
        var list = new List<DisplayStat>();
        var entry = Entry(id);
        if (entry?.source != null)
        {
            foreach (Match m in Regex.Matches(entry.source,
                "setDisplayStat\\((\\d+),\\s*\"([^\"]+)\",\\s*_loc1_\\.(\\w+)"))
            {
                list.Add(new DisplayStat
                {
                    Index = int.Parse(m.Groups[1].Value),
                    Name = m.Groups[2].Value,
                    StatKey = m.Groups[3].Value
                });
            }
        }
        list.Sort((a, b) => a.Index.CompareTo(b.Index));
        _displayCache[id] = list;
        return list;
    }

    public static List<BonusItemState> Load()
    {
        EnsureLoaded();
        var result = new List<BonusItemState>();

        foreach (var id in Order)
        {
            var entry = Entry(id);
            if (entry == null) continue;
            int max = 0;
            int.TryParse(entry.maxLevel, out max);
            PortProfile.State.BonusLevels.TryGetValue(id, out int level);
            PortProfile.State.BonusUnlockedLevels.TryGetValue(id, out int unlockedLevel);
            int overLevels = OverLevelsAvailable(id,
                PortProfile.State.BonusLevels.TryGetValue("bonusOver", out int savedOverLevel) ? savedOverLevel : 0);
            var item = new BonusItemState
            {
                Id = id,
                Name = entry.name,
                MaxLevel = max,
                Level = Math.Clamp(level, 0, max + overLevels),
                BonusUnlockedLevel = Math.Clamp(unlockedLevel, 0, max + overLevels),
                OverLevelsAvailable = overLevels,
            };
            if (entry.descriptions != null && entry.descriptions.Count > 0)
            {
                var d = entry.descriptions.FirstOrDefault(x => x.level == item.Level.ToString())
                        ?? entry.descriptions[0];
                item.Description = d.text;
            }
            result.Add(item);
        }
        return result;
    }

    public static int OverLevelsAvailable(string id, int overBonusLevel)
    {
        // GameStateItem.overLevelsAvailable: the four bonus-over upgrades only
        // receive their own extra levels after the paid upgrade is purchased.
        if (id == "bonusOverBodiesAndLaunchers" || id == "bonusOverStages" ||
            id == "bonusOverBoosts" || id == "bonusOver") return 0;
        var overBonus = Entry("bonusOver");
        if (overBonus == null || !overBonus.stats.ContainsKey("statPower")) return 0;
        try { return Math.Max(0, (int)Math.Floor(overBonus.Value("statPower", overBonusLevel))); }
        catch (ArgumentOutOfRangeException) { return 0; }
        catch (NotSupportedException) { return 0; }
    }

    public static bool TryCost(BonusItemState item, int level, out double cost)
    {
        var entry = Entry(item.Id);
        cost = 0;
        if (entry == null || !entry.stats.ContainsKey("statCost")) return false;
        try
        {
            cost = OriginalStatRules.Cost(entry, level);
            return double.IsFinite(cost) && cost >= 0;
        }
        catch (ArgumentOutOfRangeException) { return false; }
        catch (NotSupportedException) { return false; }
    }

    public static void UpdateDescription(BonusItemState item)
    {
        var entry = Entry(item.Id);
        if (entry?.descriptions == null || entry.descriptions.Count == 0) return;
        var description = entry.descriptions.FirstOrDefault(x => x.level == item.Level.ToString())
                          ?? entry.descriptions[0];
        item.Description = description.text;
    }

    // RegistryEntry.getDisplayStatString(number, level) formatting rules.
    public static string StatValue(string id, string statKey, int level)
    {
        var entry = Entry(id);
        if (entry == null) return "-";
        try
        {
            double v = entry.Value(statKey, level);
            if (double.IsNaN(v)) return "-";
            if (v >= 100) return v.ToString("N0");
            if (v >= 10) return v.ToString("N1");
            return v.ToString("N2");
        }
        catch
        {
            // StatFlat / StatCameraZoom / StatPowerAtWeight carry different storage shapes.
            return "-";
        }
    }
}
