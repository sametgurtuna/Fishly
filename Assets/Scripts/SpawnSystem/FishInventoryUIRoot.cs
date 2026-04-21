using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class FishInventoryUIRoot : MonoBehaviour
{
    [Header("Root")]
    public Canvas rootCanvas;
    public Transform dragRoot;

    [Header("Context Menu")]
    public RectTransform contextMenu;
    public Button addToInventoryButton;
    public Button sellButton;
    public Button breedButton;

    [Header("Inventory")]
    public Button inventoryToggleButton;
    public GameObject inventoryPanel;
    public Transform inventoryContent;
    public GameObject inventoryItemPrefab;
    public TMP_Text emptyStateLabel;

    public void BindDefaultsIfNeeded()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        if (dragRoot == null)
            dragRoot = transform;

        // Root must fill the canvas, otherwise bottom-right anchored UI can appear near screen center.
        RectTransform rootRT = transform as RectTransform;
        if (rootRT != null)
        {
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            rootRT.anchoredPosition = Vector2.zero;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;
            rootRT.localScale = Vector3.one;
            rootRT.localRotation = Quaternion.identity;
        }

        // Auto-wire common references so prefab setup is more forgiving.
        if (contextMenu == null)
            contextMenu = FindRectTransformByName("ContextMenu");

        if (inventoryPanel == null)
        {
            RectTransform panelRT = FindRectTransformByName("InventoryPanel");
            if (panelRT != null) inventoryPanel = panelRT.gameObject;
        }

        if (inventoryContent == null && inventoryPanel != null)
        {
            Transform content = inventoryPanel.transform.Find("Content");
            if (content == null)
                content = FindTransformByNameUnder(inventoryPanel.transform, "Content");
            if (content != null) inventoryContent = content;
        }

        if (inventoryToggleButton == null)
        {
            RectTransform toggleRT = FindRectTransformByName("InventoryToggleButton");
            if (toggleRT != null)
                inventoryToggleButton = toggleRT.GetComponent<Button>();
        }

        if (addToInventoryButton == null && contextMenu != null)
        {
            Transform t = contextMenu.Find("AddToInventoryButton");
            if (t == null) t = FindTransformByNameUnder(contextMenu, "AddToInventoryButton");
            if (t != null) addToInventoryButton = t.GetComponent<Button>();
        }

        if (sellButton == null && contextMenu != null)
        {
            Transform t = contextMenu.Find("SellButton");
            if (t == null) t = contextMenu.Find("SellFishButton");
            if (t == null) t = FindTransformByNameUnder(contextMenu, "SellButton");
            if (t == null) t = FindTransformByNameUnder(contextMenu, "SellFishButton");
            if (t != null) sellButton = t.GetComponent<Button>();
        }

        if (breedButton == null && contextMenu != null)
        {
            Transform t = contextMenu.Find("BreedButton");
            if (t == null) t = FindTransformByNameUnder(contextMenu, "BreedButton");
            if (t != null)
            {
                breedButton = t.GetComponent<Button>();
            }
            else
            {
                breedButton = CreateContextMenuButton(contextMenu, "BreedButton", "Breed", insertAsLast: true);
            }
        }

        EnsureContextMenuFitsButtons();
    }

    public void ForceBottomRightLayout(float margin = 24f)
    {
        RectTransform rootRT = transform as RectTransform;
        if (rootRT != null)
        {
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            rootRT.anchoredPosition = Vector2.zero;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;
        }

        if (inventoryToggleButton != null)
        {
            RectTransform toggleRT = inventoryToggleButton.GetComponent<RectTransform>();
            if (toggleRT != null)
            {
                toggleRT.anchorMin = new Vector2(1f, 0f);
                toggleRT.anchorMax = new Vector2(1f, 0f);
                toggleRT.pivot = new Vector2(1f, 0f);
                toggleRT.anchoredPosition = new Vector2(-margin, margin);
            }
        }

        if (inventoryPanel != null)
        {
            RectTransform panelRT = inventoryPanel.GetComponent<RectTransform>();
            if (panelRT != null)
            {
                panelRT.anchorMin = new Vector2(1f, 0f);
                panelRT.anchorMax = new Vector2(1f, 0f);
                panelRT.pivot = new Vector2(1f, 0f);
                panelRT.anchoredPosition = new Vector2(-margin, margin + 66f);
            }
        }

        EnsureContextMenuFitsButtons();
    }

    private void EnsureContextMenuFitsButtons()
    {
        if (contextMenu == null) return;

        int buttonCount = 0;
        if (addToInventoryButton != null) buttonCount++;
        if (sellButton != null) buttonCount++;
        if (breedButton != null) buttonCount++;

        float requiredHeight = 24f + (buttonCount * 38f) + Mathf.Max(0, buttonCount - 1) * 8f;
        RectTransform contextRT = contextMenu;
        if (contextRT.sizeDelta.y < requiredHeight)
            contextRT.sizeDelta = new Vector2(contextRT.sizeDelta.x, requiredHeight);
    }

    private RectTransform FindRectTransformByName(string objectName)
    {
        RectTransform[] all = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == objectName)
                return all[i];
        }

        return null;
    }

    private Transform FindTransformByNameUnder(Transform root, string objectName)
    {
        if (root == null) return null;

        List<Transform> stack = new List<Transform> { root };
        while (stack.Count > 0)
        {
            Transform current = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);

            if (current.name == objectName)
                return current;

            for (int i = 0; i < current.childCount; i++)
                stack.Add(current.GetChild(i));
        }

        return null;
    }

    public static FishInventoryUIRoot CreateDefaultUI(Transform parent = null)
    {
        Canvas canvas = null;

        if (parent != null)
            canvas = parent.GetComponentInParent<Canvas>();

        if (canvas == null)
            canvas = Object.FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("FishlyUI_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform rootParent = parent != null ? parent : canvas.transform;

        GameObject rootObj = new GameObject("FishInventoryUIRoot", typeof(RectTransform));
        rootObj.transform.SetParent(rootParent, false);

        FishInventoryUIRoot root = rootObj.AddComponent<FishInventoryUIRoot>();
        root.rootCanvas = canvas;
        root.dragRoot = rootObj.transform;

        GameObject contextObj = new GameObject("ContextMenu", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        contextObj.transform.SetParent(rootObj.transform, false);
        RectTransform contextRT = contextObj.GetComponent<RectTransform>();
        contextRT.sizeDelta = new Vector2(230f, 120f);
        contextRT.pivot = new Vector2(0f, 1f);

        Image contextBg = contextObj.GetComponent<Image>();
        contextBg.color = new Color(0.08f, 0.1f, 0.16f, 0.95f);

        VerticalLayoutGroup contextLayout = contextObj.GetComponent<VerticalLayoutGroup>();
        contextLayout.padding = new RectOffset(12, 12, 12, 12);
        contextLayout.spacing = 8f;
        contextLayout.childControlWidth = true;
        contextLayout.childControlHeight = true;
        contextLayout.childForceExpandHeight = false;

        root.contextMenu = contextRT;
        root.addToInventoryButton = CreateButton(contextRT, "AddToInventoryButton", "Add To Inventory");
        root.sellButton = CreateButton(contextRT, "SellButton", "Sell");
        root.breedButton = CreateButton(contextRT, "BreedButton", "Breed");
        contextObj.SetActive(false);

        GameObject inventoryToggleObj = new GameObject("InventoryToggleButton", typeof(RectTransform), typeof(Image), typeof(Button));
        inventoryToggleObj.transform.SetParent(rootObj.transform, false);
        RectTransform toggleRT = inventoryToggleObj.GetComponent<RectTransform>();
        toggleRT.anchorMin = new Vector2(1f, 0f);
        toggleRT.anchorMax = new Vector2(1f, 0f);
        toggleRT.pivot = new Vector2(1f, 0f);
        toggleRT.anchoredPosition = new Vector2(-24f, 24f);
        toggleRT.sizeDelta = new Vector2(220f, 56f);
        inventoryToggleObj.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.24f, 0.95f);
        root.inventoryToggleButton = inventoryToggleObj.GetComponent<Button>();
        CreateButtonLabel(toggleRT, "Inventory", TextAlignmentOptions.Center);

        GameObject panelObj = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(rootObj.transform, false);
        RectTransform panelRT = panelObj.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1f, 0f);
        panelRT.anchorMax = new Vector2(1f, 0f);
        panelRT.pivot = new Vector2(1f, 0f);
        panelRT.anchoredPosition = new Vector2(-24f, 90f);
        panelRT.sizeDelta = new Vector2(320f, 360f);
        panelObj.GetComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.95f);
        root.inventoryPanel = panelObj;

        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform titleRT = titleObj.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -12f);
        titleRT.sizeDelta = new Vector2(-20f, 38f);
        TMP_Text title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "Fish Inventory";
        title.fontSize = 24f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(0.88f, 0.95f, 1f, 1f);

        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentObj.transform.SetParent(panelObj.transform, false);
        RectTransform contentRT = contentObj.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 0f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.offsetMin = new Vector2(12f, 12f);
        contentRT.offsetMax = new Vector2(-12f, -56f);
        VerticalLayoutGroup vlg = contentObj.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(2, 2, 2, 2);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        root.inventoryContent = contentObj.transform;

        GameObject itemPrefab = new GameObject("InventoryItemTemplate", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        itemPrefab.transform.SetParent(rootObj.transform, false);
        itemPrefab.SetActive(false);
        itemPrefab.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.32f, 1f);
        itemPrefab.GetComponent<LayoutElement>().preferredHeight = 48f;
        RectTransform itemRT = itemPrefab.GetComponent<RectTransform>();
        itemRT.sizeDelta = new Vector2(0f, 48f);
        CreateLabelArea(itemRT, "Fish x0", TextAlignmentOptions.MidlineLeft);
        root.inventoryItemPrefab = itemPrefab;

        GameObject emptyLabelObj = new GameObject("EmptyStateTemplate", typeof(RectTransform));
        emptyLabelObj.transform.SetParent(rootObj.transform, false);
        emptyLabelObj.SetActive(false);
        TMP_Text emptyLabel = emptyLabelObj.AddComponent<TextMeshProUGUI>();
        emptyLabel.text = "Inventory is empty.";
        emptyLabel.fontSize = 20f;
        emptyLabel.alignment = TextAlignmentOptions.Center;
        emptyLabel.color = new Color(0.7f, 0.78f, 0.85f, 1f);
        root.emptyStateLabel = emptyLabel;

        panelObj.SetActive(false);
        return root;
    }

    private static Button CreateButton(Transform parent, string objectName, string text)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.16f, 0.26f, 0.35f, 1f);
        obj.GetComponent<LayoutElement>().preferredHeight = 38f;

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 38f);
        CreateButtonLabel(rt, text, TextAlignmentOptions.Center);

        return obj.GetComponent<Button>();
    }

    private static Button CreateContextMenuButton(Transform parent, string objectName, string text, bool insertAsLast)
    {
        GameObject buttonObj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);
        buttonObj.GetComponent<Image>().color = new Color(0.16f, 0.26f, 0.35f, 1f);
        buttonObj.GetComponent<LayoutElement>().preferredHeight = 38f;

        RectTransform rt = buttonObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 38f);

        if (insertAsLast)
            buttonObj.transform.SetAsLastSibling();

        CreateButtonLabel(rt, text, TextAlignmentOptions.Center);
        return buttonObj.GetComponent<Button>();
    }

    private static TMP_Text CreateButtonLabel(RectTransform parent, string text, TextAlignmentOptions align)
    {
        GameObject labelObj = new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(parent, false);

        RectTransform rt = labelObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TMP_Text label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 20f;
        label.alignment = align;
        label.color = Color.white;
        label.enableWordWrapping = false;
        return label;
    }

    private static TMP_Text CreateLabelArea(RectTransform parent, string text, TextAlignmentOptions align)
    {
        GameObject textObj = new GameObject("Label", typeof(RectTransform));
        textObj.transform.SetParent(parent, false);

        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(12f, 0f);
        textRT.offsetMax = new Vector2(-12f, 0f);

        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 20f;
        label.alignment = align;
        label.color = Color.white;
        label.enableWordWrapping = false;
        return label;
    }
}
