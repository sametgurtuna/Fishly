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
    //  RUNTIME PERSONALITY (copied from FishData, then optionally randomized)
    // ═══════════════════════════════════════════════════════════════
    [Header("Runtime Personality")]
    [Range(0f, 1f)] public float boldness = 0.5f;
    [Range(0f, 1f)] public float activityLevel = 0.5f;
    [Range(0f, 1f)] public float sociability = 0.5f;

    // Runtime movement values. These are intentionally mutable
    // (e.g. FishHunger slows fish by modifying these).
    [HideInInspector] public float minSpeed = 0.5f;
    [HideInInspector] public float maxSpeed = 2.0f;

    private float currentSpeed;
    private float targetSpeed;
    private float speedVelocity;   // SmoothDamp internal

    private float idleTimer;
    private float wobblePhase;         // unique per instance

    private float currentTilt;

    private float targetScaleX;
    private float originalScaleX;

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

    private FishDepthLayer runtimePreferredDepth = FishDepthLayer.Mid;
    private float feedTimer;

    [Header("Technical / Scene Settings")]
    [Tooltip("Layer mask for other fish — set to your Fish layer")]
    public LayerMask fishLayer;
    private float fleeTimer;

    // ═══════════════════════════════════════════════════════════════
    //  STATE MACHINE
    // ═══════════════════════════════════════════════════════════════
    private enum FishState { Idle, Swimming, Schooling, Curious, Feeding, Resting, Fleeing }
    private FishState state = FishState.Idle;

    // ═══════════════════════════════════════════════════════════════
    //  INTERNALS
    // ═══════════════════════════════════════════════════════════════
    private SpriteRenderer  spriteRenderer;
    private FishLifeStage   lifeStage;
    private SpriteRenderer  selectionHaloRenderer;
    private Vector3         previousPosition;
    private Vector3         baseScale;               // before breathing
    private Vector2         schoolingVelocity;       // boids composite

    // Reusable overlap buffer (no per-frame allocations)
    private static readonly Collider2D[] _overlapBuffer = new Collider2D[24];

    // FishData-backed config accessors
    private float SpeedSmoothTime => fishData != null ? fishData.speedSmoothTime : 1.2f;
    private float MinIdleTime => fishData != null ? fishData.minIdleTime : 0.8f;
    private float MaxIdleTime => fishData != null ? fishData.maxIdleTime : 3.5f;
    private float RestChance => fishData != null ? fishData.restChance : 0.12f;
    private float MinRestTime => fishData != null ? fishData.minRestTime : 2.0f;
    private float MaxRestTime => fishData != null ? fishData.maxRestTime : 6.0f;
    private float WobbleFreqMin => fishData != null ? fishData.wobbleFreqMin : 1.8f;
    private float WobbleFreqMax => fishData != null ? fishData.wobbleFreqMax : 7.0f;
    private float WobbleAmplitude => fishData != null ? fishData.wobbleAmplitude : 0.12f;
    private float MaxTiltAngle => fishData != null ? fishData.maxTiltAngle : 14f;
    private float TiltSmooth => fishData != null ? fishData.tiltSmooth : 7f;
    private float FlipSpeed => fishData != null ? fishData.flipSpeed : 8f;
    private float CurveStrength => fishData != null ? fishData.curveStrength : 2.0f;
    private bool CanSurfaceFeed => fishData != null ? fishData.canSurfaceFeed : true;
    private float SurfaceFeedChance => fishData != null ? fishData.surfaceFeedChance : 0.08f;
    private float SurfaceY => fishData != null ? fishData.surfaceY : 3.5f;
    private float FeedDuration => fishData != null ? fishData.feedDuration : 1.6f;
    private bool EnableSchooling => fishData != null ? fishData.enableSchooling : true;
    private float SeparationRadius => fishData != null ? fishData.separationRadius : 1.2f;
    private float NeighborRadius => fishData != null ? fishData.neighborRadius : 4.0f;
    private float SeparationWeight => fishData != null ? fishData.separationWeight : 1.5f;
    private float AlignmentWeight => fishData != null ? fishData.alignmentWeight : 0.8f;
    private float CohesionWeight => fishData != null ? fishData.cohesionWeight : 0.5f;
    private bool EnableCuriosity => fishData != null ? fishData.enableCuriosity : true;
    private float BaseCuriosityChance => fishData != null ? fishData.baseCuriosityChance : 0.14f;
    private float CuriosityStopDistance => fishData != null ? fishData.curiosityStopDistance : 1.4f;
    private float FleeRadius => fishData != null ? fishData.fleeRadius : 2.5f;
    private float FleeSpeedMultiplier => fishData != null ? fishData.fleeSpeedMultiplier : 2.8f;
    private float FleeDuration => fishData != null ? fishData.fleeDuration : 0.8f;
    private float DepthBias => fishData != null ? fishData.depthBias : 0.6f;
    private float SizeVariation => fishData != null ? fishData.sizeVariation : 0.18f;

    public bool IsEgg => lifeStage != null && lifeStage.IsEgg;
    public bool IsBaby => lifeStage != null && lifeStage.IsBaby;
    public bool IsAdult => lifeStage == null || lifeStage.CanBreed;
    public bool CanBreed => fishData != null && fishData.canBreed && IsAdult;
    public bool CanGenerateIncome => IsAdult;

    // ═══════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        lifeStage = GetComponent<FishLifeStage>();

        if (fishData != null && fishData.fishSprite != null && !IsEgg)
            spriteRenderer.sprite = fishData.fishSprite;

        // Copy base movement/personality from FishData, then randomize per-instance if enabled.
        if (fishData != null)
        {
            minSpeed = fishData.minSpeed;
            maxSpeed = fishData.maxSpeed;
            boldness = fishData.defaultBoldness;
            activityLevel = fishData.defaultActivityLevel;
            sociability = fishData.defaultSociability;
            runtimePreferredDepth = fishData.preferredDepth;
        }

        if (fishData != null && fishData.randomisePersonality)
        {
            boldness       = Random.value;
            activityLevel  = Random.value;
            sociability    = Random.value;
        }

        // Unique wobble phase so fish don't oscillate in sync
        wobblePhase = Random.Range(0f, Mathf.PI * 2f);

        // Size variation only applies to normal spawned fish, not eggs or baby growth states.
        if (!IsBaby)
        {
            float scaleMultiplier = 1f + Random.Range(-SizeVariation, SizeVariation);
            transform.localScale *= scaleMultiplier;
        }

        baseScale      = transform.localScale;
        originalScaleX = Mathf.Abs(baseScale.x);
        targetScaleX   = originalScaleX;

        // Assign random depth preference if not set manually
        if (runtimePreferredDepth == FishDepthLayer.Any)
            runtimePreferredDepth = (FishDepthLayer)Random.Range(0, 3);

        // Stagger start so fish don't all move at frame 0
        idleTimer = Random.Range(0f, 1.0f);
        state     = FishState.Idle;

        previousPosition = transform.position;
    }

    void Update()
    {
        if (IsEgg)
            return;

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
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, SpeedSmoothTime);

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

        int count = Physics2D.OverlapCircleNonAlloc(transform.position, NeighborRadius, _overlapBuffer, fishLayer);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _overlapBuffer[i];
            if (col == null || col.gameObject == gameObject) continue;

            Vector2 toNeighbor  = (Vector2)col.transform.position - (Vector2)transform.position;
            float   dist        = toNeighbor.magnitude;

            // Separation — push away from very close fish
            if (dist < SeparationRadius && dist > 0.001f)
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
            separation *= SeparationWeight;
            alignment   = (alignment / neighbors).normalized * AlignmentWeight;
            cohesion    = ((cohesion / neighbors) - (Vector2)transform.position).normalized * CohesionWeight;
            steer       = (separation + alignment + cohesion) * sociability;
        }

        // Blend schooling with current travel direction
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, SpeedSmoothTime);

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

        if (toMouse.magnitude < CuriosityStopDistance)
        {
            EnterIdle();
            return;
        }

        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed * 0.65f, ref speedVelocity, SpeedSmoothTime);
        transform.position += (Vector3)(toMouse.normalized * currentSpeed) * Time.deltaTime;

        UpdateFacingDirection();
        ClampToBounds();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: FEEDING  (swim to surface, nibble, return)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateFeeding()
    {
        Vector2 target     = new Vector2(transform.position.x, SurfaceY);
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
            transform.position = new Vector3(transform.position.x, SurfaceY + nibble, transform.position.z);
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
        float burstTarget = targetSpeed * FleeSpeedMultiplier;
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
        float curiosityChance = BaseCuriosityChance * boldness;

        // 1. Surface feeding
        if (CanSurfaceFeed && rng < SurfaceFeedChance)
        {
            feedTimer    = FeedDuration;
            targetSpeed  = Random.Range(minSpeed, maxSpeed) * 0.6f;
            currentSpeed = Mathf.Max(currentSpeed, 0.05f);
            state        = FishState.Feeding;
            return;
        }

        // 2. Long rest (lazy fish more likely)
        float adjustedRestChance = RestChance + (1f - activityLevel) * 0.15f;
        if (rng < adjustedRestChance)
        {
            idleTimer = Random.Range(MinRestTime, MaxRestTime);
            state     = FishState.Resting;
            return;
        }

        // 3. Curiosity about cursor
        if (EnableCuriosity && rng < curiosityChance)
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
        if (EnableSchooling && sociability > 0.35f)
        {
            int neighborCount = Physics2D.OverlapCircleNonAlloc(transform.position, NeighborRadius, _overlapBuffer, fishLayer);
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
        if (FishSpawnManager.Instance != null && FishSpawnManager.Instance.IsBreedingSelectionMode) return;
        if (IsEgg) return;

        Vector3 clickWorld = GetMouseWorldPosition();
        float   dist       = Vector2.Distance(clickWorld, transform.position);

        // Bold fish have a smaller effective flee radius
        float adjustedRadius = FleeRadius * (1f - boldness * 0.4f);
        if (dist > adjustedRadius) return;

        Vector2 fleeDir    = ((Vector2)transform.position - (Vector2)clickWorld).normalized;
        Vector2 fleeTarget = ClampPositionToBounds((Vector2)transform.position + fleeDir * Random.Range(2.5f, 5f));

        bezierStart   = transform.position;
        bezierEnd     = fleeTarget;
        Vector2 mid   = (bezierStart + bezierEnd) * 0.5f;
        Vector2 perp  = Vector2.Perpendicular((bezierEnd - bezierStart).normalized);
        bezierControl = ClampPositionToBounds(mid + perp * Random.Range(-1.2f, 1.2f));
        bezierT       = 0f;

        targetSpeed  = maxSpeed * FleeSpeedMultiplier;
        currentSpeed = maxSpeed;
        fleeTimer    = FleeDuration;
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
        float finalMinY = Mathf.Lerp(padMinY, depthMin, DepthBias);
        float finalMaxY = Mathf.Lerp(padMaxY, depthMax, DepthBias);

        Vector2 target = new Vector2(
            Random.Range(padMinX, padMaxX),
            Random.Range(finalMinY, finalMaxY)
        );

        // Optionally bias target toward center-of-mass of nearby fish
        if (biasTowardNeighbors)
        {
            Vector2 centerOfMass = Vector2.zero;
            int     count        = Physics2D.OverlapCircleNonAlloc(transform.position, NeighborRadius, _overlapBuffer, fishLayer);
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
        float   offset   = Random.Range(-CurveStrength, CurveStrength);
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

        float freq  = Mathf.Lerp(WobbleFreqMin, WobbleFreqMax, speedFraction);
        float scale = Mathf.Lerp(0.25f, 1f, speedFraction);   // less wobble when very slow

        float wobbleY = Mathf.Sin(Time.time * freq + wobblePhase) * WobbleAmplitude * scale * Time.deltaTime;
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
        scale.x = Mathf.Lerp(scale.x, targetScaleX, Time.deltaTime * FlipSpeed);
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
            ? Mathf.Clamp(-delta.y * 90f, -MaxTiltAngle, MaxTiltAngle)
            : 0f;

        currentTilt = Mathf.Lerp(currentTilt, desired, Time.deltaTime * TiltSmooth);
        transform.rotation = Quaternion.Euler(0f, 0f, currentTilt);
    }

    public void SetBreedingHighlight(bool active)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (active)
        {
            if (selectionHaloRenderer == null)
                CreateSelectionHalo();

            if (selectionHaloRenderer != null)
                selectionHaloRenderer.enabled = true;
        }
        else if (selectionHaloRenderer != null)
        {
            selectionHaloRenderer.enabled = false;
        }
    }

    private void CreateSelectionHalo()
    {
        GameObject halo = new GameObject("BreedingHighlight");
        halo.transform.SetParent(transform, false);
        halo.transform.localPosition = Vector3.zero;
        halo.transform.localScale = Vector3.one * 1.35f;

        selectionHaloRenderer = halo.AddComponent<SpriteRenderer>();
        selectionHaloRenderer.sprite = BreedingHaloFactory.Get();
        selectionHaloRenderer.color = new Color(1f, 1f, 1f, 0.42f);
        selectionHaloRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : -1;
        selectionHaloRenderer.enabled = false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE TRANSITIONS
    // ═══════════════════════════════════════════════════════════════

    private void EnterIdle()
    {
        state     = FishState.Idle;
        idleTimer = Random.Range(MinIdleTime, MaxIdleTime);
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


internal static class BreedingHaloFactory
{
    private static Sprite cached;

    public static Sprite Get()
    {
        if (cached != null)
            return cached;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerRadius = size * 0.5f - 1f;
        float innerRadius = outerRadius * 0.62f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance > outerRadius || distance < innerRadius)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float t = Mathf.InverseLerp(innerRadius, outerRadius, distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, 0.75f * (1f - t)));
            }
        }

        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        cached.name = "BreedingHalo";
        return cached;
    }
}
    private void GetDepthRange(out float yMin, out float yMax)
    {
        float height = maxBounds.y - minBounds.y;
        float bot    = minBounds.y;

        switch (runtimePreferredDepth)
        {
            case FishDepthLayer.Surface:
                yMin = bot + height * 0.65f;
                yMax = maxBounds.y;
                break;
            case FishDepthLayer.Bottom:
                yMin = bot;
                yMax = bot + height * 0.35f;
                break;
            case FishDepthLayer.Mid:
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
        Gizmos.DrawWireSphere(transform.position, FleeRadius * (1f - boldness * 0.4f));

        // Neighbor radius
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, NeighborRadius);

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
