using System;
using UnityEngine;

/// <summary>
/// Sahnedeki her balýk instance'ýna AddComponent ile eklenir.
/// Orijinal prefab scriptlerine (FishAI, FishHunger) dokunulmaz.
/// </summary>
public class FishInstance : MonoBehaviour
{
    public string InstanceId { get; private set; }
    public FishData Data { get; private set; }

    public event Action<FishInstance> OnRemoveRequested;

    public void Initialize(FishData data)
    {
        InstanceId = Guid.NewGuid().ToString();
        Data = data;
    }

    // AquariumUI listedeki çarpý butonuna bastýðýnda çaðýrýr
    public void RequestRemove()
    {
        OnRemoveRequested?.Invoke(this);
    }
}