using UnityEngine;

/// <summary>
/// Simple hunger system for aquarium fish.
/// Hunger drops over time. Feed by calling Feed() or via click on fish.
/// </summary>
public class FishHunger : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  SETTINGS
    // ═══════════════════════════════════════════════════════════════
    [Header("Hunger")]
    [Tooltip("Starting hunger (0 = starving, 100 = full)")]
    [Range(0f, 100f)] public float hunger = 100f;

    [Tooltip("How many hunger points are lost per second")]
    [Range(0.1f, 10f)] public float hungerDecayRate = 1f;

    [Tooltip("How much hunger is restored when fed")]
    [Range(10f, 100f)] public float feedAmount = 40f;

    [Header("Thresholds")]
    [Tooltip("Below this = fish is hungry (swims slower)")]
    [Range(10f, 60f)] public float hungryThreshold = 40f;
    [Tooltip("Below this = fish is starving (critical)")]
    [Range(0f, 30f)] public float starvingThreshold = 15f;

    // ═══════════════════════════════════════════════════════════════
    //  STATE
    // ═══════════════════════════════════════════════════════════════
    public bool IsHungry   => hunger < hungryThreshold;
    public bool IsStarving => hunger < starvingThreshold;
    public bool IsFull     => hunger >= 100f;

    // Normalised 0–1, useful for UI bars
    public float HungerNormalized => hunger / 100f;

    // ═══════════════════════════════════════════════════════════════
    //  REFERENCES
    // ═══════════════════════════════════════════════════════════════
    private FishAI fishAI;
    private float  originalMinSpeed;
    private float  originalMaxSpeed;

    // ═══════════════════════════════════════════════════════════════
    //  EVENTS  (hook these up in other scripts if needed)
    // ═══════════════════════════════════════════════════════════════
    public System.Action OnBecameHungry;
    public System.Action OnBecameStarving;
    public System.Action OnFed;

    // ═══════════════════════════════════════════════════════════════
    //  INTERNAL
    // ═══════════════════════════════════════════════════════════════
    private bool wasHungry   = false;
    private bool wasStarving = false;

    // ═══════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    void Start()
    {
        fishAI = GetComponent<FishAI>();

        if (fishAI != null)
        {
            originalMinSpeed = fishAI.minSpeed;
            originalMaxSpeed = fishAI.maxSpeed;
        }

        // Randomise starting hunger so not all fish get hungry at the same time
        hunger = Random.Range(60f, 100f);
    }

    void Update()
    {
        DecayHunger();
        ApplyHungerEffects();
        FireEvents();
    }

    // ═══════════════════════════════════════════════════════════════
    //  HUNGER DECAY
    // ═══════════════════════════════════════════════════════════════

    private void DecayHunger()
    {
        hunger = Mathf.Max(0f, hunger - hungerDecayRate * Time.deltaTime);
    }

    // ═══════════════════════════════════════════════════════════════
    //  EFFECTS ON FISH MOVEMENT
    // ═══════════════════════════════════════════════════════════════

    private void ApplyHungerEffects()
    {
        if (fishAI == null) return;

        if (IsStarving)
        {
            // Very sluggish
            fishAI.minSpeed = originalMinSpeed * 0.4f;
            fishAI.maxSpeed = originalMaxSpeed * 0.4f;
        }
        else if (IsHungry)
        {
            // Slightly slower
            fishAI.minSpeed = originalMinSpeed * 0.7f;
            fishAI.maxSpeed = originalMaxSpeed * 0.7f;
        }
        else
        {
            // Normal speed
            fishAI.minSpeed = originalMinSpeed;
            fishAI.maxSpeed = originalMaxSpeed;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  EVENTS
    // ═══════════════════════════════════════════════════════════════

    private void FireEvents()
    {
        if (IsStarving && !wasStarving)
        {
            OnBecameStarving?.Invoke();
            wasStarving = true;
        }
        else if (!IsStarving)
        {
            wasStarving = false;
        }

        if (IsHungry && !wasHungry)
        {
            OnBecameHungry?.Invoke();
            wasHungry = true;
        }
        else if (!IsHungry)
        {
            wasHungry = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Feed the fish with the default feedAmount.</summary>
    public void Feed()
    {
        hunger = Mathf.Min(100f, hunger + feedAmount);
        OnFed?.Invoke();
    }

    /// <summary>Feed the fish with a custom amount.</summary>
    public void Feed(float amount)
    {
        hunger = Mathf.Min(100f, hunger + amount);
        OnFed?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════
    //  EDITOR GIZMOS
    // ═══════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Simple hunger bar drawn above the fish in Scene view
        float   barWidth  = 1.0f;
        float   barHeight = 0.12f;
        Vector3 origin    = transform.position + Vector3.up * 0.8f;

        // Background
        Gizmos.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        Gizmos.DrawCube(origin, new Vector3(barWidth, barHeight, 0f));

        // Coloured fill
        Gizmos.color = IsStarving ? Color.red
                     : IsHungry  ? new Color(1f, 0.55f, 0f)
                                 : Color.green;
        float   fill       = HungerNormalized;
        Vector3 fillCenter = origin + Vector3.left * (barWidth * (1f - fill) * 0.5f);
        Gizmos.DrawCube(fillCenter, new Vector3(barWidth * fill, barHeight, 0f));

        UnityEditor.Handles.Label(
            origin + Vector3.up * 0.18f,
            $"Hunger: {hunger:F0}  {(IsStarving ? "STARVING!" : IsHungry ? "hungry" : "full")}"
        );
    }
#endif
}