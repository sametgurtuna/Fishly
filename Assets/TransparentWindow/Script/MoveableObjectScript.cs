using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class MoveableObjectScript : MonoBehaviour
{
    [Header("Ayarlar")]
    public RectTransform panelToMove;
    public float moveDistance = 200f;
    public float moveDuration = 1.0f;

    [Header("Buton Ayarları")]
    public RectTransform buttonToRotate;

    [SerializeField] private Sprite[] originalOpenSprite; // [0]=Normal [1]=Highlighted [2]=Pressed [3]=Selected [4]=Disabled
    [SerializeField] private Sprite[] rotatedOpenSprite;  // aynı sıra

    private float originalPositionX;
    private bool isPanelOpen;

    private Button _button;
    private Image _image;
    void Start()
    {
        if (panelToMove != null)
            originalPositionX = panelToMove.anchoredPosition.x;
        else
            Debug.LogError("Panel To Move atanmamış!", gameObject);

        if (buttonToRotate == null)
        {
            Debug.LogError("Button To Rotate atanmamış!", gameObject);
            return;
        }

        _button = buttonToRotate.GetComponent<Button>();
        _image = buttonToRotate.GetComponent<Image>();

        if (_button == null || _image == null)
            Debug.LogError("Butonda Button veya Image component eksik!", gameObject);

        ApplyButtonVisual(isPanelOpen);
    }
    public void TogglePanel()
    {
        if (panelToMove == null || _button == null || _image == null) return;

        panelToMove.DOKill();

        isPanelOpen = !isPanelOpen;

        float targetX = isPanelOpen ? (originalPositionX - moveDistance) : originalPositionX;
        panelToMove.DOAnchorPosX(targetX, moveDuration).SetEase(Ease.OutQuad);

        ApplyButtonVisual(isPanelOpen);

        StartCoroutine(ClearSelectionNextFrame());
    }
    private System.Collections.IEnumerator ClearSelectionNextFrame()
    {
        yield return null;
        EventSystem.current?.SetSelectedGameObject(null);
    }
    private void ApplyButtonVisual(bool panelOpen)
    {
        Sprite[] set = panelOpen ? rotatedOpenSprite : originalOpenSprite;

        _image.sprite = set[0];

        var st = _button.spriteState;
        st.highlightedSprite = set.Length > 1 ? set[1] : set[0];
        st.pressedSprite     = set.Length > 2 ? set[2] : set[0];
        st.selectedSprite    = set.Length > 3 ? set[3] : st.highlightedSprite;
        st.disabledSprite    = set.Length > 4 ? set[4] : st.disabledSprite;

        _button.spriteState = st;
    }
}