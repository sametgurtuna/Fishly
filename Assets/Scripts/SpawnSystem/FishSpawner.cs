using UnityEngine;

/// <summary>
/// Balýklarý fiziksel olarak sahneye ekler ve kaldýrýr.
/// Spawn koordinatlarýný sahnedeki herhangi bir FishAI'ýn
/// bounds deðerlerinden okur — deðerleri tekrar tanýmlamaya gerek yok.
/// </summary>
public class FishSpawner : MonoBehaviour
{
    [Header("Spawn Ayarlarý")]
    [Tooltip("Spawn edilecek balýðýn z derinliði (2D için 0 býrakýn)")]
    [SerializeField] private float spawnZ = 0f;

    // Bounds ve padding'i tek seferinde FishAI'dan okur
    private Vector2 _minBounds;
    private Vector2 _maxBounds;
    private float _edgePadding;
    private bool _boundsReady;

    private void Awake()
    {
        CacheBounds();
    }

    // ?? Public API ???????????????????????????????????????????????

    public FishInstance Spawn(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[FishSpawner] Prefab null.");
            return null;
        }

        Vector3 pos = GetRandomSpawnPosition();
        GameObject go = Instantiate(prefab, pos, Quaternion.identity);

        // FishAI zaten prefabda var; oradan FishData'yý okuyoruz
        FishAI fishAI = go.GetComponent<FishAI>();
        if (fishAI == null)
        {
            Debug.LogError($"[FishSpawner] '{prefab.name}' prefabýnda FishAI bulunamadý.");
            Destroy(go);
            return null;
        }

        // Prefaba dokunmadan FishInstance ekle
        FishInstance instance = go.AddComponent<FishInstance>();
        instance.Initialize(fishAI.fishData);

        return instance;
    }

    public void Despawn(FishInstance instance)
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
    }

    // ?? Yardýmcý Metodlar ????????????????????????????????????????

    private Vector3 GetRandomSpawnPosition()
    {
        if (!_boundsReady)
        {
            Debug.LogWarning("[FishSpawner] Bounds hazýr deðil, varsayýlan alan kullanýlýyor.");
            return Vector3.zero;
        }

        float x = Random.Range(_minBounds.x + _edgePadding, _maxBounds.x - _edgePadding);
        float y = Random.Range(_minBounds.y + _edgePadding, _maxBounds.y - _edgePadding);
        return new Vector3(x, y, spawnZ);
    }

    private void CacheBounds()
    {
        // Sahnedeki herhangi bir FishAI'dan bounds deðerlerini oku
        // (Tüm balýklar ayný akvaryumda yüzdüðü için deðerler ortaktýr)
        FishAI reference = FindObjectOfType<FishAI>();
        if (reference != null)
        {
            _minBounds = reference.minBounds;
            _maxBounds = reference.maxBounds;
            _edgePadding = reference.edgePadding;
            _boundsReady = true;
        }
        else
        {
            // Sahnede henüz balýk yoksa, AquariumManager ilk spawn'dan önce
            // SetBounds() ile deðerleri manuel verebilir
            Debug.LogWarning("[FishSpawner] Sahnede FishAI bulunamadý. SetBounds() ile manuel ayarlayýn.");
        }
    }

    /// <summary>
    /// Sahnede henüz hiç balýk yokken AquariumManager tarafýndan çaðrýlabilir.
    /// </summary>
    public void SetBounds(Vector2 min, Vector2 max, float padding)
    {
        _minBounds = min;
        _maxBounds = max;
        _edgePadding = padding;
        _boundsReady = true;
    }
}