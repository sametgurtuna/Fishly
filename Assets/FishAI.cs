using UnityEngine;

/// <summary>
/// Advanced fish AI with natural swimming, smooth curved paths,
/// multiple behavior states, cursor curiosity, and edge avoidance.
/// Designed for idle aquarium games (Idlequarium style).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FishAI : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  DATA
    // ═══════════════════════════════════════════════════════════════
    [Header("Fish Data")]
    public FishData fishData;

    // ═══════════════════════════════════════════════════════════════
    //  MOVEMENT
    // ═══════════════════════════════════════════════════════════════
    [Header("Movement")]
    [Range(0.3f, 2f)] public float minSpeed = 0.5f;
    [Range(0.5f, 4f)] public float maxSpeed = 1.8f;
    private float currentSpeed;
    private float targetSpeed;

    [Header("Acceleration")]
    [Tooltip("How smoothly the fish accelerates/decelerates")]
    [Range(0.5f, 5f)] public float speedSmoothTime = 1.5f;
    private float speedVelocity; // for SmoothDamp

    // ═══════════════════════════════════════════════════════════════
    //  IDLE / WAIT
    // ═══════════════════════════════════════════════════════════════
    [Header("Idle")]
    public float minIdleTime = 0.8f;
    public float maxIdleTime = 4.0f;
    private float idleTimer;

    // ═══════════════════════════════════════════════════════════════
    //  WOBBLE (natural body oscillation)
    // ═══════════════════════════════════════════════════════════════
    [Header("Natural Wobble")]
    [Tooltip("Vertical sine oscillation frequency")]
    [Range(1f, 6f)] public float wobbleSpeed = 2.5f;
    [Tooltip("Vertical sine oscillation strength")]
    [Range(0.02f, 0.6f)] public float wobbleIntensity = 0.15f;
    private float wobbleOffset;

    // ═══════════════════════════════════════════════════════════════
    //  SMOOTH ROTATION (tilt towards swim direction)
    // ═══════════════════════════════════════════════════════════════
    [Header("Rotation")]
    [Tooltip("How much the fish tilts in its swim direction (degrees)")]
    [Range(0f, 25f)] public float maxTiltAngle = 12f;
    [Tooltip("How smoothly the fish tilts")]
    [Range(1f, 15f)] public float tiltSmooth = 5f;
    private float currentTilt;

    // ═══════════════════════════════════════════════════════════════
    //  SMOOTH FLIP (scale-based, no pop)
    // ═══════════════════════════════════════════════════════════════
    [Header("Flip")]
    [Tooltip("How fast the sprite flips horizontally")]
    [Range(2f, 20f)] public float flipSpeed = 8f;
    private float targetScaleX;
    private float originalScaleX;

    // ═══════════════════════════════════════════════════════════════
    //  BEZIER CURVED PATH
    // ═══════════════════════════════════════════════════════════════
    [Header("Curved Path")]
    [Tooltip("How much the path curves (0 = straight line)")]
    [Range(0f, 4f)] public float curveStrength = 1.8f;
    private Vector2 bezierStart;
    private Vector2 bezierControl;
    private Vector2 bezierEnd;
    private float bezierT; // 0 → 1 progress along the curve

    // ═══════════════════════════════════════════════════════════════
    //  AQUARIUM BOUNDS & EDGE AVOIDANCE
    // ═══════════════════════════════════════════════════════════════
    [Header("Aquarium Bounds")]
    public Vector2 minBounds = new Vector2(-7.5f, -4f);
    public Vector2 maxBounds = new Vector2(7.5f, 4f);

    [Tooltip("Soft margin – fish prefers to stay this far from edges")]
    [Range(0.2f, 2f)] public float edgePadding = 1.0f;

    // ═══════════════════════════════════════════════════════════════
    //  CURIOSITY (reacts to mouse/tap)
    // ═══════════════════════════════════════════════════════════════
    [Header("Curiosity")]
    [Tooltip("Should the fish sometimes swim towards the cursor?")]
    public bool enableCuriosity = true;
    [Range(0f, 1f)] public float curiosityChance = 0.15f;
    [Tooltip("How close the fish will swim to the cursor")]
    [Range(0.5f, 3f)] public float curiosityStopDistance = 1.5f;

    // ═══════════════════════════════════════════════════════════════
    //  FLEE (reacts to sudden taps / clicks)
    // ═══════════════════════════════════════════════════════════════
    [Header("Flee")]
    [Tooltip("How close a click must be to scare the fish")]
    [Range(0.5f, 5f)] public float fleeRadius = 2.5f;
    [Tooltip("Speed multiplier when fleeing")]
    [Range(1.5f, 4f)] public float fleeSpeedMultiplier = 2.5f;
    [Range(0.3f, 2f)] public float fleeDuration = 0.7f;
    private float fleeTimer;

    // ═══════════════════════════════════════════════════════════════
    //  SIZE VARIATION
    // ═══════════════════════════════════════════════════════════════
    [Header("Individual Variation")]
    [Tooltip("Random scale variation per fish instance")]
    [Range(0f, 0.3f)] public float sizeVariation = 0.15f;

    // ═══════════════════════════════════════════════════════════════
    //  STATE MACHINE
    // ═══════════════════════════════════════════════════════════════
    private enum FishState { Swimming, Idle, Curious, Fleeing }
    private FishState state = FishState.Idle;

    // ═══════════════════════════════════════════════════════════════
    //  INTERNALS
    // ═══════════════════════════════════════════════════════════════
    private SpriteRenderer spriteRenderer;
    private Vector3 previousPosition;

    // ═══════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Apply FishData visuals
        if (fishData != null && fishData.fishSprite != null)
        {
            spriteRenderer.sprite = fishData.fishSprite;
        }

        // Each fish gets a unique wobble phase so they don't all bob in sync
        wobbleOffset = Random.Range(0f, Mathf.PI * 2f);

        // Individual size variation
        float scaleMultiplier = 1f + Random.Range(-sizeVariation, sizeVariation);
        transform.localScale *= scaleMultiplier;

        // Cache original X scale for smooth flipping
        originalScaleX = Mathf.Abs(transform.localScale.x);
        targetScaleX = originalScaleX;

        // Small random start delay so all fish don't move at frame 0
        idleTimer = Random.Range(0f, 0.5f);
        state = FishState.Idle;

        previousPosition = transform.position;
    }

    void Update()
    {
        // Check for flee trigger (mouse click nearby)
        CheckFleeTrigger();

        switch (state)
        {
            case FishState.Idle:
                UpdateIdle();
                break;
            case FishState.Swimming:
                UpdateSwimming();
                break;
            case FishState.Curious:
                UpdateCurious();
                break;
            case FishState.Fleeing:
                UpdateFleeing();
                break;
        }

        // Smooth flip & tilt are always running
        UpdateFlip();
        UpdateTilt();
        ApplyWobble();

        previousPosition = transform.position;
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: IDLE
    // ═══════════════════════════════════════════════════════════════

    private void UpdateIdle()
    {
        // Gentle drift while idle (very slow, barely noticeable)
        float driftX = Mathf.Sin(Time.time * 0.3f + wobbleOffset) * 0.05f;
        transform.position += new Vector3(driftX, 0, 0) * Time.deltaTime;

        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
        {
            ChooseNextBehavior();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: SWIMMING (Bezier curve patrol)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateSwimming()
    {
        // Smoothly ramp speed
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, speedSmoothTime);

        // Advance along the bezier path
        float distance = Vector2.Distance(bezierStart, bezierEnd);
        float travelSpeed = (distance > 0.01f) ? (currentSpeed / distance) : 1f;
        bezierT += travelSpeed * Time.deltaTime;
        bezierT = Mathf.Clamp01(bezierT);

        Vector2 newPos = EvaluateQuadraticBezier(bezierStart, bezierControl, bezierEnd, bezierT);
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);

        // Update facing direction based on actual movement delta
        UpdateFacingDirection();

        // Arrived?
        if (bezierT >= 1f)
        {
            EnterIdle();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: CURIOUS (swim towards the mouse cursor)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateCurious()
    {
        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector2 toMouse = (Vector2)mouseWorld - (Vector2)transform.position;
        float dist = toMouse.magnitude;

        if (dist < curiosityStopDistance)
        {
            // Close enough, go idle and look at mouse
            EnterIdle();
            return;
        }

        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed * 0.7f, ref speedVelocity, speedSmoothTime);
        Vector2 dir = toMouse.normalized;
        transform.position += (Vector3)(dir * currentSpeed) * Time.deltaTime;

        UpdateFacingDirection();
        ClampToBounds();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE: FLEEING (dash away from a click)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateFleeing()
    {
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed * fleeSpeedMultiplier, ref speedVelocity, 0.1f);

        bezierT += (currentSpeed / Mathf.Max(Vector2.Distance(bezierStart, bezierEnd), 0.1f)) * Time.deltaTime;
        bezierT = Mathf.Clamp01(bezierT);

        Vector2 newPos = EvaluateQuadraticBezier(bezierStart, bezierControl, bezierEnd, bezierT);
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);

        UpdateFacingDirection();

        fleeTimer -= Time.deltaTime;
        if (fleeTimer <= 0f || bezierT >= 1f)
        {
            EnterIdle();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  BEHAVIOR SELECTION
    // ═══════════════════════════════════════════════════════════════

    private void ChooseNextBehavior()
    {
        // Small chance to be curious about the cursor
        if (enableCuriosity && Random.value < curiosityChance)
        {
            Vector3 mouseWorld = GetMouseWorldPosition();
            if (IsInsideBounds(mouseWorld))
            {
                targetSpeed = Random.Range(minSpeed, maxSpeed);
                currentSpeed = 0.1f;
                state = FishState.Curious;
                return;
            }
        }

        // Default: swim to a new patrol point via bezier curve
        SetupBezierPath();
        state = FishState.Swimming;
    }

    // ═══════════════════════════════════════════════════════════════
    //  FLEE CHECK
    // ═══════════════════════════════════════════════════════════════

    private void CheckFleeTrigger()
    {
        if (state == FishState.Fleeing) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 clickWorld = GetMouseWorldPosition();
            float dist = Vector2.Distance(clickWorld, transform.position);

            if (dist < fleeRadius)
            {
                // Flee AWAY from the click
                Vector2 fleeDir = ((Vector2)transform.position - (Vector2)clickWorld).normalized;
                Vector2 fleeTarget = (Vector2)transform.position + fleeDir * Random.Range(2f, 4f);
                fleeTarget = ClampPositionToBounds(fleeTarget);

                bezierStart = transform.position;
                bezierEnd = fleeTarget;
                // Curve the flee path a bit
                Vector2 mid = (bezierStart + bezierEnd) * 0.5f;
                Vector2 perpendicular = Vector2.Perpendicular(bezierEnd - bezierStart).normalized;
                bezierControl = mid + perpendicular * Random.Range(-1f, 1f);
                bezierT = 0f;

                targetSpeed = maxSpeed * fleeSpeedMultiplier;
                currentSpeed = maxSpeed; // instant burst
                fleeTimer = fleeDuration;
                state = FishState.Fleeing;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  BEZIER PATH SETUP
    // ═══════════════════════════════════════════════════════════════

    private void SetupBezierPath()
    {
        bezierStart = transform.position;

        // Pick a random target inside padded bounds
        float padMinX = minBounds.x + edgePadding;
        float padMaxX = maxBounds.x - edgePadding;
        float padMinY = minBounds.y + edgePadding;
        float padMaxY = maxBounds.y - edgePadding;

        bezierEnd = new Vector2(
            Random.Range(padMinX, padMaxX),
            Random.Range(padMinY, padMaxY)
        );

        // Create a control point perpendicular to the path for a nice curve
        Vector2 midpoint = (bezierStart + bezierEnd) * 0.5f;
        Vector2 direction = (bezierEnd - bezierStart).normalized;
        Vector2 perpendicular = Vector2.Perpendicular(direction);
        float curveOffset = Random.Range(-curveStrength, curveStrength);
        bezierControl = midpoint + perpendicular * curveOffset;

        // Clamp control point inside bounds so the curve doesn't leave the tank
        bezierControl = ClampPositionToBounds(bezierControl);

        bezierT = 0f;
        targetSpeed = Random.Range(minSpeed, maxSpeed);
        currentSpeed = Mathf.Max(currentSpeed, 0.05f); // avoid starting at zero
    }

    // ═══════════════════════════════════════════════════════════════
    //  WOBBLE (Y-axis sine wave for organic feel)
    // ═══════════════════════════════════════════════════════════════

    private void ApplyWobble()
    {
        // Only apply wobble offset to visual position, not actual transform
        // This approach layers wobble ON TOP of the current movement
        float wobbleY = Mathf.Sin(Time.time * wobbleSpeed + wobbleOffset) * wobbleIntensity * Time.deltaTime;
        transform.position += new Vector3(0, wobbleY, 0);
    }

    // ═══════════════════════════════════════════════════════════════
    //  SMOOTH FLIP (scale-based transition, no jarring pop)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateFacingDirection()
    {
        float deltaX = transform.position.x - previousPosition.x;
        if (Mathf.Abs(deltaX) > 0.001f)
        {
            // If default sprite faces RIGHT, moving right = positive scale
            targetScaleX = (deltaX > 0) ? originalScaleX : -originalScaleX;
        }
    }

    private void UpdateFlip()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Lerp(scale.x, targetScaleX, Time.deltaTime * flipSpeed);
        transform.localScale = scale;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TILT (subtle Z rotation towards swim direction)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateTilt()
    {
        Vector3 delta = transform.position - previousPosition;
        if (delta.sqrMagnitude > 0.00001f)
        {
            // Map vertical movement to a tilt angle
            float desiredTilt = Mathf.Clamp(-delta.y * 80f, -maxTiltAngle, maxTiltAngle);
            currentTilt = Mathf.Lerp(currentTilt, desiredTilt, Time.deltaTime * tiltSmooth);
        }
        else
        {
            // Return to flat when idle
            currentTilt = Mathf.Lerp(currentTilt, 0f, Time.deltaTime * tiltSmooth);
        }

        transform.rotation = Quaternion.Euler(0, 0, currentTilt);
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE TRANSITIONS
    // ═══════════════════════════════════════════════════════════════

    private void EnterIdle()
    {
        state = FishState.Idle;
        idleTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    // ═══════════════════════════════════════════════════════════════
    //  UTILITY
    // ═══════════════════════════════════════════════════════════════

    private Vector2 EvaluateQuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        // B(t) = (1-t)² * P0 + 2(1-t)t * P1 + t² * P2
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = -Camera.main.transform.position.z;
        return Camera.main.ScreenToWorldPoint(mouseScreen);
    }

    private bool IsInsideBounds(Vector3 pos)
    {
        return pos.x > minBounds.x && pos.x < maxBounds.x &&
               pos.y > minBounds.y && pos.y < maxBounds.y;
    }

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

    private void OnDrawGizmosSelected()
    {
        // Draw aquarium bounds
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Vector3 center = new Vector3(
            (minBounds.x + maxBounds.x) / 2,
            (minBounds.y + maxBounds.y) / 2, 0);
        Vector3 size = new Vector3(
            maxBounds.x - minBounds.x,
            maxBounds.y - minBounds.y, 0);
        Gizmos.DrawWireCube(center, size);

        // Draw padded inner zone
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
        Vector3 innerSize = new Vector3(
            size.x - edgePadding * 2,
            size.y - edgePadding * 2, 0);
        Gizmos.DrawWireCube(center, innerSize);

        if (!Application.isPlaying) return;

        // Draw bezier curve
        if (state == FishState.Swimming || state == FishState.Fleeing)
        {
            Gizmos.color = (state == FishState.Fleeing) ? Color.red : Color.yellow;
            Vector2 prev = bezierStart;
            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f;
                Vector2 point = EvaluateQuadraticBezier(bezierStart, bezierControl, bezierEnd, t);
                Gizmos.DrawLine(prev, point);
                prev = point;
            }

            // Target point
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(bezierEnd, 0.15f);
        }
    }
}
