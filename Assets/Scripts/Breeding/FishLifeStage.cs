using System.Collections;
using UnityEngine;

/// <summary>
/// Manages a fish's life stage: Egg -> Baby -> Adult.
///
/// Egg:   circle sprite while incubating (hours).
/// Baby:  smaller fish, cannot breed, grows into adult after minutes.
/// Adult: can breed.
/// </summary>
public class FishLifeStage : MonoBehaviour
{
    public enum Stage { Egg, Baby, Adult }

    [Header("State")]
    public FishData fishData;
    public Stage stage = Stage.Adult;

    [Header("Timings")]
    [Tooltip("Incubation duration in HOURS of real time.")]
    public float incubationHours = 2f;
    [Tooltip("Baby->Adult growth duration in MINUTES of real time.")]
    public float growthMinutes = 30f;

    [Header("Visuals")]
    [Range(0.1f, 1f)] public float babyScale = 0.55f;
    [Range(0.1f, 1f)] public float eggScale = 0.30f;
    public Color eggColor = new Color(1f, 0.95f, 0.8f, 1f);

    private float timer;
    private SpriteRenderer spriteRenderer;
    private FishAI fishAI;

    public bool CanBreed => stage == Stage.Adult;
    public bool IsEgg => stage == Stage.Egg;
    public bool IsBaby => stage == Stage.Baby;
    public Stage CurrentStage => stage;
    public float RemainingStageTimeSeconds => Mathf.Max(0f, timer);

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        fishAI = GetComponent<FishAI>();
    }

    /// <summary>Configure this GameObject as a freshly-laid egg.</summary>
    public void InitAsEgg(FishData data, float incubHours, float growthMin)
    {
        fishData = data;
        incubationHours = incubHours;
        growthMinutes = growthMin;
        if (data != null)
        {
            babyScale = data.babyScale > 0f ? data.babyScale : babyScale;
            eggScale = data.eggScale > 0f ? data.eggScale : eggScale;
        }

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (fishAI == null) fishAI = GetComponent<FishAI>();
        if (fishAI != null)
            fishAI.enabled = false;

        bool hasCustomEggVisual = spriteRenderer != null && spriteRenderer.sprite != null;
        if (!hasCustomEggVisual)
        {
            spriteRenderer.sprite = EggSpriteFactory.Get();
            spriteRenderer.color = eggColor;
            transform.localScale = Vector3.one * eggScale;
        }
        transform.rotation = Quaternion.identity;

        stage = Stage.Egg;
        timer = incubationHours * 3600f;
        gameObject.name = (data != null ? data.fishName : "Fish") + "_Egg";
    }

    void Update()
    {
        switch (stage)
        {
            case Stage.Egg:
                UpdateEgg();
                break;
            case Stage.Baby:
                UpdateBaby();
                break;
        }
    }

    private void UpdateEgg()
    {
        float pulse = 1f + Mathf.Sin(Time.time * 2.2f) * 0.06f;
        transform.localScale = Vector3.one * eggScale * pulse;

        timer -= Time.deltaTime;
        if (timer <= 0f)
            HatchToBaby();
    }

    private void UpdateBaby()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
            GrowToAdult();
    }

    private void HatchToBaby()
    {
        if (fishData != null && fishData.babyPrefab != null)
        {
            GameObject babyObj = Instantiate(fishData.babyPrefab, transform.position, Quaternion.identity);
            babyObj.name = (fishData.fishName ?? "Fish") + "_Baby";

            FishLifeStage babyStage = babyObj.GetComponent<FishLifeStage>();
            if (babyStage == null)
                babyStage = babyObj.AddComponent<FishLifeStage>();

            babyStage.InitAsBaby(fishData, growthMinutes, preserveScale: true);
            Destroy(gameObject);
        }
        else
        {
            InitAsBaby(fishData, growthMinutes, preserveScale: false);
        }

        GameManager.Instance?.ScanSceneFish();
        GameManager.Instance?.SaveGame();
    }

    public void InitAsBaby(FishData data, float growthMin, bool preserveScale)
    {
        fishData = data;
        growthMinutes = growthMin;
        if (data != null)
            babyScale = data.babyScale > 0f ? data.babyScale : babyScale;

        stage = Stage.Baby;
        timer = growthMinutes * 60f;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        if (fishData != null && fishData.fishSprite != null && spriteRenderer.sprite == null)
            spriteRenderer.sprite = fishData.fishSprite;
        spriteRenderer.color = Color.white;

        if (fishAI == null) fishAI = GetComponent<FishAI>();
        if (fishAI == null)
            fishAI = gameObject.AddComponent<FishAI>();

        fishAI.fishData = fishData;
        fishAI.enabled = true;

        if (GetComponent<Collider2D>() == null)
        {
            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
        }

        if (!preserveScale)
            transform.localScale = Vector3.one * babyScale;

        gameObject.name = (fishData != null ? fishData.fishName : "Fish") + "_Baby";
    }

    public void SetRemainingStageTime(float seconds)
    {
        timer = Mathf.Max(0f, seconds);
    }

    private void GrowToAdult()
    {
        stage = Stage.Adult;
        gameObject.name = fishData != null ? fishData.fishName : "Fish";
        StartCoroutine(GrowScaleRoutine());

        GameManager.Instance?.ScanSceneFish();
        GameManager.Instance?.SaveGame();
    }

    private IEnumerator GrowScaleRoutine()
    {
        Vector3 from = transform.localScale;
        Vector3 to = Vector3.one;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 0.6f;
            transform.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }
        transform.localScale = to;
    }
}

/// <summary>Procedurally-generated, cached circle sprite used for eggs.</summary>
internal static class EggSpriteFactory
{
    private static Sprite _cached;

    public static Sprite Get()
    {
        if (_cached != null) return _cached;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d <= radius)
                {
                    float shade = Mathf.Lerp(1f, 0.78f, d / radius);
                    tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();

        _cached = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _cached.name = "EggCircle";
        return _cached;
    }
}
