using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored dialogue layout; runtime only supplies text and choice entries.</summary>
[DisallowMultipleComponent]
public sealed class AdventureDialogueView : MonoBehaviour
{
    public Button screenButton;
    public RectTransform dialogueFrame;
    public TMP_Text speaker;
    public TMP_Text line;
    public TMP_Text nextLabel;
    public GameObject optionsPanel;
    public RectTransform choices;
    public Button choiceTemplate;
    [Min(0f), Tooltip("Khoảng cách giữa các lựa chọn, tính theo đơn vị Canvas. Chiều cao lấy từ Choice Template.")]
    public float choiceSpacing = 8f;

    [Tooltip("Độ dịch khung thoại khi panel lựa chọn ẩn, theo đơn vị Canvas. Y âm để đưa xuống dưới. Khi có lựa chọn dùng vị trí gốc trong Canvas.")]
    public Vector2 frameOffsetWithoutOptions = new Vector2(0f, -230f);
    public Color choiceNormalTextColor = new Color(.5f, .5f, .5f, 1f);
    public Color choiceHoverTextColor = Color.white;

    Vector3 framePositionWithOptions;
    Vector3 speakerPositionWithOptions;
    bool layoutCaptured;

    public void CaptureLayout()
    {
        if (layoutCaptured || dialogueFrame == null) return;
        framePositionWithOptions = dialogueFrame.anchoredPosition3D;
        if (speaker != null) speakerPositionWithOptions = speaker.rectTransform.localPosition;
        layoutCaptured = true;
    }

    public void SetOptionsVisible(bool visible)
    {
        CaptureLayout();
        if (dialogueFrame != null)
        {
            Vector3 offset = visible ? Vector3.zero : (Vector3)frameOffsetWithoutOptions;
            dialogueFrame.anchoredPosition3D = framePositionWithOptions + offset;
            // Speaker may be authored beside the frame or inside it.
            if (speaker != null && !speaker.transform.IsChildOf(dialogueFrame))
            {
                var speakerRect = speaker.rectTransform;
                Vector3 worldOffset = dialogueFrame.parent.TransformVector(offset);
                speakerRect.localPosition = speakerPositionWithOptions +
                    speakerRect.parent.InverseTransformVector(worldOffset);
            }
        }
        if (optionsPanel != null) optionsPanel.SetActive(visible);
    }
}
