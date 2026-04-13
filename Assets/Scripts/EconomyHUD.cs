using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Self-contained economy HUD with adjustable position.
///
/// SETUP:  
///   1. Add this script to an empty GameObject
///   2. Right-click the component → "Build HUD"  
///   3. Done! The UI is created and saved in the scene.
///      It will NOT be recreated on every Play.
///
/// HUD position is adjustable via Inspector sliders (live preview in Play mode).
/// </summary>
public class EconomyHUD : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  POSITION SETTINGS
    // ═══════════════════════════════════════════════════════════════

    [Header("═══ HUD POSITION ═══")]
    [Tooltip("Horizontal position: 0 = left edge, 1 = right edge")]
    [Range(0f, 1f)] public float horizontalPosition = 1f;

    [Tooltip("Vertical position: 0 = bottom edge, 1 = top edge")]
    [Range(0f, 1f)] public float verticalPosition = 1f;

    [Tooltip("Margin from screen edge (pixels)")]
    [Range(0f, 100f)] public float edgeMargin = 30f;

    [Header("═══ HUD SIZE ═══")]
    [Range(150f, 500f)] public float containerWidth = 300f;
    [Range(40f, 120f)] public float containerHeight = 65f;
    [Range(18f, 60f)] public float goldFontSize = 34f;
    [Range(12f, 40f)] public float incomeFontSize = 20f;

    // ═══════════════════════════════════════════════════════════════
    //  ANIMATION SETTINGS
    // ═══════════════════════════════════════════════════════════════

    [Header("═══ ANIMATION ═══")]
    [Range(0.2f, 1.5f)] public float countDuration = 0.5f;
    [Range(0.1f, 0.6f)] public float punchDuration = 0.3f;
    public Ease countEase = Ease.OutCubic;
    [Tooltip("Enable slide-in animation when game starts")]
    public bool playEntranceAnim = false;

    [Header("═══ STYLE ═══")]
    public Color backgroundColor = new Color(0.05f, 0.05f, 0.12f, 0.75f);
    public Color goldColor = new Color(1f, 0.85f, 0.2f);
    public Color incomeColor = new Color(0.4f, 1f, 0.5f, 0.9f);

    [Header("═══ FONT ═══")]
    public TMP_FontAsset customFont;

    // ═══════════════════════════════════════════════════════════════
    //  UI REFERENCES (auto-assigned by Build HUD, saved in scene)
    // ═══════════════════════════════════════════════════════════════

    [Header("═══ REFERENCES (Auto-filled) ═══")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private RectTransform goldContainer;
    [SerializeField] private Image goldBgImage;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI incomePopupText;
    [SerializeField] private RectTransform incomeRT;
    [SerializeField] private RectTransform floatingTextArea;

    // ═══════════════════════════════════════════════════════════════
    //  INTERNALS (not saved)
    // ═══════════════════════════════════════════════════════════════

    private double displayedGold;
    private double previousGold;
    private Tweener goldCountTween;

    // ═══════════════════════════════════════════════════════════════
    //  CONTEXT MENU — BUILD HUD (run once in Editor!)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Right-click the component in Inspector → "Build HUD".
    /// Creates Canvas and all UI elements as scene objects.
    /// Safe to call multiple times — destroys old UI first.
    /// </summary>
    [ContextMenu("Build HUD")]
    public void BuildHUD()
    {
        // Destroy old canvas if exists
        if (hudCanvas != null)
        {
            if (Application.isPlaying)
                Destroy(hudCanvas.gameObject);
            else
                DestroyImmediate(hudCanvas.gameObject);
        }

        // ── CANVAS ──────────────────────────────────────────
        GameObject canvasObj = new GameObject("EconomyCanvas");
        canvasObj.transform.SetParent(transform);

        hudCanvas = canvasObj.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        RectTransform canvasRT = canvasObj.GetComponent<RectTransform>();

        // ── GOLD CONTAINER ──────────────────────────────────
        goldContainer = CreateRect("GoldContainer", canvasRT);
        goldContainer.sizeDelta = new Vector2(containerWidth, containerHeight);

        goldBgImage = goldContainer.gameObject.AddComponent<Image>();
        goldBgImage.color = backgroundColor;

        goldText = CreateTMPText("GoldText", goldContainer, goldFontSize,
            TextAlignmentOptions.MidlineRight, goldColor);
        StretchToParent(goldText.rectTransform, 15f, 0f);
        goldText.text = "0";
        goldText.fontStyle = FontStyles.Bold;

        // ── INCOME TEXT ─────────────────────────────────────
        incomeRT = CreateRect("IncomePopup", canvasRT);
        incomeRT.sizeDelta = new Vector2(200, 30);

        incomePopupText = CreateTMPText("IncomeText", incomeRT, incomeFontSize,
            TextAlignmentOptions.MidlineRight, incomeColor);
        StretchToParent(incomePopupText.rectTransform, 5f, 0f);
        incomePopupText.text = "+0/s";

        // ── FLOATING TEXT AREA ──────────────────────────────
        floatingTextArea = CreateRect("FloatingTextArea", canvasRT);
        floatingTextArea.sizeDelta = new Vector2(250, 50);

        // Apply position
        ApplyPosition();

        Debug.Log("[EconomyHUD] HUD built! Save the scene to keep it.");

        // Mark scene dirty in editor so it saves
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
    }

    // ═══════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    private void Start()
    {
        DOTween.Init();

        // Auto-build if any reference is missing
        if (hudCanvas == null || goldText == null || goldContainer == null
            || incomePopupText == null || incomeRT == null || floatingTextArea == null)
        {
            Debug.Log("[EconomyHUD] References missing — auto-building HUD...");
            BuildHUD();
        }

        if (GameManager.Instance != null)
        {
            displayedGold = GameManager.Instance.Gold;
            previousGold = displayedGold;
            RefreshGoldText(displayedGold);
            UpdateIncomeDisplay(GameManager.Instance.TotalIncomePerSecond);

            GameManager.Instance.OnGoldChanged += OnGoldChanged;
            GameManager.Instance.OnIncomeChanged += OnIncomeChanged;
            GameManager.Instance.OnFishPurchased += OnFishPurchased;
        }

        if (playEntranceAnim)
            PlayEntranceAnimation();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldChanged -= OnGoldChanged;
            GameManager.Instance.OnIncomeChanged -= OnIncomeChanged;
            GameManager.Instance.OnFishPurchased -= OnFishPurchased;
        }
        goldCountTween?.Kill();
    }

    private void OnValidate()
    {
        if (goldContainer != null)
        {
            ApplyPosition();
            ApplyStyle();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  POSITION SYSTEM
    // ═══════════════════════════════════════════════════════════════

    private void ApplyPosition()
    {
        if (goldContainer == null) return;

        Vector2 anchor = new Vector2(horizontalPosition, verticalPosition);

        // Gold container
        goldContainer.anchorMin = anchor;
        goldContainer.anchorMax = anchor;
        goldContainer.pivot = anchor;
        goldContainer.sizeDelta = new Vector2(containerWidth, containerHeight);

        float marginX = horizontalPosition > 0.5f ? -edgeMargin : edgeMargin;
        float marginY = verticalPosition > 0.5f ? -edgeMargin : edgeMargin;
        goldContainer.anchoredPosition = new Vector2(marginX, marginY);

        // Income text
        if (incomeRT != null)
        {
            incomeRT.anchorMin = anchor;
            incomeRT.anchorMax = anchor;
            incomeRT.pivot = anchor;

            float incomeOffsetY = verticalPosition > 0.5f
                ? marginY - containerHeight - 5f
                : marginY + containerHeight + 5f;
            incomeRT.anchoredPosition = new Vector2(marginX, incomeOffsetY);
        }

        // Floating text area
        if (floatingTextArea != null)
        {
            floatingTextArea.anchorMin = anchor;
            floatingTextArea.anchorMax = anchor;
            floatingTextArea.pivot = anchor;

            float floatOffsetY = verticalPosition > 0.5f
                ? marginY + 30f
                : marginY - containerHeight - 40f;
            floatingTextArea.anchoredPosition = new Vector2(marginX, floatOffsetY);
        }
    }

    private void ApplyStyle()
    {
        if (goldBgImage != null) goldBgImage.color = backgroundColor;
        if (goldText != null) { goldText.color = goldColor; goldText.fontSize = goldFontSize; }
        if (incomePopupText != null) { incomePopupText.color = incomeColor; incomePopupText.fontSize = incomeFontSize; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private void OnGoldChanged(double newGold)
    {
        double change = newGold - previousGold;
        bool isIncrease = change > 0;
        previousGold = newGold;

        AnimateCounter(newGold);
        PunchGold(isIncrease);
        FlashGoldColor(isIncrease);

        if (isIncrease && change > 0.1)
            ShowFloatingEarn(change);
    }

    private void OnIncomeChanged(double newIncome)
    {
        UpdateIncomeDisplay(newIncome);
    }

    private void OnFishPurchased(FishData fish)
    {
        SpawnCelebrationText(fish);
    }

    // ═══════════════════════════════════════════════════════════════
    //  GOLD COUNTER ANIMATION
    // ═══════════════════════════════════════════════════════════════

    private void AnimateCounter(double targetGold)
    {
        if (goldText == null) return;
        goldCountTween?.Kill();

        float from = (float)displayedGold;
        float to = (float)targetGold;

        goldCountTween = DOTween.To(
            () => from,
            x => { from = x; displayedGold = x; RefreshGoldText(x); },
            to,
            countDuration
        ).SetEase(countEase).SetUpdate(true);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PUNCH SCALE
    // ═══════════════════════════════════════════════════════════════

    private void PunchGold(bool isIncrease)
    {
        if (goldContainer == null) return;
        DOTween.Kill(goldContainer);
        goldContainer.localScale = Vector3.one;

        float s = isIncrease ? 0.12f : 0.08f;
        goldContainer.DOPunchScale(new Vector3(s, s, 0), punchDuration, 3, 0.5f)
            .SetUpdate(true).SetId(goldContainer);
    }

    // ═══════════════════════════════════════════════════════════════
    //  COLOR FLASH
    // ═══════════════════════════════════════════════════════════════

    private void FlashGoldColor(bool isIncrease)
    {
        if (goldText == null) return;
        Color flash = isIncrease ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.35f, 0.35f);

        goldText.DOKill();
        goldText.DOColor(flash, 0.08f)
            .OnComplete(() => goldText.DOColor(goldColor, 0.35f))
            .SetUpdate(true);
    }

    // ═══════════════════════════════════════════════════════════════
    //  FLOATING "+XX" TEXT
    // ═══════════════════════════════════════════════════════════════

    private void ShowFloatingEarn(double amount)
    {
        if (floatingTextArea == null) return;

        RectTransform rt = CreateRect("Float", floatingTextArea);
        rt.sizeDelta = new Vector2(200, 35);
        TextMeshProUGUI tmp = CreateTMPText("Txt", rt, 22f,
            TextAlignmentOptions.Midline, new Color(0.4f, 1f, 0.5f));
        StretchToParent(tmp.rectTransform, 0f, 0f);
        tmp.text = $"+{GameManager.FormatNumber(amount)}";
        tmp.fontStyle = FontStyles.Bold;

        rt.anchoredPosition = new Vector2(Random.Range(-15f, 15f), 0);
        rt.localScale = Vector3.one * 0.6f;

        float floatDir = verticalPosition > 0.5f ? 50f : -50f;

        Sequence seq = DOTween.Sequence();
        seq.Append(rt.DOScale(1.1f, 0.15f).SetEase(Ease.OutBack));
        seq.Append(rt.DOScale(0.9f, 0.1f));
        seq.Join(rt.DOAnchorPosY(rt.anchoredPosition.y + floatDir, 1.0f).SetEase(Ease.OutCubic));
        seq.Insert(0.5f, tmp.DOFade(0f, 0.5f));
        seq.OnComplete(() => Destroy(rt.gameObject));
        seq.SetUpdate(true);
    }

    // ═══════════════════════════════════════════════════════════════
    //  INCOME DISPLAY
    // ═══════════════════════════════════════════════════════════════

    private void UpdateIncomeDisplay(double incomePerSecond)
    {
        if (incomePopupText == null) return;

        if (incomePerSecond <= 0)
        {
            incomePopupText.text = "+0/s";
            incomePopupText.color = new Color(0.5f, 0.5f, 0.5f);
        }
        else
        {
            incomePopupText.text = $"+{GameManager.FormatNumber(incomePerSecond)}/s";
            incomePopupText.color = incomeColor;

            if (incomeRT != null)
            {
                incomeRT.localScale = Vector3.one;
                incomeRT.DOPunchScale(new Vector3(0.08f, 0.08f, 0f), 0.3f, 3, 0.5f)
                    .SetUpdate(true);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  PURCHASE CELEBRATION
    // ═══════════════════════════════════════════════════════════════

    private void SpawnCelebrationText(FishData fish)
    {
        if (floatingTextArea == null) return;

        RectTransform rt = CreateRect("Celebrate", floatingTextArea);
        rt.sizeDelta = new Vector2(250, 40);
        TextMeshProUGUI tmp = CreateTMPText("Txt", rt, 26f,
            TextAlignmentOptions.Midline, fish.GetRarityColor());
        StretchToParent(tmp.rectTransform, 0f, 0f);
        tmp.text = $"🐟 {fish.fishName}!";
        tmp.fontStyle = FontStyles.Bold;
        rt.localScale = Vector3.zero;

        float floatDir = verticalPosition > 0.5f ? 80f : -80f;
        Sequence seq = DOTween.Sequence();
        seq.Append(rt.DOScale(1.3f, 0.25f).SetEase(Ease.OutBack));
        seq.Append(rt.DOScale(1f, 0.1f));
        seq.Join(rt.DOAnchorPosY(floatDir, 1.5f).SetEase(Ease.OutCubic));
        seq.Insert(1.0f, tmp.DOFade(0f, 0.5f));
        seq.OnComplete(() => Destroy(rt.gameObject));
        seq.SetUpdate(true);

        if (goldContainer != null)
            goldContainer.DOShakePosition(0.3f, 6f, 10, 90, true).SetUpdate(true);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ENTRANCE ANIMATION
    // ═══════════════════════════════════════════════════════════════

    private void PlayEntranceAnimation()
    {
        if (goldContainer != null)
        {
            Vector2 target = goldContainer.anchoredPosition;
            float slideX = horizontalPosition > 0.5f ? 400f : -400f;
            goldContainer.anchoredPosition = target + new Vector2(slideX, 0);
            goldContainer.DOAnchorPos(target, 0.7f)
                .SetEase(Ease.OutBack).SetDelay(0.2f).SetUpdate(true);
        }

        if (incomeRT != null)
        {
            Vector2 target = incomeRT.anchoredPosition;
            float slideX = horizontalPosition > 0.5f ? 300f : -300f;
            incomeRT.anchoredPosition = target + new Vector2(slideX, 0);
            incomeRT.DOAnchorPos(target, 0.7f)
                .SetEase(Ease.OutBack).SetDelay(0.4f).SetUpdate(true);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  TEXT UPDATE
    // ═══════════════════════════════════════════════════════════════

    private void RefreshGoldText(double value)
    {
        if (goldText != null)
            goldText.text = $"{GameManager.FormatNumber(value)}";
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI FACTORY HELPERS
    // ═══════════════════════════════════════════════════════════════

    private RectTransform CreateRect(string name, RectTransform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private TextMeshProUGUI CreateTMPText(string name, RectTransform parent,
        float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        if (customFont != null) tmp.font = customFont;
        return tmp;
    }

    private void StretchToParent(RectTransform rt, float padH, float padV)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padH, padV);
        rt.offsetMax = new Vector2(-padH, -padV);
    }
}
