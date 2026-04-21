using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Windows desktop buddy window controller.
/// - Fixed rectangular window at bottom-right
/// - Opaque rendering (camera background color is visible)
/// - Smart click-through: only interactable objects receive clicks
/// </summary>
public class TransparentGame : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("Use camera world size + pixelsPerWorldUnit to calculate rectangular window size")]
    public bool sizeFromCamera = false;

    [Tooltip("When sizeFromCamera is false, keep camera aspect by deriving width from this height")]
    public bool useCameraAspectForManualSize = true;

    [Tooltip("Used when useCameraAspectForManualSize is true")]
    public int manualHeightPixels = 300;

    [Tooltip("Used when sizeFromCamera is false")]
    public Vector2Int manualWindowSize = new Vector2Int(420, 260);

    [Tooltip("World-to-pixel scale used in camera sizing mode")]
    public float pixelsPerWorldUnit = 170f;

    public Vector2Int minWindowSize = new Vector2Int(320, 220);
    public Vector2Int maxWindowSize = new Vector2Int(900, 600);

    public int rightMargin = 24;
    public int bottomMargin = 24;
    public bool alwaysOnTop = true;

    [Header("Input")]
    [Tooltip("When enabled: clicks pass through unless cursor is over interactable layers")]
    public bool smartClickThrough = true;
    public LayerMask interactableLayers = ~0;
    [Range(0.05f, 1f)] public float hoverRadiusWorld = 0.25f;
    [Range(0.01f, 0.2f)] public float clickCheckInterval = 0.03f;

    [Header("Camera")]
    [Tooltip("Optional camera reference. If empty, Camera.main is used")]
    public Camera targetCamera;

    [Tooltip("Apply camera view bounds to fish and spawn systems so fish stay inside visible window")]
    public bool syncAquariumBoundsToCamera = true;

    [Tooltip("Shrink bounds from camera edges by this world padding")]
    public float boundsPadding = 0.2f;

    private Camera cam;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private IntPtr hwnd = IntPtr.Zero;
    private bool currentClickThrough;
    private float nextClickCheckTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
#endif

    private void Start()
    {
        cam = targetCamera != null ? targetCamera : Camera.main;
        StartCoroutine(SetupWindowRoutine());
    }

    private IEnumerator SetupWindowRoutine()
    {
        Vector2Int windowSize = CalculateWindowSize();
        Screen.SetResolution(windowSize.x, windowSize.y, FullScreenMode.Windowed);
        yield return null;

        if (syncAquariumBoundsToCamera)
            SyncAquariumBounds();

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero)
        {
            Debug.LogWarning("[TransparentGame] Could not get window handle.");
            yield break;
        }

        MakeBorderless(hwnd);
        MoveToBottomRight(hwnd);

        if (alwaysOnTop)
            SetTopMost(hwnd);

        // Start with non-click-through so first interaction is reliable.
        SetClickThroughIfChanged(false);
#endif
    }

    private Vector2Int CalculateWindowSize()
    {
        if (sizeFromCamera && cam != null && cam.orthographic)
        {
            float worldHeight = cam.orthographicSize * 2f;
            float worldWidth = worldHeight * Mathf.Max(0.01f, cam.aspect);

            int width = Mathf.RoundToInt(worldWidth * pixelsPerWorldUnit);
            int height = Mathf.RoundToInt(worldHeight * pixelsPerWorldUnit);

            width = Mathf.Clamp(width, minWindowSize.x, maxWindowSize.x);
            height = Mathf.Clamp(height, minWindowSize.y, maxWindowSize.y);

            return new Vector2Int(width, height);
        }

        if (useCameraAspectForManualSize && cam != null)
        {
            int h = Mathf.Clamp(manualHeightPixels, minWindowSize.y, maxWindowSize.y);
            int w = Mathf.RoundToInt(h * Mathf.Max(0.01f, cam.aspect));
            w = Mathf.Clamp(w, minWindowSize.x, maxWindowSize.x);
            return new Vector2Int(w, h);
        }

        return new Vector2Int(
            Mathf.Clamp(manualWindowSize.x, minWindowSize.x, maxWindowSize.x),
            Mathf.Clamp(manualWindowSize.y, minWindowSize.y, maxWindowSize.y)
        );
    }

    private void SyncAquariumBounds()
    {
        if (cam == null || !cam.orthographic)
            return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        Vector2 min = new Vector2(
            cam.transform.position.x - halfW + boundsPadding,
            cam.transform.position.y - halfH + boundsPadding
        );
        Vector2 max = new Vector2(
            cam.transform.position.x + halfW - boundsPadding,
            cam.transform.position.y + halfH - boundsPadding
        );

        FishAI[] allFish = FindObjectsOfType<FishAI>();
        for (int i = 0; i < allFish.Length; i++)
        {
            if (allFish[i] == null) continue;
            allFish[i].minBounds = min;
            allFish[i].maxBounds = max;
            allFish[i].edgePadding = 0f;
        }

        FishSpawnManager[] spawnManagers = FindObjectsOfType<FishSpawnManager>();
        for (int i = 0; i < spawnManagers.Length; i++)
        {
            if (spawnManagers[i] == null) continue;
            spawnManagers[i].minBounds = min;
            spawnManagers[i].maxBounds = max;
            spawnManagers[i].edgePadding = 0f;
        }
    }

    private void Update()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (hwnd == IntPtr.Zero) return;

        if (smartClickThrough && Time.unscaledTime >= nextClickCheckTime)
        {
            nextClickCheckTime = Time.unscaledTime + clickCheckInterval;
            bool overInteractable = IsCursorOverInteractable();
            SetClickThroughIfChanged(!overInteractable);
        }
#endif
    }

    private bool IsCursorOverInteractable()
    {
        if (cam == null)
            cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return false;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!GetCursorPos(out POINT p))
            return false;
        if (!ScreenToClient(hwnd, ref p))
            return false;

        float sx = p.X;
        float sy = Screen.height - p.Y;
        if (sx < 0 || sx > Screen.width || sy < 0 || sy > Screen.height)
            return false;

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(sx, sy, cam.nearClipPlane));
        world.z = 0f;

        if (Physics2D.OverlapPoint(world, interactableLayers) != null)
            return true;

        if (Physics2D.OverlapCircle(world, hoverRadiusWorld, interactableLayers) != null)
            return true;
#endif

        return false;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private static void MakeBorderless(IntPtr h)
    {
        SetWindowLong(h, GWL_STYLE, WS_POPUP | WS_VISIBLE);
        SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
    }

    private void MoveToBottomRight(IntPtr h)
    {
        if (!GetWindowRect(h, out RECT rect))
            return;

        int w = rect.Right - rect.Left;
        int hgt = rect.Bottom - rect.Top;

        RECT work = GetWorkAreaForWindow(h);

        int x = work.Right - w - rightMargin;
        int y = work.Bottom - hgt - bottomMargin;

        x = Mathf.Clamp(x, work.Left, Mathf.Max(work.Left, work.Right - w));
        y = Mathf.Clamp(y, work.Top, Mathf.Max(work.Top, work.Bottom - hgt));

        SetWindowPos(h, IntPtr.Zero, x, y, 0, 0,
            SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private static RECT GetWorkAreaForWindow(IntPtr h)
    {
        IntPtr monitor = MonitorFromWindow(h, MONITOR_DEFAULTTONEAREST);
        MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };

        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref mi))
            return mi.rcWork;

        return new RECT
        {
            Left = 0,
            Top = 0,
            Right = Screen.currentResolution.width,
            Bottom = Screen.currentResolution.height
        };
    }

    private static void SetTopMost(IntPtr h)
    {
        SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private void SetClickThroughIfChanged(bool enabled)
    {
        if (currentClickThrough == enabled)
            return;

        uint exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (enabled)
            exStyle |= WS_EX_TRANSPARENT;
        else
            exStyle &= ~WS_EX_TRANSPARENT;

        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        currentClickThrough = enabled;
    }
#endif

    private void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (hwnd == IntPtr.Zero)
            hwnd = GetActiveWindow();

        if (hwnd != IntPtr.Zero)
        {
            MoveToBottomRight(hwnd);
            if (alwaysOnTop)
                SetTopMost(hwnd);
        }
#endif

        if (syncAquariumBoundsToCamera)
            SyncAquariumBounds();
    }
}