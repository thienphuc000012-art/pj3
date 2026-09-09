using UnityEngine;
using System.Collections;

public class AutoBlink : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Kéo thả Skinned Mesh Renderer của nhân vật vào đây")]
    public SkinnedMeshRenderer skinnedMeshRenderer;

    [Header("Blendshape Names")]
    public string blinkLeftName = "eyeBlinkLeft";
    public string blinkRightName = "eyeBlinkRight";

    [Header("Blink Settings")]
    [Tooltip("Thời gian chờ tối thiểu giữa các lần chớp mắt (giây)")]
    public float minBlinkInterval = 2.0f;
    [Tooltip("Thời gian chờ tối đa giữa các lần chớp mắt (giây)")]
    public float maxBlinkInterval = 6.0f;
    [Tooltip("Tốc độ nhắm/mở mắt (giây)")]
    public float blinkDuration = 0.1f;

    private int blinkLeftIndex = -1;
    private int blinkRightIndex = -1;

    void Start()
    {
        // Tự động tìm SkinnedMeshRenderer nếu bạn quên gán trên Inspector
        if (skinnedMeshRenderer == null)
        {
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        }

        if (skinnedMeshRenderer != null)
        {
            // Lấy ID của blendshape dựa vào tên
            blinkLeftIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(blinkLeftName);
            blinkRightIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(blinkRightName);

            if (blinkLeftIndex == -1 || blinkRightIndex == -1)
            {
                Debug.LogWarning("Không tìm thấy blendshape chớp mắt. Hãy kiểm tra lại tên!");
            }
            else
            {
                // Bắt đầu vòng lặp chớp mắt
                StartCoroutine(BlinkRoutine());
            }
        }
        else
        {
            Debug.LogError("Không tìm thấy SkinnedMeshRenderer trên GameObject này!");
        }
    }

    IEnumerator BlinkRoutine()
    {
        while (true)
        {
            // 1. Chờ một khoảng thời gian ngẫu nhiên trước khi chớp mắt
            float waitTime = Random.Range(minBlinkInterval, maxBlinkInterval);
            yield return new WaitForSeconds(waitTime);

            // 2. Nhắm mắt (Blendshape từ 0 -> 100)
            float timer = 0f;
            while (timer < blinkDuration)
            {
                timer += Time.deltaTime;
                float weight = Mathf.Lerp(0f, 100f, timer / blinkDuration);
                SetBlinkWeight(weight);
                yield return null;
            }
            SetBlinkWeight(100f); // Đảm bảo mắt nhắm hoàn toàn

            // 3. Mở mắt (Blendshape từ 100 -> 0)
            timer = 0f;
            while (timer < blinkDuration)
            {
                timer += Time.deltaTime;
                float weight = Mathf.Lerp(100f, 0f, timer / blinkDuration);
                SetBlinkWeight(weight);
                yield return null;
            }
            SetBlinkWeight(0f); // Đảm bảo mắt mở hoàn toàn
        }
    }

    // Hàm hỗ trợ để set giá trị cho cả 2 mắt cùng lúc
    void SetBlinkWeight(float weight)
    {
        if (blinkLeftIndex != -1)
            skinnedMeshRenderer.SetBlendShapeWeight(blinkLeftIndex, weight);

        if (blinkRightIndex != -1)
            skinnedMeshRenderer.SetBlendShapeWeight(blinkRightIndex, weight);
    }
}