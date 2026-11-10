using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

// Local-only state model for the Godot port. Field names and defaults follow
// src.system.ProfileState and src.system.GameState; no Steam account is used.
public sealed class PortGameSlot
{
    public string GameMode { get; set; } = "Empty";
    public double StoryModeSpaceLevel { get; set; } = 200000;
    public int Day { get; set; } = 1;
    public double Cash { get; set; }
    public string BodyId { get; set; } = "emptyBody";
    public int BodyLevel { get; set; }
    public string LauncherId { get; set; } = "emptyLauncher";
    public int LauncherLevel { get; set; }
    public string RudderId { get; set; } = "simpleRudder";
    public int RudderLevel { get; set; }
    public string[] StageIds { get; set; } = new[] { "emptyStage", "emptyStage", "emptyStage", "emptyStage" };
    public int[] StageLevels { get; set; } = new[] { 0, 0, 0, 0 };
    public string[] BoostIds { get; set; } = new[] { "emptyBoost", "emptyBoost", "emptyBoost", "emptyBoost" };
    public int[] BoostLevels { get; set; } = new[] { 0, 0, 0, 0 };
    public double BestAltitude { get; set; }
    public double BestDuration { get; set; }
    public double BestSpeed { get; set; }
    public double BestBonusMoney { get; set; }
    public double BestInterestEarnings { get; set; }
    public List<double> PreviousDaysScores { get; set; } = new();

    public bool IsEmpty => GameMode == "Empty";

    public static PortGameSlot NewGame(string mode, int cashMedals, double storyGoal)
    {
        return new PortGameSlot
        {
            GameMode = mode,
            StoryModeSpaceLevel = storyGoal,
            Day = 1,
            Cash = cashMedals switch { 1 => 15, 2 => 20, 3 => 30, 4 => 50, _ => 10 },
            BodyId = "emptyBody",
            LauncherId = "emptyLauncher",
            RudderId = "simpleRudder",
            StageIds = new[] { "emptyStage", "emptyStage", "emptyStage", "emptyStage" },
            StageLevels = new[] { 0, 0, 0, 0 },
            BoostIds = new[] { "emptyBoost", "emptyBoost", "emptyBoost", "emptyBoost" },
            BoostLevels = new[] { 0, 0, 0, 0 }
        };
    }

    public void Normalize()
    {
        if (StageIds == null || StageIds.Length != 4) StageIds = new[] { "emptyStage", "emptyStage", "emptyStage", "emptyStage" };
        if (StageLevels == null || StageLevels.Length != 4) StageLevels = new[] { 0, 0, 0, 0 };
        if (BoostIds == null || BoostIds.Length != 4) BoostIds = new[] { "emptyBoost", "emptyBoost", "emptyBoost", "emptyBoost" };
        if (BoostLevels == null || BoostLevels.Length != 4) BoostLevels = new[] { 0, 0, 0, 0 };
        Day = Math.Max(1, Day);
        Cash = Math.Max(0, Cash);
        StoryModeSpaceLevel = Math.Clamp(StoryModeSpaceLevel, 200000, 4000000);
        PreviousDaysScores ??= new();
    }
}

public sealed class PortProfileState
{
    public bool ModeUnlockedClassic { get; set; }
    public bool ModeUnlockedPayload { get; set; }
    public bool ModeUnlockedSandbox { get; set; }
    public double BonusPoints { get; set; }
    public int Sardines { get; set; } = 350;
    public int ConsumableEarninator { get; set; }
    public int ConsumableFloatifier { get; set; }
    public int ConsumableSpeedifier { get; set; }
    public bool BoughtItems { get; set; }
    public bool BoughtUpgrades { get; set; }
    public bool BoughtCheats { get; set; }
    public bool BoughtTimeWarp { get; set; }
    public bool BoughtBundle { get; set; }
    public Dictionary<string, int> BonusLevels { get; set; } = new();
    public Dictionary<string, int> BonusUnlockedLevels { get; set; } = new();
    public PortGameSlot[] SaveSlots { get; set; } = Enumerable.Range(0, 6).Select(_ => new PortGameSlot()).ToArray();
    public int CurrentSlotIndex { get; set; }
    public bool TutorialBuyLauncher { get; set; }
    public bool TutorialStages { get; set; }
    public Dictionary<string, int> Medals { get; set; } = new();
    public int TotalGamesStarted { get; set; }
    public int TotalFrequentBuyer { get; set; }
    public int TotalDays { get; set; }
    public double TotalCash { get; set; }
    public double TotalAltitude { get; set; }
    public double TotalFlightTime { get; set; }
    public double TotalHorizontalDistance { get; set; }
    public int TotalObstaclesHit { get; set; }
    public double BestAltitude { get; set; }
    public double BestAltitudeNoBody { get; set; }
    public double BestFlightTime { get; set; }
    public double BestSpeed { get; set; }
    public double BestHorizontalDistance { get; set; }
    public int BestObstaclesHit { get; set; }
    public double BestCash { get; set; }
    public int BestDays { get; set; }

    public void Normalize()
    {
        if (SaveSlots == null || SaveSlots.Length != 6)
        {
            var old = SaveSlots ?? Array.Empty<PortGameSlot>();
            SaveSlots = Enumerable.Range(0, 6).Select(i => i < old.Length ? old[i] ?? new PortGameSlot() : new PortGameSlot()).ToArray();
        }
        foreach (var slot in SaveSlots) slot.Normalize();
        CurrentSlotIndex = Math.Clamp(CurrentSlotIndex, 0, SaveSlots.Length - 1);
        BonusPoints = Math.Max(0, BonusPoints);
        Sardines = Math.Max(0, Sardines);
        BonusLevels ??= new Dictionary<string, int>();
        BonusUnlockedLevels ??= new Dictionary<string, int>();
        Medals ??= new Dictionary<string, int>();
    }
}

public static class PortProfile
{
    private const string SavePath = "user://ltf3-port-profile.json";
    private const string CorruptSaveBackupPath = "user://ltf3-port-profile.corrupt.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true, WriteIndented = true };
    private static PortProfileState? _state;
    private static bool _corruptSaveFound;

    // A navigation-only flag. It is intentionally not persisted across launches.
    public static bool ResumeEquipmentShop { get; set; }
    public static bool BonusShopReturnToEquipmentShop { get; set; }
    public static bool BlackMarketReturnToEquipmentShop { get; set; }

    public static PortProfileState State
    {
        get
        {
            if (_state != null) return _state;
            _state = LoadState();
            return _state;
        }
    }

    public static PortGameSlot CurrentSlot => State.SaveSlots[State.CurrentSlotIndex];

    public static void SelectSlot(int index)
    {
        State.CurrentSlotIndex = Math.Clamp(index, 0, 5);
        Save();
    }

    public static void StartNewGame(int index, string mode, double storyGoal = 200000)
    {
        if (mode != "Story Mode" && mode != "Classic Mode" && mode != "Payload Mode" && mode != "Sandbox")
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode must match a stock Learn to Fly 3 mode.");
        State.CurrentSlotIndex = Math.Clamp(index, 0, 5);
        if (!State.SaveSlots[State.CurrentSlotIndex].IsEmpty)
            throw new InvalidOperationException("Refusing to overwrite a non-empty slot during new-game setup.");
        State.SaveSlots[State.CurrentSlotIndex] = PortGameSlot.NewGame(mode, State.Medals.GetValueOrDefault("medalCashTotal"), storyGoal);
        State.TotalGamesStarted++;
        Save();
    }

    public static bool TryBuyCoil()
    {
        var slot = CurrentSlot;
        if (slot.GameMode != "Story Mode" || slot.LauncherId != "emptyLauncher") return false;
        var entries = OriginalCatalog.Load();
        var coil = entries.Single(e => e.id == "coil");
        var old = entries.Single(e => e.id == slot.LauncherId);
        double price = OriginalStatRules.Cost(coil, 0) - OriginalStatRules.Cost(old, slot.LauncherLevel);
        if (slot.Cash < price) return false;
        return Persist(() =>
        {
            slot.Cash -= price;
            slot.LauncherId = coil.id;
            slot.LauncherLevel = 0;
            State.TotalFrequentBuyer++;
            CheckMedal(entries, "medalFrequentBuyer", State.TotalFrequentBuyer);
        });
    }

    public static bool TryUpgradeCoil()
    {
        var slot = CurrentSlot;
        if (slot.GameMode != "Story Mode" || slot.LauncherId != "coil") return false;
        var entries = OriginalCatalog.Load();
        var coil = entries.Single(e => e.id == "coil");
        if (slot.LauncherLevel >= coil.MaxLevelInt) return false;
        double price = OriginalStatRules.Cost(coil, slot.LauncherLevel + 1);
        if (slot.Cash < price) return false;
        return Persist(() =>
        {
            slot.Cash -= price;
            slot.LauncherLevel++;
            State.TotalFrequentBuyer++;
            CheckMedal(entries, "medalFrequentBuyer", State.TotalFrequentBuyer);
        });
    }

    public static bool CompleteCoilDay(int slotIndex, int day, CoilDayStats stats, CoilDayReward reward)
    {
        if (State.CurrentSlotIndex != slotIndex || CurrentSlot.Day != day) return false;
        var slot = CurrentSlot;
        var entries = OriginalCatalog.Load();
        return Persist(() =>
        {
            double distance = Math.Max(Math.Abs(stats.MaxDistanceLeft), stats.MaxDistanceRight);
            slot.BestAltitude = Math.Max(slot.BestAltitude, stats.MaxAltitude);
            slot.BestDuration = Math.Max(slot.BestDuration, stats.FlightTimeMs);
            slot.BestSpeed = Math.Max(slot.BestSpeed, stats.MaxSpeed);
            slot.BestBonusMoney = Math.Max(slot.BestBonusMoney, reward.BonusEarnings);
            slot.BestInterestEarnings = Math.Max(slot.BestInterestEarnings, reward.Interest);
            State.BestDays = Math.Max(State.BestDays, day);
            State.BestCash = Math.Max(State.BestCash, slot.Cash);
            State.BestAltitude = Math.Max(State.BestAltitude, stats.MaxAltitude);
            State.BestAltitudeNoBody = Math.Max(State.BestAltitudeNoBody, stats.MaxAltitude);
            State.BestFlightTime = Math.Max(State.BestFlightTime, stats.FlightTimeMs);
            State.BestSpeed = Math.Max(State.BestSpeed, stats.MaxSpeed);
            State.BestHorizontalDistance = Math.Max(State.BestHorizontalDistance, distance);
            State.BestObstaclesHit = Math.Max(State.BestObstaclesHit, stats.ObstaclesHit);
            State.TotalAltitude += stats.MaxAltitude;
            State.TotalFlightTime += stats.FlightTimeMs;
            State.TotalHorizontalDistance += distance;
            State.TotalObstaclesHit += stats.ObstaclesHit;
            CheckMedal(entries, "medalSpeed", stats.MaxSpeed);
            CheckMedal(entries, "medalAltitude", stats.MaxAltitude);
            CheckMedal(entries, "medalFlightTime", stats.FlightTimeMs / 1000);
            CheckMedal(entries, "medalHorizontalDistance", distance);
            CheckMedal(entries, "medalBasicBody", stats.MaxAltitude);
            CheckMedal(entries, "medalDays", State.TotalDays);
            CheckMedal(entries, "medalAltitudeTotal", State.TotalAltitude);
            CheckMedal(entries, "medalFlightTimeTotal", State.TotalFlightTime / 1000);
            CheckMedal(entries, "medalCashTotal", State.TotalCash);
            slot.Cash += reward.Cash;
            State.TotalCash += reward.Cash;
            State.BonusPoints += reward.BonusPoints;
            State.TotalDays++;
            while (slot.PreviousDaysScores.Count < day) slot.PreviousDaysScores.Add(0);
            slot.PreviousDaysScores[day - 1] = stats.MaxAltitude;
            slot.Day++;
        });
    }

    private static void CheckMedal(List<OriginalEntry> entries, string id, double score)
    {
        int level = State.Medals.GetValueOrDefault(id);
        var entry = entries.FirstOrDefault(e => e.id == id);
        if (entry != null && level < 4 && score >= entry.Value("statCost", level))
            State.Medals[id] = level + 1;
    }

    private static bool Persist(Action update)
    {
        string old = JsonSerializer.Serialize(State, JsonOptions);
        try
        {
            update();
            Save();
            return true;
        }
        catch (Exception error)
        {
            _state = JsonSerializer.Deserialize<PortProfileState>(old, JsonOptions)!;
            GD.PushError(error.ToString());
            return false;
        }
    }

    public static void Save()
    {
        State.Normalize();
        if (_corruptSaveFound)
        {
            if (!FileAccess.FileExists(CorruptSaveBackupPath))
            {
                using var original = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
                using var backup = FileAccess.Open(CorruptSaveBackupPath, FileAccess.ModeFlags.Write);
                if (original == null || backup == null)
                    throw new InvalidOperationException("The unreadable local profile could not be backed up; refusing to overwrite it.");
                backup.StoreBuffer(original.GetBuffer(checked((long)original.GetLength())));
            }
            _corruptSaveFound = false;
        }
        string path = ProjectSettings.GlobalizePath(SavePath);
        string pending = path + ".tmp";
        System.IO.File.WriteAllText(pending, JsonSerializer.Serialize(State, JsonOptions));
        System.IO.File.Move(pending, path, overwrite: true);
    }

    private static PortProfileState LoadState()
    {
        if (FileAccess.FileExists(SavePath))
        {
            try
            {
                using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
                var loaded = JsonSerializer.Deserialize<PortProfileState>(file!.GetAsText(), JsonOptions);
                if (loaded != null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
            catch (Exception error)
            {
                _corruptSaveFound = true;
                GD.PushWarning($"Port profile could not be read; changes will preserve the unreadable copy at {CorruptSaveBackupPath}. {error.Message}");
            }
        }
        var fresh = new PortProfileState();
        fresh.Normalize();
        return fresh;
    }
}
