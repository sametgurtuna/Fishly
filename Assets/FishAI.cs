using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Realistic Aquarium Fish AI — Idlequarium Edition
/// ─────────────────────────────────────────────────
/// Features:
///   • Boids-style schooling (separation / alignment / cohesion)
///   • Personality system  (boldness, activity, sociability)
///   • Depth-preference zones (surface / mid / bottom)
///   • Surface feeding animation
///   • Resting state with breathing pulse
///   • Speed-scaled tail wobble
///   • Smooth quadratic-Bezier patrol paths
///   • Edge soft-avoidance
///   • Cursor curiosity + click-flee
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FishAI : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  DATA REFERENCE
    // ═══════════════════════════════════════════════════════════════
    [Header("Fish Data")]
    public FishData fishData;

    // ═══════════════════════════════════════════════════════════════
    //  PERSONALITY  (randomised once per instance in Start)
    // ═══════════════════════════════════════════════════════════════
    [Header("Personality (auto-randomised)")]
    [Tooltip("0 = very shy, 1 = very bold (affects flee/curiosity thresholds)")]
    [Range(0f, 1f)] public float boldness = 0.5f;
    [Tooltip("0 = lazy/slow, 1 = always darting around")]
    [Range(0f, 1f)] public float activityLevel = 0.5f;
    [Tooltip("0 = loner, 1 = loves company (affects schooling weight)")]
    [Range(0f, 1f)] public float sociability = 0.5f;
    [Tooltip("Randomise personality on Start?")]
    public bool randomisePersonality = true;

    // ═══════════════════════════════════════════════════════════════
    //  MOVEMENT
    // ═══════════════════════════════════════════════════════════════
    [Header("Movement")]
    [Range(0.2f, 2f)]  public float minSpeed = 0.5f;
    [Range(0.5f, 5f)]  public float maxSpeed = 2.0f;
    [Tooltip("Larger = smoother but slower speed transitions")]
    [Range(0.3f, 4f)]  public float speedSmoothTime = 1.2f;

    private float currentSpeed;
    private float targetSpeed;
    private float speedVelocity;   // SmoothDamp internal

    // ═══════════════════════════════════════════════════════════════
    //  IDLE / REST TIMING
    // ═══════════════════════════════════════════════════════════════
    [Header("Idle & Rest")]
    public float minIdleTime   = 0.8f;
    public float maxIdleTime   = 3.5f;
    [Tooltip("How often the fish enters a longer rest (0–1)")]
    [Range(0f, 0.5f)] public float restChance = 0.12f;
    public float minRestTime   = 2.0f;
    public float maxRestTime   = 6.0f;

    private float idleTimer;



    // ═══════════════════════════════════════════════════════════════
    //  TAIL WOBBLE  (speed-scaled, realistic)
    // ═══════════════════════════════════════════════════════════════
    [Header("Tail Wobble")]
    [Tooltip("Wobble frequency at minimum speed")]
    [Range(1f, 4f)] public float wobbleFreqMin = 1.8f;
    [Tooltip("Wobble frequency at maximum speed")]
    [Range(3f, 12f)] public float wobbleFreqMax = 7.0f;
    [Tooltip("Wobble Y amplitude")]
    [Range(0.01f, 0.4f)] public float wobbleAmplitude = 0.12f;
    private float wobblePhase;         // unique per instance

    // ═══════════════════════════════════════════════════════════════
    //  SMOOTH ROTATION
    // ═══════════════════════════════════════════════════════════════
    [Header("Rotation")]
    [Range(0f, 30f)] public float maxTiltAngle = 14f;
    [Range(1f, 20f)] public float tiltSmooth   = 7f;
    private float currentTilt;

    // ═══════════════════════════════════════════════════════════════
    //  SMOOTH FLIP
    // ═══════════════════════════════════════════════════════════════
    [Header("Flip")]
    [Range(2f, 20f)] public float flipSpeed = 8f;
    private float targetScaleX;
    private float originalScaleX;

    // ═══════════════════════════════════════════════════════════════
    //  BEZIER PATH
    // ═══════════════════════════════════════════════════════════════
    [Header("Curved Path")]
    [Range(0f, 5f)] public float curveStrength = 2.0f;
    private Vector2 bezierStart;
    private Vector2 bezierControl;
    private Vector2 bezierEnd;
    private float   bezierT;

    // ═══════════════════════════════════════════════════════════════
    //  AQUARIUM BOUNDS
    // ═══════════════════════════════════════════════════════════════
    [Header("Aquarium Bounds")]
    public Vector2 minBounds   = new Vector2(-7.5f, -4f);
    public Vector2 maxBounds   = new Vector2( 7.5f,  4f);
    [Range(0.2f, 2.5f)] public float edgePadding = 1.0f;

    // ═══════════════════════════════════════════════════════════════
    //  DEPTH PREFERENCE
    // ═══════════════════════════════════════════════════════════════
    public enum DepthLayer { Surface, Mid, Bottom, Any }

    [Header("Depth Preference")]
    public DepthLayer preferredDepth = DepthLayer.Any;
    [Tooltip("How strongly the fish is pulled toward its preferred depth (0 = ignored)")]
    [Range(0f, 1f)] public float depthBias = 0.6f;

    // ═══════════════════════════════════════════════════════════════
    //  SURFACE FEEDING
    // ═══════════════════════════════════════════════════════════════
    [Header("Surface Feeding")]
    [Tooltip("Should this fish occasionally swim up to eat?")]
    public bool canSurfaceFeed  = true;
    [Range(0f, 0.3f)] public float surfaceFeedChance = 0.08f;
    [Tooltip("Y position considered 'surface'")]
    public float surfaceY        = 3.5f;
    [Tooltip("How long the nibble animation plays")]
    public float feedDuration    = 1.6f;
    private float feedTimer;

    // ═══════════════════════════════════════════════════════════════
    //  SCHOOLING (Boids)
    // ═══════════════════════════════════════════════════════════════
    [Header("Schooling (Boids)")]
    public bool enableSchooling  = true;
    [Range(0.5f, 3f)]  public float separationRadius    = 1.2f;
    [Range(2f, 8f)]    public float neighborRadius       = 4.0f;
    [Range(0f, 3f)]    public float separationWeight     = 1.5f;
    [Range(0f, 2f)]    public float alignmentWeight      = 0.8f;
    [Range(0f, 2f)]    public float cohesionWeight       = 0.5f;
    [Tooltip("Layer mask for other fish — set to your Fish layer")]
    public LayerMask fishLayer;

    // ═══════════════════════════════════════════════════════════════
    //  CURIOSITY
    // ═══════════════════════════════════════════════════════════════
    [Header("Curiosity")]
    public bool enableCuriosity        = true;
    [Range(0f, 1f)] public float baseCuriosityChance = 0.14f;
    [Range(0.5f, 3f)] public float curiosityStopDistance = 1.4f;

    // ═══════════════════════════════════════════════════════════════
    //  FLEE
    // ═══════════════════════════════════════════════════════════════
    [Header("Flee")]
    [Range(0.5f, 6f)]  public float fleeRadius           = 2.5f;
    [Range(1.5f, 5f)]  public float fleeSpeedMultiplier  = 2.8f;
    [Range(0.3f, 2f)]  public float fleeDuration         = 0.8f;
    private float fleeTimer;

    // ═══════════════════════════════════════════════════════════════
    //  SIZE VARIATION
    // ═══════════════════════════════════════════════════════════════
    [Header("Individual Variation")]
    [Range(0f, 0.35f)] public float sizeVariation = 0.18f;

    // ═══════════════════════════════════════════════════════════════
    //  STATE MACHINE
    // ═══════════════════════════════════════════════════════════════
    private enum FishState { Idle, Swimming, Schooling, Curious, Feeding, Resting, Fleeing }
    private FishState state = FishState.Idle;

    // ═══════════════════════════════════════════════════════════════
    //  INTERNALS
    // ═══════════════════════════════════════════════════════════════
    private SpriteRenderer  spriteRenderer;
    private Vector3         previousPosition;
    private Vector3         baseScale;               // before breathing
    private Vector2         schoolingVelocity;       // boids composite

    // Reusable overlap buffer (no per-frame allocations)
    private static readonly Collider2D[] _overlapBuffer = new Collider2D[24];

    // ═══════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (fishData != null && fishData.fishSprite != null)
            spriteRenderer.sprite = fishData.fishSprite;

        if (randomisePersonality)
        {
            boldness       = Random.value;
            activityLevel  = Random.value;
            sociability    = Random.value;
        }

        // Unique wobble phase so fish don't oscillate in sync
        wobblePhase = Random.Range(0f, Mathf.PI * 2f);

        // Size variation
        float scaleMultiplier = 1f + Random.Range(-sizeVariation, sizeVariation);
        transform.localScale *= scaleMultiplier;

        baseScale      = transform.localScale;
        originalScaleX = Mathf.Abs(baseScale.x);
        targetScaleX   = originalScaleX;

        // Assign random depth preference if not set manually
        if (preferredDepth == DepthLayer.Any)
            preferredDepth = (DepthLayer)Random.Range(0, 3);

        // Stagger start so fish don't all move at frame 0
        idleTimer = Random.Range(0f, 1.0f);
        state     = FishState.Idle;

        previousPosition = transform.position;
    }

    void Update()
    {
        CheckFleeTrigger();

        switch (state)
        {
            case FishState.Idle:      UpdateIdle();      break;
            case FishState.Swimming:  UpdateSwimming();  break;
            case FishState.Schooling: UpdateSchooling(); break;
            case FishState.Curious:   UpdateCurious();   break;
            case FishState.Feeding:   UpdateFeeding();   break;
            case FishState.Resting:   UpdateResting();   break;
            case FishState.Fleeing:   UpdateFleeing();   break;
        }

        UpdateFlip();
        UpdateTilt();
        ApplyTailWobble();

        previousPosition = transform.position;
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: IDLE
    // ═══════════════════════════════════════════════════════════════

    private void UpdateIdle()
    {
        // Tiny drift — feels alive even when "still"
        float drift = Mathf.Sin(Time.time * 0.28f + wobblePhase) * 0.04f;
        transform.position += new Vector3(drift, 0f, 0f) * Time.deltaTime;

        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
            ChooseNextBehavior();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: RESTING  (long pause, breathing visible)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateResting()
    {
        // Barely move — settle toward a natural hover with micro-drift
        float microDrift = Mathf.Sin(Time.time * 0.18f + wobblePhase) * 0.025f;
        transform.position += new Vector3(microDrift, microDrift * 0.3f, 0f) * Time.deltaTime;

        // Gradually decelerate to a stop
        currentSpeed = Mathf.SmoothDamp(currentSpeed, 0f, ref speedVelocity, 0.8f);

        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
            ChooseNextBehavior();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: SWIMMING (Bezier patrol)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateSwimming()
    {
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, speedSmoothTime);

        float dist        = Vector2.Distance(bezierStart, bezierEnd);
        float travelSpeed = dist > 0.01f ? currentSpeed / dist : 1f;
        bezierT = Mathf.Clamp01(bezierT + travelSpeed * Time.deltaTime);

        Vector2 newPos = EvaluateQuadraticBezier(bezierStart, bezierControl, bezierEnd, bezierT);
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);

        UpdateFacingDirection();

        if (bezierT >= 1f)
            EnterIdle();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: SCHOOLING (Boids steering)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateSchooling()
    {
        Vector2 separation = Vector2.zero;
        Vector2 alignment  = Vector2.zero;
        Vector2 cohesion   = Vector2.zero;
        int     neighbors  = 0;

        int count = Physics2D.OverlapCircleNonAlloc(transform.position, neighborRadius, _overlapBuffer, fishLayer);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _overlapBuffer[i];
            if (col == null || col.gameObject == gameObject) continue;

            Vector2 toNeighbor  = (Vector2)col.transform.position - (Vector2)transform.position;
            float   dist        = toNeighbor.magnitude;

            // Separation — push away from very close fish
            if (dist < separationRadius && dist > 0.001f)
                separation -= toNeighbor.normalized / dist;

            // Alignment — match neighbour heading
            if (col.TryGetComponent(out FishAI other))
                alignment += (Vector2)(other.transform.position - other.previousPosition).normalized;

            // Cohesion — steer toward average position
            cohesion += (Vector2)col.transform.position;
            neighbors++;
        }

        Vector2 steer = Vector2.zero;

        if (neighbors > 0)
        {
            separation *= separationWeight;
            alignment   = (alignment / neighbors).normalized * alignmentWeight;
            cohesion    = ((cohesion / neighbors) - (Vector2)transform.position).normalized * cohesionWeight;
            steer       = (separation + alignment + cohesion) * sociability;
        }

        // Blend schooling with current travel direction
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, speedSmoothTime);

        if (steer.sqrMagnitude > 0.001f)
            transform.position += (Vector3)steer.normalized * currentSpeed * Time.deltaTime;
        else
            transform.position += (Vector3)(bezierEnd - (Vector2)transform.position).normalized * currentSpeed * Time.deltaTime;

        UpdateFacingDirection();
        ClampToBounds();

        if (Vector2.Distance(transform.position, bezierEnd) < 0.3f)
            EnterIdle();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: CURIOUS
    // ═══════════════════════════════════════════════════════════════

    private void UpdateCurious()
    {
        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector2 toMouse    = (Vector2)mouseWorld - (Vector2)transform.position;

        if (toMouse.magnitude < curiosityStopDistance)
        {
            EnterIdle();
            return;
        }

        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed * 0.65f, ref speedVelocity, speedSmoothTime);
        transform.position += (Vector3)(toMouse.normalized * currentSpeed) * Time.deltaTime;

        UpdateFacingDirection();
        ClampToBounds();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: FEEDING  (swim to surface, nibble, return)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateFeeding()
    {
        Vector2 target     = new Vector2(transform.position.x, surfaceY);
        Vector2 toSurface  = target - (Vector2)transform.position;

        if (toSurface.magnitude > 0.15f)
        {
            // Rise to surface
            currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed * 0.55f, ref speedVelocity, 0.6f);
            transform.position += (Vector3)(toSurface.normalized * currentSpeed) * Time.deltaTime;
            UpdateFacingDirection();
        }
        else
        {
            // Nibble animation — tiny up-down oscillation at surface
            float nibble = Mathf.Sin(Time.time * 8f) * 0.04f;
            transform.position = new Vector3(transform.position.x, surfaceY + nibble, transform.position.z);
            currentSpeed = Mathf.SmoothDamp(currentSpeed, 0f, ref speedVelocity, 0.4f);
        }

        feedTimer -= Time.deltaTime;
        if (feedTimer <= 0f)
            EnterIdle();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: FLEEING
    // ═══════════════════════════════════════════════════════════════

    private void UpdateFleeing()
    {
        float burstTarget = targetSpeed * fleeSpeedMultiplier;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, burstTarget, ref speedVelocity, 0.08f);

        float dist    = Mathf.Max(Vector2.Distance(bezierStart, bezierEnd), 0.1f);
        bezierT       = Mathf.Clamp01(bezierT + (currentSpeed / dist) * Time.deltaTime);

        Vector2 newPos = EvaluateQuadraticBezier(bezierStart, bezierControl, bezierEnd, bezierT);
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);

        UpdateFacingDirection();

        fleeTimer -= Time.deltaTime;
        if (fleeTimer <= 0f || bezierT >= 1f)
            EnterIdle();
    }

    // ═══════════════════════════════════════════════════════════════
    //  BEHAVIOR SELECTOR
    // ═══════════════════════════════════════════════════════════════

    private void ChooseNextBehavior()
    {
        float rng = Random.value;
        float curiosityChance = baseCuriosityChance * boldness;

        // 1. Surface feeding
        if (canSurfaceFeed && rng < surfaceFeedChance)
        {
            feedTimer    = feedDuration;
            targetSpeed  = Random.Range(minSpeed, maxSpeed) * 0.6f;
            currentSpeed = Mathf.Max(currentSpeed, 0.05f);
            state        = FishState.Feeding;
            return;
        }

        // 2. Long rest (lazy fish more likely)
        float adjustedRestChance = restChance + (1f - activityLevel) * 0.15f;
        if (rng < adjustedRestChance)
        {
            idleTimer = Random.Range(minRestTime, maxRestTime);
            state     = FishState.Resting;
            return;
        }

        // 3. Curiosity about cursor
        if (enableCuriosity && rng < curiosityChance)
        {
            Vector3 mouse = GetMouseWorldPosition();
            if (IsInsideBounds(mouse))
            {
                targetSpeed  = Random.Range(minSpeed, maxSpeed);
                currentSpeed = 0.1f;
                state        = FishState.Curious;
                return;
            }
        }

        // 4. Schooling with nearby fish
        if (enableSchooling && sociability > 0.35f)
        {
            int neighborCount = Physics2D.OverlapCircleNonAlloc(transform.position, neighborRadius, _overlapBuffer, fishLayer);
            int validCount    = 0;
            for (int i = 0; i < neighborCount; i++)
                if (_overlapBuffer[i] != null && _overlapBuffer[i].gameObject != gameObject) validCount++;

            if (validCount >= 2 && Random.value < sociability)
            {
                SetupBezierPath(biasTowardNeighbors: true);
                targetSpeed = Random.Range(minSpeed, maxSpeed);
                state       = FishState.Schooling;
                return;
            }
        }

        // 5. Default patrol swim
        SetupBezierPath(biasTowardNeighbors: false);
        state = FishState.Swimming;
    }

    // ═══════════════════════════════════════════════════════════════
    //  FLEE TRIGGER
    // ═══════════════════════════════════════════════════════════════

    private void CheckFleeTrigger()
    {
        if (state == FishState.Fleeing) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector3 clickWorld = GetMouseWorldPosition();
        float   dist       = Vector2.Distance(clickWorld, transform.position);

        // Bold fish have a smaller effective flee radius
        float adjustedRadius = fleeRadius * (1f - boldness * 0.4f);
        if (dist > adjustedRadius) return;

        Vector2 fleeDir    = ((Vector2)transform.position - (Vector2)clickWorld).normalized;
        Vector2 fleeTarget = ClampPositionToBounds((Vector2)transform.position + fleeDir * Random.Range(2.5f, 5f));

        bezierStart   = transform.position;
        bezierEnd     = fleeTarget;
        Vector2 mid   = (bezierStart + bezierEnd) * 0.5f;
        Vector2 perp  = Vector2.Perpendicular((bezierEnd - bezierStart).normalized);
        bezierControl = ClampPositionToBounds(mid + perp * Random.Range(-1.2f, 1.2f));
        bezierT       = 0f;

        targetSpeed  = maxSpeed * fleeSpeedMultiplier;
        currentSpeed = maxSpeed;
        fleeTimer    = fleeDuration;
        state        = FishState.Fleeing;
    }

    // ═══════════════════════════════════════════════════════════════
    //  BEZIER PATH SETUP  (with depth bias + optional neighbor bias)
    // ═══════════════════════════════════════════════════════════════

    private void SetupBezierPath(bool biasTowardNeighbors)
    {
        bezierStart = transform.position;

        float padMinX = minBounds.x + edgePadding;
        float padMaxX = maxBounds.x - edgePadding;
        float padMinY = minBounds.y + edgePadding;
        float padMaxY = maxBounds.y - edgePadding;

        // Depth-preference Y range
        float depthMin, depthMax;
        GetDepthRange(out depthMin, out depthMax);
        depthMin = Mathf.Max(depthMin, padMinY);
        depthMax = Mathf.Min(depthMax, padMaxY);

        // Lerp Y range between preferred and full range based on depthBias
        float finalMinY = Mathf.Lerp(padMinY, depthMin, depthBias);
        float finalMaxY = Mathf.Lerp(padMaxY, depthMax, depthBias);

        Vector2 target = new Vector2(
            Random.Range(padMinX, padMaxX),
            Random.Range(finalMinY, finalMaxY)
        );

        // Optionally bias target toward center-of-mass of nearby fish
        if (biasTowardNeighbors)
        {
            Vector2 centerOfMass = Vector2.zero;
            int     count        = Physics2D.OverlapCircleNonAlloc(transform.position, neighborRadius, _overlapBuffer, fishLayer);
            int     valid        = 0;
            for (int i = 0; i < count; i++)
            {
                if (_overlapBuffer[i] != null && _overlapBuffer[i].gameObject != gameObject)
                {
                    centerOfMass += (Vector2)_overlapBuffer[i].transform.position;
                    valid++;
                }
            }
            if (valid > 0)
                target = Vector2.Lerp(target, centerOfMass / valid, sociability * 0.5f);
        }

        bezierEnd = ClampPositionToBounds(target);

        // Quadratic Bezier control point for organic arc
        Vector2 dir      = (bezierEnd - bezierStart).normalized;
        Vector2 perp     = Vector2.Perpendicular(dir);
        float   offset   = Random.Range(-curveStrength, curveStrength);
        bezierControl    = ClampPositionToBounds((bezierStart + bezierEnd) * 0.5f + perp * offset);

        bezierT      = 0f;
        targetSpeed  = Random.Range(
            minSpeed + activityLevel * 0.2f,
            maxSpeed * (0.5f + activityLevel * 0.5f)
        );
        currentSpeed = Mathf.Max(currentSpeed, 0.05f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  TAIL WOBBLE  (frequency scales with speed)
    // ═══════════════════════════════════════════════════════════════

    private void ApplyTailWobble()
    {
        float speedFraction = (maxSpeed > minSpeed)
            ? Mathf.InverseLerp(minSpeed, maxSpeed, currentSpeed) : 0f;

        float freq  = Mathf.Lerp(wobbleFreqMin, wobbleFreqMax, speedFraction);
        float scale = Mathf.Lerp(0.25f, 1f, speedFraction);   // less wobble when very slow

        float wobbleY = Mathf.Sin(Time.time * freq + wobblePhase) * wobbleAmplitude * scale * Time.deltaTime;
        transform.position += new Vector3(0f, wobbleY, 0f);
    }



    // ═══════════════════════════════════════════════════════════════
    //  FLIP  (scale-based, no jarring pop)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateFacingDirection()
    {
        float deltaX = transform.position.x - previousPosition.x;
        if (Mathf.Abs(deltaX) > 0.001f)
            targetScaleX = (deltaX > 0f) ? originalScaleX : -originalScaleX;
    }

    private void UpdateFlip()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Lerp(scale.x, targetScaleX, Time.deltaTime * flipSpeed);
        transform.localScale = scale;
        baseScale = transform.localScale;   // keep baseScale in sync for breathing
    }

    // ═══════════════════════════════════════════════════════════════
    //  TILT  (subtle Z-rotation towards swim direction)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateTilt()
    {
        Vector3 delta = transform.position - previousPosition;
        float   desired = delta.sqrMagnitude > 0.00001f
            ? Mathf.Clamp(-delta.y * 90f, -maxTiltAngle, maxTiltAngle)
            : 0f;

        currentTilt = Mathf.Lerp(currentTilt, desired, Time.deltaTime * tiltSmooth);
        transform.rotation = Quaternion.Euler(0f, 0f, currentTilt);
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE TRANSITIONS
    // ═══════════════════════════════════════════════════════════════

    private void EnterIdle()
    {
        state     = FishState.Idle;
        idleTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    // ═══════════════════════════════════════════════════════════════
    //  UTILITY
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Quadratic Bezier: B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2</summary>
    private Vector2 EvaluateQuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private void GetDepthRange(out float yMin, out float yMax)
    {
        float height = maxBounds.y - minBounds.y;
        float bot    = minBounds.y;

        switch (preferredDepth)
        {
            case DepthLayer.Surface:
                yMin = bot + height * 0.65f;
                yMax = maxBounds.y;
                break;
            case DepthLayer.Bottom:
                yMin = bot;
                yMax = bot + height * 0.35f;
                break;
            case DepthLayer.Mid:
                yMin = bot + height * 0.25f;
                yMax = bot + height * 0.75f;
                break;
            default:
                yMin = minBounds.y;
                yMax = maxBounds.y;
                break;
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 m = Input.mousePosition;
        m.z = -Camera.main.transform.position.z;
        return Camera.main.ScreenToWorldPoint(m);
    }

    private bool IsInsideBounds(Vector3 pos)
        => pos.x > minBounds.x && pos.x < maxBounds.x
        && pos.y > minBounds.y && pos.y < maxBounds.y;

    private Vector2 ClampPositionToBounds(Vector2 pos)
    {
        pos.x = Mathf.Clamp(pos.x, minBounds.x + 0.3f, maxBounds.x - 0.3f);
        pos.y = Mathf.Clamp(pos.y, minBounds.y + 0.3f, maxBounds.y - 0.3f);
        return pos;
    }

    private void ClampToBounds()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minBounds.x, maxBounds.x);
        pos.y = Mathf.Clamp(pos.y, minBounds.y, maxBounds.y);
        transform.position = pos;
    }

    // ═══════════════════════════════════════════════════════════════
    //  EDITOR GIZMOS
    // ═══════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Aquarium boundary
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.35f);
        Vector3 center = new Vector3((minBounds.x + maxBounds.x) * 0.5f, (minBounds.y + maxBounds.y) * 0.5f, 0f);
        Vector3 size   = new Vector3(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 0f);
        Gizmos.DrawWireCube(center, size);

        // Padded zone
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
        Gizmos.DrawWireCube(center, size - new Vector3(edgePadding * 2f, edgePadding * 2f, 0f));

        // Depth preference band
        float yMin, yMax;
        GetDepthRange(out yMin, out yMax);
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.12f);
        float bandH  = yMax - yMin;
        Vector3 bandC = new Vector3(center.x, (yMin + yMax) * 0.5f, 0f);
        Gizmos.DrawCube(bandC, new Vector3(size.x, bandH, 0.01f));

        // Flee radius
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, fleeRadius * (1f - boldness * 0.4f));

        // Neighbor radius
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, neighborRadius);

        if (!Application.isPlaying) return;

        // Bezier path
        if (state == FishState.Swimming || state == FishState.Fleeing || state == FishState.Schooling)
        {
            Gizmos.color = state == FishState.Fleeing ? Color.red : Color.yellow;
            Vector2 prev = bezierStart;
            for (int i = 1; i <= 24; i++)
            {
                float   t     = i / 24f;
                Vector2 point = EvaluateQuadraticBezier(bezierStart, bezierControl, bezierEnd, t);
                Gizmos.DrawLine(prev, point);
                prev = point;
            }
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(bezierEnd, 0.12f);
        }

        // Personality display
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.55f,
            $"B:{boldness:F1} A:{activityLevel:F1} S:{sociability:F1}  [{state}]"
        );
    }
#endif
}
