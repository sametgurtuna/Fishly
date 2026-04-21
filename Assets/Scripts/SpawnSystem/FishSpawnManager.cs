using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FishSpawnManager : MonoBehaviour
{
    public static FishSpawnManager Instance { get; private set; }

    [Header("Spawn Settings")]
    public List<GameObject> fishPrefabs;
    public int maxFishCount = 20;

    [Header("Boundaries")]
    public Vector2 minBounds = new Vector2(-7.5f, -4f);
    public Vector2 maxBounds = new Vector2(7.5f, 4f);
    [Range(0.2f, 2.5f)] public float edgePadding = 1.0f;

    [Header("Interaction")]
    [Range(0.2f, 2f)] public float rightClickPickRadius = 0.9f;

    [Header("UI Prefab Setup")]
    public FishInventoryUIRoot uiPrefab;
    public Transform uiParentOverride;

    private readonly List<GameObject> activeFish = new List<GameObject>();
    private readonly Dictionary<int, int> inventoryFish = new Dictionary<int, int>();
    private const string SpawnSaveKey = "SpawnSystemSaveData";

    [Serializable]
    private class InventoryEntry
    {
        public int fishIndex;
        public int count;
    }

    [Serializable]
    private class SceneFishEntry
    {
        public int fishIndex;
        public Vector3 position;
        public int stage;
        public float stageTimerSeconds;
    }

    [Serializable]
    private class SpawnSystemSaveData
    {
        public List<InventoryEntry> inventory = new List<InventoryEntry>();
        public List<SceneFishEntry> sceneFish = new List<SceneFishEntry>();
    }

    private Camera mainCam;
    private FishInventoryUIRoot uiRoot;
    private GameObject selectedFish;
    private bool warnedLegacyApi;

    public bool IsBreedingSelectionMode => BreedingManager.Instance != null && BreedingManager.Instance.InSelectionMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        mainCam = Camera.main;
        EnsureEventSystem();
        EnsureUIRoot();

        bool loadedState = LoadSpawnState();
        if (!loadedState)
            CacheSceneFish();

        HideFishContextMenu();
        RefreshInventoryList();

        if (GameManager.Instance != null)
            GameManager.Instance.ScanSceneFish();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveSpawnState();
    }

    private void OnApplicationQuit()
    {
        SaveSpawnState();
    }

    private void Update()
    {
        if (IsBreedingSelectionMode)
        {
            HandleBreedingSelectionClick();
            return;
        }

        HandleRightClickContext();

        if (uiRoot != null && uiRoot.contextMenu != null && uiRoot.contextMenu.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            HideFishContextMenu();
    }

    public void AddFish(int fishIndex)
    {
        if (fishIndex < 0 || fishIndex >= fishPrefabs.Count) return;
        CacheSceneFish();
        if (activeFish.Count >= maxFishCount) return;

        Vector3 spawnPos = GetRandomSpawnPoint();
        GameObject newFish = Instantiate(fishPrefabs[fishIndex], spawnPos, Quaternion.identity);
        activeFish.Add(newFish);
        SyncEconomyAfterFishChange();
    }

    public void RemoveSpecificFish(GameObject fish)
    {
        RemoveFishFromScene(fish);
    }

    public void OpenRemovePanel()
    {
        WarnLegacyApiOnce();
    }

    public void OpenAddPanel()
    {
        WarnLegacyApiOnce();

        if (uiRoot != null && uiRoot.inventoryPanel != null)
        {
            uiRoot.inventoryPanel.SetActive(true);
            RefreshInventoryList();
        }
    }

    public void RefreshRemoveList()
    {
        WarnLegacyApiOnce();
    }

    public void ClosePanels()
    {
        WarnLegacyApiOnce();

        if (uiRoot != null)
        {
            HideFishContextMenu();
            if (uiRoot.inventoryPanel != null)
                uiRoot.inventoryPanel.SetActive(false);
        }
    }

    private void WarnLegacyApiOnce()
    {
        if (warnedLegacyApi) return;
        warnedLegacyApi = true;
        Debug.LogWarning("[FishSpawnManager] Legacy Add/Remove panel API is disabled. Use right-click context menu and inventory UI prefab flow.");
    }

    private void HandleRightClickContext()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        CacheSceneFish();

        GameObject fish = FindFishNearMouse();
        if (fish == null)
        {
            HideFishContextMenu();
            return;
        }

        selectedFish = fish;
        OpenFishContextMenu(Input.mousePosition);
    }

    private GameObject FindFishNearMouse()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return null;

        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;

        float bestDist = rightClickPickRadius;
        GameObject best = null;

        for (int i = activeFish.Count - 1; i >= 0; i--)
        {
            GameObject fish = activeFish[i];
            if (fish == null)
            {
                activeFish.RemoveAt(i);
                continue;
            }

            float dist = Vector2.Distance(mousePos, fish.transform.position);
            if (dist <= bestDist)
            {
                bestDist = dist;
                best = fish;
            }
        }

        return best;
    }

    private void CacheSceneFish()
    {
        FishAI[] sceneFish = FindObjectsOfType<FishAI>();
        for (int i = 0; i < sceneFish.Length; i++)
        {
            if (sceneFish[i] == null) continue;
            GameObject fishObj = sceneFish[i].gameObject;
            if (!activeFish.Contains(fishObj))
                activeFish.Add(fishObj);
        }
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void EnsureUIRoot()
    {
        uiRoot = FindObjectOfType<FishInventoryUIRoot>(true);

        if (uiRoot == null && uiPrefab != null)
        {
            Transform parent = uiParentOverride;
            if (parent == null)
            {
                Canvas canvas = FindObjectOfType<Canvas>();
                if (canvas != null)
                    parent = canvas.transform;
            }

            FishInventoryUIRoot instance = Instantiate(uiPrefab, parent);
            instance.name = uiPrefab.name;
            uiRoot = instance;
        }

        if (uiRoot == null)
        {
            uiRoot = FishInventoryUIRoot.CreateDefaultUI(uiParentOverride);
            if (uiRoot != null)
            {
                Debug.LogWarning("[FishSpawnManager] UI root was missing, so a default one was created at runtime.");
            }
        }

        if (uiRoot == null)
        {
            Debug.LogError("[FishSpawnManager] UI root could not be created.");
            return;
        }

        uiRoot.BindDefaultsIfNeeded();
        uiRoot.ForceBottomRightLayout();
        BindUIEvents();

        if (uiRoot.inventoryPanel != null)
            uiRoot.inventoryPanel.SetActive(false);
    }

    private void BindUIEvents()
    {
        if (uiRoot == null) return;

        if (uiRoot.addToInventoryButton != null)
        {
            uiRoot.addToInventoryButton.onClick.RemoveListener(OnAddToInventoryPressed);
            uiRoot.addToInventoryButton.onClick.AddListener(OnAddToInventoryPressed);
        }

        if (uiRoot.sellButton != null)
        {
            uiRoot.sellButton.onClick.RemoveListener(OnSellPressed);
            uiRoot.sellButton.onClick.AddListener(OnSellPressed);
        }

        if (uiRoot.inventoryToggleButton != null)
        {
            uiRoot.inventoryToggleButton.onClick.RemoveListener(ToggleInventoryPanel);
            uiRoot.inventoryToggleButton.onClick.AddListener(ToggleInventoryPanel);
        }

        if (uiRoot.breedButton != null)
        {
            uiRoot.breedButton.onClick.RemoveListener(OnBreedPressed);
            uiRoot.breedButton.onClick.AddListener(OnBreedPressed);
        }
    }

    private void OpenFishContextMenu(Vector2 screenPosition)
    {
        if (uiRoot == null || uiRoot.contextMenu == null || uiRoot.rootCanvas == null)
            return;

        UpdateContextMenuButtons(selectedFish);

        RectTransform canvasRT = uiRoot.rootCanvas.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT,
            screenPosition,
            uiRoot.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiRoot.rootCanvas.worldCamera,
            out Vector2 localPos
        );

        uiRoot.contextMenu.anchoredPosition = localPos;
        uiRoot.contextMenu.gameObject.SetActive(true);
    }

    private void UpdateContextMenuButtons(GameObject fish)
    {
        bool canBreed = IsBreedableFish(fish);

        if (uiRoot != null && uiRoot.breedButton != null)
            uiRoot.breedButton.gameObject.SetActive(canBreed);
    }

    private void HideFishContextMenu()
    {
        if (uiRoot != null && uiRoot.contextMenu != null)
            uiRoot.contextMenu.gameObject.SetActive(false);
    }

    private void OnAddToInventoryPressed()
    {
        if (selectedFish == null) return;

        int fishIndex = ResolveFishIndex(selectedFish);
        if (fishIndex < 0) return;

        AddFishToInventory(fishIndex, 1);
        RemoveFishFromScene(selectedFish);
        HideFishContextMenu();
        RefreshInventoryList();
    }

    private void OnSellPressed()
    {
        if (selectedFish == null) return;

        int fishIndex = ResolveFishIndex(selectedFish);
        if (fishIndex < 0) return;

        double sellValue = GetFishSellValue(selectedFish, fishIndex);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(sellValue);
            GameManager.Instance.SaveGame();
        }

        RemoveFishFromScene(selectedFish);
        HideFishContextMenu();
    }

    private void OnBreedPressed()
    {
        if (selectedFish == null || !IsBreedableFish(selectedFish)) return;

        if (BreedingManager.Instance != null)
            BreedingManager.Instance.StartSelection(selectedFish.GetComponent<FishAI>());

        HideFishContextMenu();
    }

    private void HandleBreedingSelectionClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        GameObject fish = FindFishNearMouse();
        if (fish == null) return;

        FishAI target = fish.GetComponent<FishAI>();
        if (target == null) return;

        if (BreedingManager.Instance != null)
            BreedingManager.Instance.TrySelectMate(target);
    }

    private double GetFishSellValue(GameObject fish, int fishIndex)
    {
        FishAI fishAI = fish.GetComponent<FishAI>();
        if (fishAI != null && fishAI.fishData != null)
            return fishAI.fishData.baseCost;

        if (fishIndex >= 0 && fishIndex < fishPrefabs.Count)
        {
            FishAI prefabAI = fishPrefabs[fishIndex].GetComponent<FishAI>();
            if (prefabAI != null && prefabAI.fishData != null)
                return prefabAI.fishData.baseCost;
        }

        return 0;
    }

    private int ResolveFishIndex(GameObject fish)
    {
        FishAI fishAI = fish.GetComponent<FishAI>();

        if (fishAI != null && fishAI.fishData != null)
        {
            for (int i = 0; i < fishPrefabs.Count; i++)
            {
                FishAI prefabAI = fishPrefabs[i].GetComponent<FishAI>();
                if (prefabAI != null && prefabAI.fishData == fishAI.fishData)
                    return i;
            }
        }

        string fishName = fish.name.Replace("(Clone)", "").Trim();
        for (int i = 0; i < fishPrefabs.Count; i++)
        {
            if (fishPrefabs[i] != null && fishPrefabs[i].name == fishName)
                return i;
        }

        return -1;
    }

    private bool IsBreedableFish(GameObject fish)
    {
        if (fish == null) return false;

        FishAI fishAI = fish.GetComponent<FishAI>();
        if (fishAI == null || fishAI.fishData == null) return false;

        FishLifeStage stage = fish.GetComponent<FishLifeStage>();
        if (stage != null && !stage.CanBreed) return false;

        return fishAI.fishData.canBreed;
    }

    private void RemoveFishFromScene(GameObject fish)
    {
        if (fish == null) return;

        activeFish.Remove(fish);
        fish.SetActive(false);
        Destroy(fish);
        if (selectedFish == fish)
            selectedFish = null;
        SyncEconomyAfterFishChange();
    }

    private void AddFishToInventory(int fishIndex, int amount)
    {
        if (amount <= 0) return;

        if (!inventoryFish.ContainsKey(fishIndex))
            inventoryFish[fishIndex] = 0;

        inventoryFish[fishIndex] += amount;
    }

    private bool ConsumeFishFromInventory(int fishIndex, int amount)
    {
        if (amount <= 0) return false;
        if (!inventoryFish.ContainsKey(fishIndex)) return false;
        if (inventoryFish[fishIndex] < amount) return false;

        inventoryFish[fishIndex] -= amount;
        if (inventoryFish[fishIndex] <= 0)
            inventoryFish.Remove(fishIndex);

        return true;
    }

    public bool TryDropFishFromInventory(int fishIndex, Vector2 screenPosition)
    {
        CacheSceneFish();
        if (activeFish.Count >= maxFishCount) return false;

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return false;
        if (!ConsumeFishFromInventory(fishIndex, 1)) return false;

        Vector3 world = mainCam.ScreenToWorldPoint(screenPosition);
        world.z = 0f;

        if (!IsInsideAquarium(world))
        {
            AddFishToInventory(fishIndex, 1);
            RefreshInventoryList();
            return false;
        }

        GameObject fish = Instantiate(fishPrefabs[fishIndex], world, Quaternion.identity);
        activeFish.Add(fish);
        RefreshInventoryList();
        SyncEconomyAfterFishChange();
        return true;
    }

    public Transform GetDragRoot()
    {
        if (uiRoot != null && uiRoot.dragRoot != null)
            return uiRoot.dragRoot;

        if (uiRoot != null)
            return uiRoot.transform;

        return transform;
    }

    private bool IsInsideAquarium(Vector3 worldPos)
    {
        float minX = minBounds.x + edgePadding;
        float maxX = maxBounds.x - edgePadding;
        float minY = minBounds.y + edgePadding;
        float maxY = maxBounds.y - edgePadding;

        return worldPos.x >= minX && worldPos.x <= maxX && worldPos.y >= minY && worldPos.y <= maxY;
    }

    private void SyncEconomyAfterFishChange()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.ScanSceneFish();
        GameManager.Instance.SaveGame();
        SaveSpawnState();
    }

    private void SaveSpawnState()
    {
        SpawnSystemSaveData data = new SpawnSystemSaveData();

        foreach (var kvp in inventoryFish)
        {
            if (kvp.Value <= 0) continue;
            data.inventory.Add(new InventoryEntry
            {
                fishIndex = kvp.Key,
                count = kvp.Value
            });
        }

        FishAI[] sceneFish = FindObjectsOfType<FishAI>();
        for (int i = 0; i < sceneFish.Length; i++)
        {
            FishAI fishAI = sceneFish[i];
            if (fishAI == null || fishAI.fishData == null) continue;

            int fishIndex = GetFishIndexByData(fishAI.fishData);
            if (fishIndex < 0) continue;

            FishLifeStage stage = fishAI.GetComponent<FishLifeStage>();
            FishLifeStage.Stage currentStage = stage != null ? stage.CurrentStage : FishLifeStage.Stage.Adult;
            float stageTimer = stage != null ? stage.RemainingStageTimeSeconds : 0f;

            data.sceneFish.Add(new SceneFishEntry
            {
                fishIndex = fishIndex,
                position = fishAI.transform.position,
                stage = (int)currentStage,
                stageTimerSeconds = stageTimer
            });
        }

        FishLifeStage[] lifeStages = FindObjectsOfType<FishLifeStage>();
        for (int i = 0; i < lifeStages.Length; i++)
        {
            FishLifeStage life = lifeStages[i];
            if (life == null || !life.IsEgg || life.fishData == null) continue;

            int fishIndex = GetFishIndexByData(life.fishData);
            if (fishIndex < 0) continue;

            data.sceneFish.Add(new SceneFishEntry
            {
                fishIndex = fishIndex,
                position = life.transform.position,
                stage = (int)FishLifeStage.Stage.Egg,
                stageTimerSeconds = life.RemainingStageTimeSeconds
            });
        }

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SpawnSaveKey, json);
        PlayerPrefs.Save();
    }

    private bool LoadSpawnState()
    {
        if (!PlayerPrefs.HasKey(SpawnSaveKey))
            return false;

        string json = PlayerPrefs.GetString(SpawnSaveKey, "");
        if (string.IsNullOrEmpty(json))
            return false;

        SpawnSystemSaveData data = JsonUtility.FromJson<SpawnSystemSaveData>(json);
        if (data == null)
            return false;

        float offlineElapsedSeconds = GetOfflineElapsedSeconds();

        ClearAllSceneCreatures();
        inventoryFish.Clear();

        if (data.inventory != null)
        {
            for (int i = 0; i < data.inventory.Count; i++)
            {
                InventoryEntry entry = data.inventory[i];
                if (entry == null) continue;
                if (entry.fishIndex < 0 || entry.fishIndex >= fishPrefabs.Count) continue;
                if (entry.count <= 0) continue;

                inventoryFish[entry.fishIndex] = entry.count;
            }
        }

        if (data.sceneFish != null)
        {
            for (int i = 0; i < data.sceneFish.Count; i++)
            {
                SceneFishEntry entry = data.sceneFish[i];
                if (entry == null) continue;
                RestoreSceneFish(entry, offlineElapsedSeconds);
            }
        }

        return true;
    }

    private void ClearAllSceneCreatures()
    {
        HashSet<GameObject> toDestroy = new HashSet<GameObject>();

        FishAI[] fishAI = FindObjectsOfType<FishAI>();
        for (int i = 0; i < fishAI.Length; i++)
        {
            if (fishAI[i] != null)
                toDestroy.Add(fishAI[i].gameObject);
        }

        FishLifeStage[] stageEntities = FindObjectsOfType<FishLifeStage>();
        for (int i = 0; i < stageEntities.Length; i++)
        {
            if (stageEntities[i] != null)
                toDestroy.Add(stageEntities[i].gameObject);
        }

        foreach (GameObject go in toDestroy)
        {
            if (go != null)
                Destroy(go);
        }

        activeFish.Clear();
        selectedFish = null;
    }

    private void RestoreSceneFish(SceneFishEntry entry, float offlineElapsedSeconds)
    {
        if (entry.fishIndex < 0 || entry.fishIndex >= fishPrefabs.Count)
            return;

        GameObject prefab = fishPrefabs[entry.fishIndex];
        if (prefab == null)
            return;

        FishAI prefabAI = prefab.GetComponent<FishAI>();
        FishData data = prefabAI != null ? prefabAI.fishData : null;

        FishLifeStage.Stage stage = (FishLifeStage.Stage)Mathf.Clamp(entry.stage, 0, 2);
        GameObject created = null;

        if (data != null && stage != FishLifeStage.Stage.Adult)
        {
            float remaining = entry.stageTimerSeconds - offlineElapsedSeconds;

            if (stage == FishLifeStage.Stage.Egg)
            {
                if (remaining > 0f)
                {
                    entry.stageTimerSeconds = remaining;
                }
                else
                {
                    float babyTotal = Mathf.Max(1f, data.growthMinutes * 60f);
                    float babyRemaining = babyTotal + remaining;
                    if (babyRemaining > 0f)
                    {
                        stage = FishLifeStage.Stage.Baby;
                        entry.stage = (int)FishLifeStage.Stage.Baby;
                        entry.stageTimerSeconds = babyRemaining;
                    }
                    else
                    {
                        stage = FishLifeStage.Stage.Adult;
                        entry.stage = (int)FishLifeStage.Stage.Adult;
                        entry.stageTimerSeconds = 0f;
                    }
                }
            }
            else if (stage == FishLifeStage.Stage.Baby)
            {
                if (remaining > 0f)
                {
                    entry.stageTimerSeconds = remaining;
                }
                else
                {
                    stage = FishLifeStage.Stage.Adult;
                    entry.stage = (int)FishLifeStage.Stage.Adult;
                    entry.stageTimerSeconds = 0f;
                }
            }
        }

        if (stage == FishLifeStage.Stage.Adult)
        {
            created = Instantiate(prefab, entry.position, Quaternion.identity);
            if (created.GetComponent<FishAI>() != null)
                activeFish.Add(created);
            return;
        }

        if (data == null)
        {
            created = Instantiate(prefab, entry.position, Quaternion.identity);
            if (created.GetComponent<FishAI>() != null)
                activeFish.Add(created);
            return;
        }

        if (stage == FishLifeStage.Stage.Egg)
        {
            if (data.eggPrefab != null)
                created = Instantiate(data.eggPrefab, entry.position, Quaternion.identity);
            else
                created = new GameObject((data.fishName ?? "Fish") + "_Egg");

            if (created.transform.position != entry.position)
                created.transform.position = entry.position;

            FishLifeStage life = created.GetComponent<FishLifeStage>();
            if (life == null)
                life = created.AddComponent<FishLifeStage>();

            life.InitAsEgg(data, data.incubationHours, data.growthMinutes);
            life.SetRemainingStageTime(entry.stageTimerSeconds);
            return;
        }

        if (stage == FishLifeStage.Stage.Baby)
        {
            if (data.babyPrefab != null)
                created = Instantiate(data.babyPrefab, entry.position, Quaternion.identity);
            else
                created = Instantiate(prefab, entry.position, Quaternion.identity);

            FishLifeStage life = created.GetComponent<FishLifeStage>();
            if (life == null)
                life = created.AddComponent<FishLifeStage>();

            life.InitAsBaby(data, data.growthMinutes, preserveScale: true);
            life.SetRemainingStageTime(entry.stageTimerSeconds);

            FishAI ai = created.GetComponent<FishAI>();
            if (ai != null)
                activeFish.Add(created);
        }
    }

    private int GetFishIndexByData(FishData data)
    {
        if (data == null) return -1;

        for (int i = 0; i < fishPrefabs.Count; i++)
        {
            if (fishPrefabs[i] == null) continue;
            FishAI ai = fishPrefabs[i].GetComponent<FishAI>();
            if (ai != null && ai.fishData == data)
                return i;
        }

        return -1;
    }

    private float GetOfflineElapsedSeconds()
    {
        string lastSaveStr = PlayerPrefs.GetString("LastSaveTime", "");
        if (string.IsNullOrEmpty(lastSaveStr))
            return 0f;

        if (!DateTime.TryParse(lastSaveStr, out DateTime lastSave))
            return 0f;

        TimeSpan elapsed = DateTime.Now - lastSave;
        return Mathf.Max(0f, (float)elapsed.TotalSeconds);
    }

    private void ToggleInventoryPanel()
    {
        if (uiRoot == null || uiRoot.inventoryPanel == null) return;

        bool newState = !uiRoot.inventoryPanel.activeSelf;
        uiRoot.inventoryPanel.SetActive(newState);

        if (newState)
            RefreshInventoryList();
    }

    private void RefreshInventoryList()
    {
        if (uiRoot == null || uiRoot.inventoryContent == null) return;

        Transform content = uiRoot.inventoryContent;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        bool hasAny = false;

        for (int i = 0; i < fishPrefabs.Count; i++)
        {
            if (!inventoryFish.TryGetValue(i, out int count) || count <= 0)
                continue;

            hasAny = true;
            CreateInventoryEntry(i, count);
        }

        if (!hasAny && uiRoot.emptyStateLabel != null)
        {
            GameObject emptyObj = Instantiate(uiRoot.emptyStateLabel.gameObject, content);
            emptyObj.SetActive(true);
        }
    }

    private void CreateInventoryEntry(int fishIndex, int count)
    {
        GameObject entryObj;

        if (uiRoot != null && uiRoot.inventoryItemPrefab != null)
        {
            entryObj = Instantiate(uiRoot.inventoryItemPrefab, uiRoot.inventoryContent);
        }
        else
        {
            entryObj = new GameObject("InventoryItem", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            entryObj.transform.SetParent(uiRoot.inventoryContent, false);
            entryObj.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.32f, 1f);
            entryObj.GetComponent<LayoutElement>().preferredHeight = 48f;
        }

        entryObj.name = $"InventoryFish_{fishIndex}";
        entryObj.SetActive(true);

        TMP_Text label = entryObj.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            GameObject textObj = new GameObject("Label", typeof(RectTransform));
            textObj.transform.SetParent(entryObj.transform, false);
            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(12f, 0f);
            textRT.offsetMax = new Vector2(-12f, 0f);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.fontSize = 20f;
            label = tmp;
        }

        string fishName = fishPrefabs[fishIndex] != null ? fishPrefabs[fishIndex].name : "Fish";
        label.text = fishName + " x" + count;

        InventoryDragItem dragItem = entryObj.GetComponent<InventoryDragItem>();
        if (dragItem == null)
            dragItem = entryObj.AddComponent<InventoryDragItem>();

        dragItem.Setup(this, fishIndex, label.text);
    }

    private Vector3 GetRandomSpawnPoint()
    {
        float x = UnityEngine.Random.Range(minBounds.x + edgePadding, maxBounds.x - edgePadding);
        float y = UnityEngine.Random.Range(minBounds.y + edgePadding, maxBounds.y - edgePadding);
        return new Vector3(x, y, 0f);
    }
}
