using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

/// <summary>
/// Net_graph-style debug overlay for development.
/// Shows FPS, frame time, memory, GC activity, and live aquarium stats.
/// </summary>
public class DebugPerformanceOverlay : MonoBehaviour
{
    [Header("Display")]
    public bool showOverlay = true;
    public bool showDuringBuilds = true;
    [Range(0.1f, 2f)] public float refreshInterval = 0.25f;
    [Range(120f, 360f)] public float width = 180f;
    [Range(60f, 200f)] public float height = 92f;
    [Range(10f, 28f)] public float fontSize = 14f;
    [Range(4f, 32f)] public float edgeMargin = 10f;

    [Header("Style")]
    public Color backgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.78f);
    public Color goodColor = new Color(0.55f, 1f, 0.6f, 1f);
    public Color warnColor = new Color(1f, 0.82f, 0.28f, 1f);
    public Color badColor = new Color(1f, 0.38f, 0.38f, 1f);
    public Color textColor = new Color(0.92f, 0.97f, 1f, 1f);

    [Header("Runtime Data")]
    [SerializeField] private TextMeshProUGUI overlayText;
    [SerializeField] private RectTransform panelRoot;

    private Canvas overlayCanvas;
    private float refreshTimer;
    private int frameCounter;
    private float lastRealtime;
    private double currentFps;
    private float currentFrameMs;
    private long previousMonoAllocBytes;
    private int previousGcCollections;

    private void Awake()
    {
        if (!showOverlay)
            return;

        BuildOverlayIfNeeded();
        previousMonoAllocBytes = GC.GetTotalMemory(false);
        previousGcCollections = GC.CollectionCount(0);
        lastRealtime = Time.realtimeSinceStartup;
    }

    private void OnEnable()
    {
        if (showOverlay && overlayText == null)
            BuildOverlayIfNeeded();
    }

    private void Update()
    {
        if (!showOverlay || overlayText == null)
            return;

        frameCounter++;
        refreshTimer += Time.unscaledDeltaTime;

        if (refreshTimer < refreshInterval)
            return;

        float now = Time.realtimeSinceStartup;
        float elapsed = Mathf.Max(0.0001f, now - lastRealtime);

        currentFps = frameCounter / elapsed;
        currentFrameMs = (elapsed / Mathf.Max(1, frameCounter)) * 1000f;

        long monoNow = GC.GetTotalMemory(false);
        long monoDelta = Mathf.Max(0, (int)(monoNow - previousMonoAllocBytes));
        int gcCount = GC.CollectionCount(0);
        int gcDelta = gcCount - previousGcCollections;

        previousMonoAllocBytes = monoNow;
        previousGcCollections = gcCount;
        lastRealtime = now;
        frameCounter = 0;
        refreshTimer = 0f;

        overlayText.text = BuildText(monoNow, monoDelta, gcDelta);
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayText != null)
            return;

        Canvas existingCanvas = FindObjectOfType<Canvas>();
        if (existingCanvas == null)
        {
            GameObject canvasObj = new GameObject("DebugPerformanceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlayCanvas = canvasObj.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 9999;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            overlayCanvas = existingCanvas;
        }

        GameObject rootObj = new GameObject("DebugPerformanceOverlay", typeof(RectTransform), typeof(Image));
        rootObj.transform.SetParent(overlayCanvas.transform, false);
        panelRoot = rootObj.GetComponent<RectTransform>();
        panelRoot.anchorMin = new Vector2(0f, 1f);
        panelRoot.anchorMax = new Vector2(0f, 1f);
        panelRoot.pivot = new Vector2(0f, 1f);
        panelRoot.sizeDelta = new Vector2(width, height);
        panelRoot.anchoredPosition = new Vector2(edgeMargin, -edgeMargin);

        Image bg = rootObj.GetComponent<Image>();
        bg.color = backgroundColor;

        GameObject labelObj = new GameObject("Text", typeof(RectTransform));
        labelObj.transform.SetParent(rootObj.transform, false);
        RectTransform labelRT = labelObj.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(12f, 10f);
        labelRT.offsetMax = new Vector2(-12f, -10f);

        overlayText = labelObj.AddComponent<TextMeshProUGUI>();
        overlayText.fontSize = fontSize;
        overlayText.alignment = TextAlignmentOptions.TopLeft;
        overlayText.color = textColor;
        overlayText.enableWordWrapping = false;
        overlayText.richText = true;
        overlayText.text = "Debug overlay initializing...";

        if (!showDuringBuilds && Application.isEditor && !Application.isPlaying)
            rootObj.SetActive(false);
    }

    private string BuildText(long monoNowBytes, long monoDeltaBytes, int gcDelta)
    {
        double fps = currentFps;
        float frameMs = currentFrameMs;

        Color fpsColor = fps >= 55f ? goodColor : fps >= 30f ? warnColor : badColor;
        Color msColor = frameMs <= 18f ? goodColor : frameMs <= 33f ? warnColor : badColor;

        int fishCount = FindObjectsOfType<FishAI>().Length;
        int breedingCount = FindObjectsOfType<FishLifeStage>().Length;

        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGBA(fpsColor)}>FPS</color> {fps:0.0}  <color=#{ColorUtility.ToHtmlStringRGBA(msColor)}>{frameMs:0.0} ms</color>");
        builder.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGBA(textColor)}>Fish</color> {fishCount}  <color=#{ColorUtility.ToHtmlStringRGBA(textColor)}>Stages</color> {breedingCount}");
        builder.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGBA(textColor)}>Mono</color> {FormatBytes(monoNowBytes)}  <color=#{ColorUtility.ToHtmlStringRGBA(textColor)}>Alloc</color> {FormatSignedBytes(monoDeltaBytes)}");
        builder.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGBA(textColor)}>GC</color> {gcDelta}");
        return builder.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        double mb = bytes / (1024d * 1024d);
        return mb.ToString("0.0") + " MB";
    }

    private static string FormatSignedBytes(long bytes)
    {
        string prefix = bytes >= 0 ? "+" : "-";
        return prefix + FormatBytes(Math.Abs(bytes));
    }
}