using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class FishSpawnManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    public List<GameObject> fishPrefabs;
    public int maxFishCount = 20;

    [Header("Boundaries")]
    public Vector2 minBounds = new Vector2(-7.5f, -4f);
    public Vector2 maxBounds = new Vector2(7.5f, 4f);
    [Range(0.2f, 2.5f)] public float edgePadding = 1.0f;

    [Header("UI References")]
    public GameObject addPanel;
    public GameObject removePanel;
    public Transform addContentParent;
    public Transform removeContentParent;
    public GameObject uiButtonPrefab;

    private List<GameObject> activeFish = new List<GameObject>();

    void Start()
    {
        addPanel.SetActive(false);
        removePanel.SetActive(false);
        PrepareAddList();
    }

    // --- EKLEME PANELÝ HAZIRLIÐI ---
    void PrepareAddList()
    {
        foreach (Transform child in addContentParent) Destroy(child.gameObject);

        for (int i = 0; i < fishPrefabs.Count; i++)
        {
            int index = i;
            GameObject btnObj = Instantiate(uiButtonPrefab, addContentParent);
            btnObj.GetComponentInChildren<TMP_Text>().text = fishPrefabs[i].name + " Ekle";
            btnObj.GetComponent<Button>().onClick.AddListener(() => AddFish(index));
        }
    }

    public void AddFish(int fishIndex)
    {
        if (activeFish.Count >= maxFishCount) return;

        Vector3 spawnPos = GetRandomSpawnPoint();
        GameObject newFish = Instantiate(fishPrefabs[fishIndex], spawnPos, Quaternion.identity);
        activeFish.Add(newFish);

        // Eðer çýkarma paneli o an açýksa, listeyi anýnda güncelle
        if (removePanel.activeSelf) RefreshRemoveList();
    }

    // --- ÇIKARMA PANELÝ MANTIÐI ---
    public void OpenRemovePanel()
    {
        addPanel.SetActive(false);
        removePanel.SetActive(true);
        RefreshRemoveList();
    }

    public void RefreshRemoveList()
    {
        // Önceki butonlarý temizle (Liste þiþmesin)
        foreach (Transform child in removeContentParent) Destroy(child.gameObject);

        // Sahnedeki her balýk için bir buton oluþtur
        for (int i = 0; i < activeFish.Count; i++)
        {
            GameObject targetFish = activeFish[i];

            // Null kontrolü (Balýk baþka bir sebeple yok olmuþsa listeyi bozmasýn)
            if (targetFish == null) continue;

            GameObject btnObj = Instantiate(uiButtonPrefab, removeContentParent);
            // Listede "1. Japon Balýðý", "2. Köpekbalýðý" gibi görünmesi için:
            btnObj.GetComponentInChildren<TMP_Text>().text = (i + 1) + ". " + targetFish.name.Replace("(Clone)", "");

            // BUTONUN GÖREVÝ: Týklanýnca hedef balýðý silecek
            btnObj.GetComponent<Button>().onClick.AddListener(() => RemoveSpecificFish(targetFish));
        }
    }

    public void RemoveSpecificFish(GameObject fish)
    {
        if (activeFish.Contains(fish))
        {
            activeFish.Remove(fish); // Listeden çýkar
            Destroy(fish);           // Sahneden sil
            RefreshRemoveList();     // Listeyi hemen tazele (Silinen butonun gitmesi için)
        }
    }

    // --- ARAÇLAR ---
    private Vector3 GetRandomSpawnPoint()
    {
        float x = Random.Range(minBounds.x + edgePadding, maxBounds.x - edgePadding);
        float y = Random.Range(minBounds.y + edgePadding, maxBounds.y - edgePadding);
        return new Vector3(x, y, 0f);
    }

    public void ClosePanels()
    {
        addPanel.SetActive(false);
        removePanel.SetActive(false);
    }
    // Ana ekrandaki "Balýk Ekle" butonuna bunu baðlayacaðýz
    public void OpenAddPanel()
    {
        addPanel.SetActive(true);
        removePanel.SetActive(false); // Diðer panel açýksa kapat
        PrepareAddList(); // Listeyi her ihtimale karþý tazele
    }
}