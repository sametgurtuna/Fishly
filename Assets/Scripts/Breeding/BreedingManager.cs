using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global breeding coordinator.
///
/// Flow:
///   1. StartSelection(initiator) -> highlight all adult same-species fish.
///   2. Player left-clicks a highlighted fish -> TrySelectMate().
///   3. Both fish swim toward each other, then an egg is spawned at midpoint.
///   4. Egg incubates for the species' incubationHours, becomes baby,
///      then adult after growthMinutes.
/// </summary>
public class BreedingManager : MonoBehaviour
{
    private static BreedingManager _instance;
    public static BreedingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<BreedingManager>();
                if (_instance == null)
                {
                    var go = new GameObject("BreedingManager");
                    _instance = go.AddComponent<BreedingManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Breeding Timings")]
    [Tooltip("Fallback incubation duration in HOURS of real time.")]
    public float incubationHours = 2f;
    [Tooltip("Fallback baby growth duration in MINUTES of real time.")]
    public float growthMinutes = 30f;

    [Header("Approach")]
    [Tooltip("Units per second while the two parents swim toward each other.")]
    public float approachSpeed = 2.0f;
    [Tooltip("Distance between the two parents while mating side by side.")]
    public float matingSpacing = 0.7f;
    [Tooltip("How long the pair remains side by side before the egg appears.")]
    public float matingPauseSeconds = 2f;
    [Tooltip("How quickly the egg drops toward the bottom of the aquarium.")]
    public float eggDropSpeed = 2.25f;

    [Header("Visuals")]
    public Color highlightColor = Color.white;

    private FishAI initiator;
    private bool inSelectionMode;
    private readonly List<FishAI> highlightedFish = new List<FishAI>();

    public bool InSelectionMode => inSelectionMode;
    public FishAI Initiator => initiator;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    void Update()
    {
        if (inSelectionMode && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
            CancelSelection();
    }

    public void StartSelection(FishAI source)
    {
        if (source == null || !IsBreedable(source)) return;

        if (inSelectionMode)
            CancelSelection();

        initiator = source;
        inSelectionMode = true;
        HighlightSameSpecies(source);
    }

    public void CancelSelection()
    {
        for (int i = 0; i < highlightedFish.Count; i++)
        {
            if (highlightedFish[i] != null)
                highlightedFish[i].SetBreedingHighlight(false);
        }

        highlightedFish.Clear();
        inSelectionMode = false;
        initiator = null;
    }

    public bool TrySelectMate(FishAI target)
    {
        if (!inSelectionMode || initiator == null || target == null) return false;
        if (target == initiator) return false;
        if (target.fishData != initiator.fishData) return false;
        if (!IsBreedable(target) || !IsBreedable(initiator)) return false;

        FishAI a = initiator;
        FishAI b = target;
        CancelSelection();

        StartCoroutine(BreedRoutine(a, b));
        return true;
    }

    private IEnumerator BreedRoutine(FishAI a, FishAI b)
    {
        if (a == null || b == null) yield break;

        bool aAI = a.enabled;
        bool bAI = b.enabled;
        a.enabled = false;
        b.enabled = false;

        Vector3 startA = a.transform.position;
        Vector3 startB = b.transform.position;
        Vector3 center = (startA + startB) * 0.5f;
        float targetY = center.y;

        int side = startA.x <= startB.x ? -1 : 1;
        Vector3 leftTarget = new Vector3(center.x - matingSpacing * 0.5f, targetY, startA.z);
        Vector3 rightTarget = new Vector3(center.x + matingSpacing * 0.5f, targetY, startB.z);

        Vector3 targetPosA = side < 0 ? leftTarget : rightTarget;
        Vector3 targetPosB = side < 0 ? rightTarget : leftTarget;

        FaceSameDirection(a.transform, b.transform);

        while (a != null && b != null)
        {
            Vector3 dirA = targetPosA - a.transform.position;
            Vector3 dirB = targetPosB - b.transform.position;

            float step = approachSpeed * Time.deltaTime;
            if (dirA.magnitude > 0.05f)
                a.transform.position += dirA.normalized * step;
            if (dirB.magnitude > 0.05f)
                b.transform.position += dirB.normalized * step;

            FaceSameDirection(a.transform, b.transform);

            if (Vector3.Distance(a.transform.position, targetPosA) <= 0.06f &&
                Vector3.Distance(b.transform.position, targetPosB) <= 0.06f)
                break;

            yield return null;
        }

        if (a != null) a.transform.position = targetPosA;
        if (b != null) b.transform.position = targetPosB;
        FaceSameDirection(a.transform, b.transform);

        yield return new WaitForSeconds(matingPauseSeconds);

        if (a != null && b != null)
        {
            Vector3 eggPos = new Vector3((a.transform.position.x + b.transform.position.x) * 0.5f, Mathf.Min(a.transform.position.y, b.transform.position.y) - 0.25f, a.transform.position.z);
            SpawnEgg(eggPos, a.fishData);
        }

        if (a != null) a.enabled = aAI;
        if (b != null) b.enabled = bAI;
    }

    private static void FaceSameDirection(Transform a, Transform b)
    {
        if (a == null || b == null) return;

        bool faceRight = a.position.x <= b.position.x;
        SetFacing(a, faceRight);
        SetFacing(b, faceRight);
    }

    private static void SetFacing(Transform t, bool faceRight)
    {
        if (t == null) return;

        Vector3 s = t.localScale;
        float abs = Mathf.Abs(s.x);
        s.x = faceRight ? abs : -abs;
        t.localScale = s;
    }

    private void SpawnEgg(Vector3 position, FishData data)
    {
        if (data == null) return;

        GameObject egg;
        if (data.eggPrefab != null)
        {
            egg = Instantiate(data.eggPrefab, position, Quaternion.identity);
            egg.name = (data.fishName ?? "Fish") + "_Egg";
        }
        else
        {
            egg = new GameObject((data.fishName ?? "Fish") + "_Egg");
            egg.transform.position = position;

            var sr = egg.AddComponent<SpriteRenderer>();
            sr.sprite = EggSpriteFactory.Get();
            sr.color = new Color(1f, 0.95f, 0.8f, 1f);

            var col = egg.AddComponent<CircleCollider2D>();
            col.radius = 0.25f;
        }

        var stage = egg.GetComponent<FishLifeStage>();
        if (stage == null)
            stage = egg.AddComponent<FishLifeStage>();

        stage.InitAsEgg(
            data,
            data.incubationHours > 0f ? data.incubationHours : incubationHours,
            data.growthMinutes > 0f ? data.growthMinutes : growthMinutes
        );

        StartCoroutine(DropEggToBottom(egg.transform));

        GameManager.Instance?.ScanSceneFish();
        GameManager.Instance?.SaveGame();
    }

    private IEnumerator DropEggToBottom(Transform egg)
    {
        if (egg == null) yield break;

        float bottomY = -3.6f;
        FishAI[] all = FindObjectsOfType<FishAI>();
        if (all.Length > 0)
        {
            bottomY = all[0].minBounds.y + 0.35f;
        }

        Vector3 start = egg.position;
        Vector3 target = new Vector3(start.x, bottomY, start.z);
        float duration = Mathf.Max(0.12f, Mathf.Abs(start.y - target.y) / eggDropSpeed);
        float t = 0f;

        while (egg != null && t < 1f)
        {
            t += Time.deltaTime / duration;
            egg.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        if (egg != null)
            egg.position = target;
    }

    private void HighlightSameSpecies(FishAI source)
    {
        FishAI[] all = FindObjectsOfType<FishAI>();
        foreach (FishAI f in all)
        {
            if (f == null || f == source) continue;
            if (f.fishData != source.fishData) continue;
            if (!IsBreedable(f)) continue;

            highlightedFish.Add(f);
            f.SetBreedingHighlight(true);
        }
    }

    private static bool IsBreedable(FishAI f)
    {
        if (f == null || f.fishData == null) return false;
        if (!f.fishData.canBreed) return false;

        FishLifeStage stage = f.GetComponent<FishLifeStage>();
        return stage == null || stage.CanBreed;
    }
}
