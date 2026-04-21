using UnityEngine;

public enum FishDepthLayer
{
    Surface,
    Mid,
    Bottom,
    Any
}

public enum Rarity
{
    Common,    
    Uncommon,  
    Rare,      
    Epic,      
    Legendary  
}

[CreateAssetMenu(fileName = "New Fish", menuName = "Fish Data")]
public class FishData : ScriptableObject
{
    [Header("Visuals")]
    public string fishName;
    public Sprite fishSprite;

    [Header("Rarity")]
    public Rarity fishRarity; 

    [Header("Economy")]
    public double baseCost;
    [Tooltip("Base passive income generated per HOUR by one fish of this type")]
    public double baseIncome;
    
    [Header("Growth")]
    public float priceMultiplier = 1.15f;

    [Header("Movement")]
    [Range(0.2f, 2f)] public float minSpeed = 0.5f;
    [Range(0.5f, 5f)] public float maxSpeed = 2.0f;
    [Range(0.3f, 4f)] public float speedSmoothTime = 1.2f;

    [Header("Tail Wobble")]
    [Range(1f, 4f)] public float wobbleFreqMin = 1.8f;
    [Range(3f, 12f)] public float wobbleFreqMax = 7.0f;
    [Range(0.01f, 0.4f)] public float wobbleAmplitude = 0.12f;

    [Header("Rotation & Path")]
    [Range(0f, 30f)] public float maxTiltAngle = 14f;
    [Range(1f, 20f)] public float tiltSmooth = 7f;
    [Range(2f, 20f)] public float flipSpeed = 8f;
    [Range(0f, 5f)] public float curveStrength = 2.0f;

    [Header("Idle & Rest")]
    public float minIdleTime = 0.8f;
    public float maxIdleTime = 3.5f;
    [Range(0f, 0.5f)] public float restChance = 0.12f;
    public float minRestTime = 2.0f;
    public float maxRestTime = 6.0f;

    [Header("Surface Feeding")]
    public bool canSurfaceFeed = true;
    [Range(0f, 0.3f)] public float surfaceFeedChance = 0.08f;
    public float surfaceY = 3.5f;
    public float feedDuration = 1.6f;

    [Header("Schooling")]
    public bool enableSchooling = true;
    [Range(0.5f, 3f)] public float separationRadius = 1.2f;
    [Range(2f, 8f)] public float neighborRadius = 4.0f;
    [Range(0f, 3f)] public float separationWeight = 1.5f;
    [Range(0f, 2f)] public float alignmentWeight = 0.8f;
    [Range(0f, 2f)] public float cohesionWeight = 0.5f;

    [Header("Curiosity")]
    public bool enableCuriosity = true;
    [Range(0f, 1f)] public float baseCuriosityChance = 0.14f;
    [Range(0.5f, 3f)] public float curiosityStopDistance = 1.4f;

    [Header("Flee")]
    [Range(0.5f, 6f)] public float fleeRadius = 2.5f;
    [Range(1.5f, 5f)] public float fleeSpeedMultiplier = 2.8f;
    [Range(0.3f, 2f)] public float fleeDuration = 0.8f;

    [Header("Depth Preference")]
    public FishDepthLayer preferredDepth = FishDepthLayer.Any;
    [Range(0f, 1f)] public float depthBias = 0.6f;

    [Header("Personality")]
    [Range(0f, 1f)] public float defaultBoldness = 0.5f;
    [Range(0f, 1f)] public float defaultActivityLevel = 0.5f;
    [Range(0f, 1f)] public float defaultSociability = 0.5f;
    public bool randomisePersonality = true;

    [Header("Variation")]
    [Range(0f, 0.35f)] public float sizeVariation = 0.18f;

    [Header("Breeding")]
    public bool canBreed = true;

    [Tooltip("Prefab spawned as this fish's egg. If empty, a fallback circle egg is used.")]
    public GameObject eggPrefab;

    [Tooltip("Prefab spawned when this fish hatches to baby (e.g. Turadi_Baby). If empty, current object turns into baby.")]
    public GameObject babyPrefab;

    [Tooltip("How long the egg takes to hatch, in HOURS of real time")]
    [Range(0.1f, 24f)] public float incubationHours = 2f;

    [Tooltip("How long the baby takes to become an adult, in MINUTES of real time")]
    [Range(1f, 240f)] public float growthMinutes = 30f;

    [Tooltip("Scale of the baby relative to the adult fish")]
    [Range(0.1f, 1f)] public float babyScale = 0.55f;

    [Tooltip("Scale of the egg relative to the adult fish")]
    [Range(0.1f, 1f)] public float eggScale = 0.30f;

    public Color GetRarityColor()
    {
        switch (fishRarity)
        {
            case Rarity.Common: return Color.white;
            case Rarity.Uncommon: return Color.green;
            case Rarity.Rare: return Color.blue;
            case Rarity.Epic: return new Color(0.5f, 0, 0.5f); 
            case Rarity.Legendary: return new Color(1, 0.5f, 0); 
            default: return Color.white;
        }
    }
}