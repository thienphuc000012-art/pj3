using UnityEngine;

public class ParrySystem : MonoBehaviour
{
    public static ParrySystem Instance;

    public bool isParryWindowOpen { get; private set; }
    public bool parrySuccessful { get; private set; }

    void Awake() { Instance = this; }

    void Update()
    {
        if (isParryWindowOpen && Input.GetKeyDown(KeyCode.Space))
        {
            parrySuccessful = true;
            isParryWindowOpen = false;

            if (AdvancedUIManager.Instance != null)
            {
                // --- TÌM VỊ TRÍ NHÂN VẬT ĐANG BỊ ĐÁNH ---
                Transform parryTargetTransform = null;
                if (CombatManager.Instance != null && CombatManager.Instance.currentTarget != null)
                {
                    parryTargetTransform = CombatManager.Instance.currentTarget.transform;
                }

                // --- TRUYỀN VỊ TRÍ SANG UI ---
                AdvancedUIManager.Instance.ShowParryText(parryTargetTransform);
            }
        }
    }

    public void OpenWindow()
    {
        isParryWindowOpen = true;
        parrySuccessful = false;
    }

    public void CloseWindow()
    {
        isParryWindowOpen = false;
    }

    public void ResetParryState()
    {
        isParryWindowOpen = false;
        parrySuccessful = false;
    }
}