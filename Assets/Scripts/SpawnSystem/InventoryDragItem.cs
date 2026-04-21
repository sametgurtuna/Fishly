using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private FishSpawnManager spawnManager;
    private int fishIndex;
    private string label;

    private RectTransform dragGhost;

    public void Setup(FishSpawnManager manager, int index, string text)
    {
        spawnManager = manager;
        fishIndex = index;
        label = text;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (spawnManager == null) return;

        Transform dragRoot = spawnManager.GetDragRoot();
        if (dragRoot == null) return;

        GameObject ghostObj = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
        ghostObj.transform.SetParent(dragRoot, false);
        dragGhost = ghostObj.GetComponent<RectTransform>();
        dragGhost.sizeDelta = new Vector2(220f, 48f);

        Image bg = ghostObj.GetComponent<Image>();
        bg.color = new Color(0.25f, 0.38f, 0.52f, 0.9f);
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(dragGhost, false);

        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 0f);
        textRT.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost == null) return;
        dragGhost.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
            Destroy(dragGhost.gameObject);

        if (spawnManager == null) return;
        spawnManager.TryDropFishFromInventory(fishIndex, eventData.position);
    }
}
