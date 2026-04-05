using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central economy manager for the idle aquarium game.
/// Handles currency, passive income, fish purchasing, price scaling,
/// offline earnings, and save/load via PlayerPrefs.
/// 
/// Uses Singleton pattern — access anywhere via GameManager.Instance.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  SINGLETON
    // ═══════════════════════════════════════════════════════════════

    public static GameManager Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    //  EVENTS (UI can subscribe to these)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Fired whenever gold changes. Parameter = new total gold.</summary>
    public event Action<double> OnGoldChanged;

    /// <summary>Fired whenever income per second changes.</summary>
    public event Action<double> OnIncomeChanged;

    /// <summary>Fired when a fish is successfully purchased. Param = FishData.</summary>
    public event Action<FishData> OnFishPurchased;

    // ═══════════════════════════════════════════════════════════════
    //  CURRENCY
    // ═══════════════════════════════════════════════════════════════

    [Header("Economy")]
    [Tooltip("Starting gold for a new game")]
    public double startingGold = 100.0;

    [Header("═══ CURRENT STATE (Read-Only) ═══")]
    [SerializeField, Tooltip("Current gold — read-only, do not edit")]
    private double _inspectorGold;

    [SerializeField, Tooltip("Current income per second")]
    private double _inspectorIncome;

    [SerializeField, Tooltip("Fish count in scene")]
    private int _inspectorFishCount;

    private double _gold;
    public double Gold
    {
        get => _gold;
        private set
        {
            _gold = value;
            OnGoldChanged?.Invoke(_gold);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  INCOME
    // ═══════════════════════════════════════════════════════════════

    [Header("Income Settings")]
    [Tooltip("How often income ticks (seconds)")]
    [Range(0.5f, 5f)] public float incomeTickInterval = 1.0f;

    [Tooltip("How often to rescan scene for new fish (seconds)")]
    [Range(1f, 10f)] public float sceneScanInterval = 3.0f;

    private float incomeTimer;
    private float sceneScanTimer;

    /// <summary>Total gold earned per second from all fish.</summary>
    public double TotalIncomePerSecond { get; private set; }

    /// <summary>Number of fish currently detected in the scene.</summary>
    public int SceneFishCount { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    //  FISH INVENTORY
    // ═══════════════════════════════════════════════════════════════

    [Header("Fish Catalog")]
    [Tooltip("All available fish types in the game (drag FishData assets here)")]
    public List<FishData> fishCatalog = new List<FishData>();

    /// <summary>
    /// How many of each fish type the player owns.
    /// Key = FishData instance, Value = count.
    /// </summary>
    private Dictionary<FishData, int> ownedFish = new Dictionary<FishData, int>();

    // ═══════════════════════════════════════════════════════════════
    //  GLOBAL MULTIPLIERS
    // ═══════════════════════════════════════════════════════════════

    [Header("Global Multipliers")]
    [Tooltip("Global income multiplier (from upgrades, boosts, etc.)")]
    public double globalIncomeMultiplier = 1.0;

    [Tooltip("Global cost reduction (0.9 = 10% cheaper)")]
    [Range(0.5f, 1f)] public float globalCostMultiplier = 1.0f;

    // ═══════════════════════════════════════════════════════════════
    //  OFFLINE EARNINGS
    // ═══════════════════════════════════════════════════════════════

    [Header("Offline Earnings")]
    [Tooltip("What percentage of income is earned while offline (0.5 = 50%)")]
    [Range(0f, 1f)] public float offlineEarningRate = 0.5f;

    [Tooltip("Maximum offline hours that count")]
    [Range(1f, 24f)] public float maxOfflineHours = 8f;

    /// <summary>Gold earned while offline (shown to player on return).</summary>
    public double LastOfflineEarnings { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    //  STATISTICS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Total gold earned across the entire game.</summary>
    public double TotalGoldEarned { get; private set; }

    /// <summary>Total number of fish purchased.</summary>
    public int TotalFishPurchased { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize inventory
        foreach (var fish in fishCatalog)
        {
            if (fish != null && !ownedFish.ContainsKey(fish))
                ownedFish[fish] = 0;
        }
    }

    private void Start()
    {
        LoadGame();
        ScanSceneFish();        // detect fish already in the scene
        RecalculateIncome();
        CalculateOfflineEarnings();
    }

    private void Update()
    {
        // Income tick
        incomeTimer += Time.deltaTime;
        if (incomeTimer >= incomeTickInterval)
        {
            incomeTimer -= incomeTickInterval;
            EarnIncome();
        }

        // Periodically rescan scene for newly spawned fish
        sceneScanTimer += Time.deltaTime;
        if (sceneScanTimer >= sceneScanInterval)
        {
            sceneScanTimer = 0f;
            ScanSceneFish();
        }

        // Sync inspector display fields
        _inspectorGold = _gold;
        _inspectorIncome = TotalIncomePerSecond;
        _inspectorFishCount = SceneFishCount;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveGame();
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    // ═══════════════════════════════════════════════════════════════
    //  PURCHASING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the current cost for the next fish of this type.
    /// Cost = baseCost × priceMultiplier ^ ownedCount × globalCostMultiplier
    /// </summary>
    public double GetFishCost(FishData fish)
    {
        int count = GetOwnedCount(fish);
        double cost = fish.baseCost * Math.Pow(fish.priceMultiplier, count);
        return cost * globalCostMultiplier;
    }

    /// <summary>Check if the player can afford a fish.</summary>
    public bool CanAfford(FishData fish)
    {
        return Gold >= GetFishCost(fish);
    }

    /// <summary>
    /// Attempt to purchase a fish. Returns true if successful.
    /// Deducts gold, increments count, recalculates income,
    /// fires OnFishPurchased event, and saves.
    /// </summary>
    public bool TryPurchaseFish(FishData fish)
    {
        double cost = GetFishCost(fish);
        if (Gold < cost)
        {
            Debug.Log($"[GameManager] Not enough gold! Need {cost:F0}, have {Gold:F0}");
            return false;
        }

        // Deduct cost
        Gold -= cost;

        // Add to inventory
        if (!ownedFish.ContainsKey(fish))
            ownedFish[fish] = 0;
        ownedFish[fish]++;

        TotalFishPurchased++;

        // Recalculate passive income
        RecalculateIncome();

        // Fire event (UI & spawner listen to this)
        OnFishPurchased?.Invoke(fish);

        Debug.Log($"[GameManager] Purchased {fish.fishName}! " +
                  $"Count: {ownedFish[fish]}, " +
                  $"Gold: {Gold:F0}, " +
                  $"Income/s: {TotalIncomePerSecond:F1}");

        // Auto-save after purchase
        SaveGame();

        return true;
    }

    /// <summary>How many of this fish type the player owns.</summary>
    public int GetOwnedCount(FishData fish)
    {
        return ownedFish.ContainsKey(fish) ? ownedFish[fish] : 0;
    }

    // ═══════════════════════════════════════════════════════════════
    //  SCENE FISH SCANNING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Scan the scene for all FishAI components and count their FishData.
    /// This is how the GameManager knows which fish are in the aquarium.
    /// </summary>
    public void ScanSceneFish()
    {
        FishAI[] allFish = FindObjectsOfType<FishAI>();
        SceneFishCount = allFish.Length;

        // Rebuild scene-based counts
        Dictionary<FishData, int> sceneCounts = new Dictionary<FishData, int>();

        foreach (FishAI fish in allFish)
        {
            if (fish.fishData != null)
            {
                if (!sceneCounts.ContainsKey(fish.fishData))
                    sceneCounts[fish.fishData] = 0;
                sceneCounts[fish.fishData]++;
            }
        }

        // Update ownedFish with scene counts
        // (scene fish are the source of truth)
        foreach (var kvp in sceneCounts)
        {
            ownedFish[kvp.Key] = kvp.Value;
        }

        RecalculateIncome();

        Debug.Log($"[GameManager] Scene scan: {SceneFishCount} fish found, Income: {TotalIncomePerSecond:F1}/s");
    }

    // ═══════════════════════════════════════════════════════════════
    //  INCOME
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Recalculate total income per second from all owned fish.
    /// Call this whenever fish are purchased or multipliers change.
    /// </summary>
    public void RecalculateIncome()
    {
        double totalIncome = 0;

        foreach (var kvp in ownedFish)
        {
            FishData fish = kvp.Key;
            int count = kvp.Value;

            if (fish != null && count > 0)
            {
                totalIncome += fish.baseIncome * count;
            }
        }

        TotalIncomePerSecond = totalIncome * globalIncomeMultiplier;
        OnIncomeChanged?.Invoke(TotalIncomePerSecond);
    }

    /// <summary>
    /// Get the income contribution of a specific fish type.
    /// </summary>
    public double GetFishIncome(FishData fish)
    {
        int count = GetOwnedCount(fish);
        return fish.baseIncome * count * globalIncomeMultiplier;
    }

    private void EarnIncome()
    {
        if (TotalIncomePerSecond <= 0) return;

        double earned = TotalIncomePerSecond * incomeTickInterval;
        Gold += earned;
        TotalGoldEarned += earned;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GOLD UTILITIES
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Add gold directly (from tapping, bonuses, rewards, etc.)</summary>
    public void AddGold(double amount)
    {
        if (amount <= 0) return;
        Gold += amount;
        TotalGoldEarned += amount;
    }

    /// <summary>Try to spend gold. Returns false if insufficient.</summary>
    public bool TrySpendGold(double amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  OFFLINE EARNINGS
    // ═══════════════════════════════════════════════════════════════

    private void CalculateOfflineEarnings()
    {
        string lastSaveStr = PlayerPrefs.GetString("LastSaveTime", "");
        if (string.IsNullOrEmpty(lastSaveStr))
        {
            LastOfflineEarnings = 0;
            return;
        }

        if (DateTime.TryParse(lastSaveStr, out DateTime lastSave))
        {
            TimeSpan elapsed = DateTime.Now - lastSave;
            double offlineSeconds = elapsed.TotalSeconds;

            // Cap offline time
            double maxSeconds = maxOfflineHours * 3600;
            offlineSeconds = Math.Min(offlineSeconds, maxSeconds);

            // Only count if at least 60 seconds have passed
            if (offlineSeconds >= 60)
            {
                LastOfflineEarnings = TotalIncomePerSecond * offlineSeconds * offlineEarningRate;
                Gold += LastOfflineEarnings;
                TotalGoldEarned += LastOfflineEarnings;

                Debug.Log($"[GameManager] Offline earnings: {LastOfflineEarnings:F0} gold " +
                          $"({elapsed.Hours}h {elapsed.Minutes}m away)");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SAVE / LOAD (PlayerPrefs — simple & reliable)
    // ═══════════════════════════════════════════════════════════════

    public void SaveGame()
    {
        // Gold & stats
        PlayerPrefs.SetString("Gold", Gold.ToString("R"));
        PlayerPrefs.SetString("TotalGoldEarned", TotalGoldEarned.ToString("R"));
        PlayerPrefs.SetInt("TotalFishPurchased", TotalFishPurchased);

        // Global multipliers
        PlayerPrefs.SetFloat("GlobalIncomeMultiplier", (float)globalIncomeMultiplier);
        PlayerPrefs.SetFloat("GlobalCostMultiplier", globalCostMultiplier);

        // Fish inventory
        for (int i = 0; i < fishCatalog.Count; i++)
        {
            if (fishCatalog[i] != null)
            {
                string key = $"Fish_{fishCatalog[i].fishName}";
                int count = GetOwnedCount(fishCatalog[i]);
                PlayerPrefs.SetInt(key, count);
            }
        }

        // Timestamp for offline earnings
        PlayerPrefs.SetString("LastSaveTime", DateTime.Now.ToString("O"));

        PlayerPrefs.Save();
        Debug.Log("[GameManager] Game saved.");
    }

    public void LoadGame()
    {
        // Gold
        string goldStr = PlayerPrefs.GetString("Gold", "");
        if (!string.IsNullOrEmpty(goldStr) && double.TryParse(goldStr, out double savedGold))
            _gold = savedGold;
        else
            _gold = startingGold;

        // Stats
        string totalStr = PlayerPrefs.GetString("TotalGoldEarned", "0");
        if (double.TryParse(totalStr, out double savedTotal))
            TotalGoldEarned = savedTotal;

        TotalFishPurchased = PlayerPrefs.GetInt("TotalFishPurchased", 0);

        // Global multipliers
        globalIncomeMultiplier = PlayerPrefs.GetFloat("GlobalIncomeMultiplier", 1f);
        globalCostMultiplier = PlayerPrefs.GetFloat("GlobalCostMultiplier", 1f);

        // Fish inventory
        for (int i = 0; i < fishCatalog.Count; i++)
        {
            if (fishCatalog[i] != null)
            {
                string key = $"Fish_{fishCatalog[i].fishName}";
                int count = PlayerPrefs.GetInt(key, 0);
                ownedFish[fishCatalog[i]] = count;
            }
        }

        // Fire initial UI update
        OnGoldChanged?.Invoke(_gold);

        Debug.Log($"[GameManager] Game loaded. Gold: {_gold:F0}");
    }

    /// <summary>Delete all save data and reset to defaults.</summary>
    public void ResetSaveData()
    {
        PlayerPrefs.DeleteAll();
        _gold = startingGold;
        TotalGoldEarned = 0;
        TotalFishPurchased = 0;
        globalIncomeMultiplier = 1.0;
        globalCostMultiplier = 1f;

        foreach (var fish in fishCatalog)
        {
            if (fish != null)
                ownedFish[fish] = 0;
        }

        RecalculateIncome();
        OnGoldChanged?.Invoke(_gold);

        Debug.Log("[GameManager] Save data reset.");
    }

    // ═══════════════════════════════════════════════════════════════
    //  NUMBER FORMATTING (for UI display)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Format large numbers for display: 1000 → "1.0K", 1500000 → "1.5M", etc.
    /// Common pattern in idle games.
    /// </summary>
    public static string FormatNumber(double value)
    {
        if (value < 0) return "-" + FormatNumber(-value);

        if (value < 1_000)
            return value.ToString("F0");
        if (value < 1_000_000)
            return (value / 1_000).ToString("F1") + "K";
        if (value < 1_000_000_000)
            return (value / 1_000_000).ToString("F1") + "M";
        if (value < 1_000_000_000_000)
            return (value / 1_000_000_000).ToString("F1") + "B";
        if (value < 1_000_000_000_000_000)
            return (value / 1_000_000_000_000).ToString("F1") + "T";

        return value.ToString("E2");
    }

    /// <summary>Format with currency symbol for UI.</summary>
    public static string FormatGold(double value)
    {
        return "🪙 " + FormatNumber(value);
    }
}
