using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Akvaryumdaki balık listesini ve ekleme butonlarını yönetir.
///
/// Hierarchy örneği:
/// AquariumUI (bu script)
///  ├── FishCountText     (TextMeshProUGUI)  → "3 / 20"
///  ├── AvailablePanel
///  │    └── AvailableContent (LayoutGroup)  → eklenebilir balık butonları
///  └── ActivePanel
///       └── ActiveContent   (LayoutGroup)  → aktif balık satırları
/// </summary>
public class AquariumUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private AquariumManager manager;

    [Header("Balık Sayacı")]
    [SerializeField] private TextMeshProUGUI fishCountText;

    [Header("Eklenebilir Balıklar")]
    [SerializeField] private Transform availableContent;
    [SerializeField] private GameObject availableItemPrefab; // Button + balık adı

    [Header("Aktif Balıklar")]
    [SerializeField] private Transform activeContent;
    [SerializeField] private GameObject activeItemPrefab;   // Satır + çarpı butonu

    // ── Unity ────────────────────────────────────────────────────

    private void Start()
    {
        if (manager == null)
        {
            Debug.LogError("[AquariumUI] AquariumManager atanmamış.");
            return;
        }

        manager.OnFishListChanged += RefreshActiveList;

        BuildAvailableList();
        RefreshActiveList();
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.OnFishListChanged -= RefreshActiveList;
    }

    // ── Eklenebilir Balıklar ─────────────────────────────────────

    private void BuildAvailableList()
    {
        ClearChildren(availableContent);

        foreach (GameObject prefab in manager.availableFish)
        {
            if (prefab == null) continue;

            // Balık adını FishAI üzerindeki FishData'dan okuyoruz
            string fishName = GetFishName(prefab);

            GameObject item = Instantiate(availableItemPrefab, availableContent);

            // Butona balık adını yaz
            TextMeshProUGUI label = item.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = fishName;

            // Butona tıklanınca o prefabı ekle
            Button btn = item.GetComponentInChildren<Button>();
            if (btn != null)
            {
                GameObject capturedPrefab = prefab; // closure için
                btn.onClick.AddListener(() => OnAddFishClicked(capturedPrefab));
            }
        }
    }

    private void OnAddFishClicked(GameObject prefab)
    {
        bool success = manager.AddFish(prefab);
        if (!success)
        {
            Debug.Log("[AquariumUI] Balık eklenemedi (limit dolu olabilir).");
            // İstersen burada bir doluluk uyarısı gösterebilirsin
        }
    }

    // ── Aktif Balık Listesi ──────────────────────────────────────

    private void RefreshActiveList()
    {
        ClearChildren(activeContent);
        UpdateFishCountText();

        foreach (FishInstance fish in manager.ActiveFish)
        {
            GameObject item = Instantiate(activeItemPrefab, activeContent);

            // Balık adını satıra yaz
            TextMeshProUGUI label = item.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = fish.Data != null ? fish.Data.fishName : fish.gameObject.name;

            // Çarpı butonuna tıklanınca o balığı kaldır
            Button removeBtn = item.GetComponentInChildren<Button>();
            if (removeBtn != null)
            {
                FishInstance capturedFish = fish; // closure için
                removeBtn.onClick.AddListener(() => capturedFish.RequestRemove());
            }
        }
    }

    private void UpdateFishCountText()
    {
        if (fishCountText != null)
            fishCountText.text = $"{manager.CurrentCount} / {manager.MaxCount}";
    }

    // ── Yardımcı ─────────────────────────────────────────────────

    /// <summary>
    /// Prefabdaki FishAI'dan fishData.fishName'i okur.
    /// FishData'da fishName field'ı yoksa prefab adını döner.
    /// </summary>
    private static string GetFishName(GameObject prefab)
    {
        FishAI ai = prefab.GetComponent<FishAI>();
        if (ai != null && ai.fishData != null)
            return ai.fishData.fishName;

        return prefab.name;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform child in parent)
            Destroy(child.gameObject);
    }
}