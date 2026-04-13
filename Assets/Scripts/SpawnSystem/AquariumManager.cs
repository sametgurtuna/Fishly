using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Akvaryumun state'ini yönetir.
/// UI ve Spawner arasýndaki köprüdür.
/// </summary>
public class AquariumManager : MonoBehaviour
{
    [Header("Balýk Havuzu")]
    [Tooltip("Oyuncunun ekleyebileceði balýk prefablarý")]
    public List<GameObject> availableFish = new List<GameObject>();

    [Header("Limitler")]
    [SerializeField] private int maxFishCount = 20;

    // Baðýmlýlýklar
    private FishSpawner _spawner;

    // Aktif balýklar
    private readonly List<FishInstance> _activeFish = new List<FishInstance>();
    public IReadOnlyList<FishInstance> ActiveFish => _activeFish;

    // UI bu eventi dinler
    public event Action OnFishListChanged;

    // ?? Unity ????????????????????????????????????????????????????

    private void Awake()
    {
        _spawner = GetComponent<FishSpawner>();
        if (_spawner == null)
            _spawner = gameObject.AddComponent<FishSpawner>();
    }

    // ?? Public API ???????????????????????????????????????????????

    /// <summary>Akvaryuma balýk ekler. Baþarýsýz olursa false döner.</summary>
    public bool AddFish(GameObject prefab)
    {
        if (!CanAddFish(out string reason))
        {
            Debug.LogWarning($"[AquariumManager] Eklenemedi: {reason}");
            return false;
        }

        FishInstance instance = _spawner.Spawn(prefab);
        if (instance == null) return false;

        _activeFish.Add(instance);
        instance.OnRemoveRequested += HandleRemoveRequested;

        OnFishListChanged?.Invoke();
        return true;
    }

    /// <summary>Belirli bir balýðý akvaryumdan çýkarýr.</summary>
    public void RemoveFish(FishInstance instance)
    {
        if (instance == null || !_activeFish.Contains(instance)) return;

        instance.OnRemoveRequested -= HandleRemoveRequested;
        _activeFish.Remove(instance);
        _spawner.Despawn(instance);

        OnFishListChanged?.Invoke();
    }

    public int CurrentCount => _activeFish.Count;
    public int MaxCount => maxFishCount;
    public bool IsFull => _activeFish.Count >= maxFishCount;

    // ?? Private ??????????????????????????????????????????????????

    private bool CanAddFish(out string reason)
    {
        if (_activeFish.Count >= maxFishCount)
        {
            reason = $"Maksimum balýk sayýsýna ulaþýldý ({maxFishCount})";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private void HandleRemoveRequested(FishInstance instance)
    {
        RemoveFish(instance);
    }
}