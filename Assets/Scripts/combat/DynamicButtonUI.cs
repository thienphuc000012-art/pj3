using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class DynamicButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [Header("1. Hiệu ứng Xuất hiện (Entrance - Từ tâm ra rìa)")]
    public Vector2 startOffset = new Vector2(0f, -80f);
    public float entranceSpeed = 10f;
    public float startRotationAngle = 15f;

    [Header("2. Hiệu ứng Tương tác (Hover / Click)")]
    public Vector2 hoverMoveOffset = new Vector2(20f, 0f);
    public float hoverScale = 1.15f;
    public float smoothSpeed = 18f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private Vector2 originalPos;
    private Vector3 originalScale;
    private Quaternion originalRotation;

    private Vector2 targetPos;
    private Vector3 targetScale;
    private Quaternion targetRotation;

    private bool isHovered = false;
    private bool hasInitialized = false;
    private bool isFullyAppeared = false;
    private UIHoverOutline hoverOutline;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        RefreshHoverGraphic();
    }

    public void RefreshHoverGraphic()
    {
        var graphic = GetComponent<Graphic>();
        var button = GetComponent<Button>();
        if (button != null && button.targetGraphic != null) graphic = button.targetGraphic;
        if (graphic != null)
        {
            hoverOutline = graphic.GetComponent<UIHoverOutline>();
            if (hoverOutline == null) hoverOutline = graphic.gameObject.AddComponent<UIHoverOutline>();
            hoverOutline.Configure(new Color(.88f, .9f, .93f, .95f));
        }
    }

    void OnEnable()
    {
        if (!hasInitialized)
        {
            originalPos = rectTransform.anchoredPosition;
            originalScale = rectTransform.localScale;
            originalRotation = rectTransform.localRotation;
            hasInitialized = true;
        }

        ResetAppearance();
    }

    private void ResetAppearance()
    {
        isHovered = false;
        isFullyAppeared = false;

        targetScale = originalScale;
        targetPos = originalPos;
        targetRotation = originalRotation;

        rectTransform.anchoredPosition = originalPos + startOffset;
        rectTransform.localScale = originalScale * 0.7f;
        rectTransform.localRotation = Quaternion.Euler(0, 0, originalRotation.eulerAngles.z + startRotationAngle);
        canvasGroup.alpha = 0f;
    }

    void Update()
    {
        if (!hasInitialized) return;

        // Chỉ xuất hiện khi đang trong lượt của Player
        if (CombatManager.Instance != null && CombatManager.Instance.state != CombatState.PlayerTurn)
        {
            ResetAppearance();
            return;
        }

        // Giai đoạn 1: Hiệu ứng xuất hiện
        if (!isFullyAppeared)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, originalPos, Time.deltaTime * entranceSpeed);
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, originalScale, Time.deltaTime * entranceSpeed);
            rectTransform.localRotation = Quaternion.Lerp(rectTransform.localRotation, originalRotation, Time.deltaTime * entranceSpeed);

            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, Time.deltaTime * entranceSpeed);

            if (Vector2.Distance(rectTransform.anchoredPosition, originalPos) < 1f && canvasGroup.alpha > 0.98f)
            {
                isFullyAppeared = true;
                rectTransform.anchoredPosition = originalPos;
                canvasGroup.alpha = 1f;
            }
        }
        else
        {
            // Giai đoạn 2: Tương tác (Đã loại bỏ hoàn toàn hiệu ứng nhịp thở/pulse ở đây)
            // Hover feedback is an edge outline only; keep the skill tile fixed in place.
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, originalPos, Time.deltaTime * smoothSpeed);
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, originalScale, Time.deltaTime * smoothSpeed);
        }
    }

    // --- Các hàm sự kiện tương tác ---
    public void OnPointerEnter(PointerEventData eventData) => SetHoverState();
    public void OnSelect(BaseEventData eventData) => SetHoverState();
    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current.currentSelectedGameObject != this.gameObject) SetNormalState();
    }
    public void OnDeselect(BaseEventData eventData) => SetNormalState();
    public void OnPointerDown(PointerEventData eventData) { }
    public void OnPointerUp(PointerEventData eventData) { }

    private void SetHoverState() => isHovered = true;
    private void SetNormalState() => isHovered = false;
}
