using UnityEngine;

public class BattleUnit_AnimationEvents : MonoBehaviour
{
    public BattleUnit ownerUnit;
    [Header("Sword Trail")]
    public SwordBladeTrail swordTrail;
    public void AnimEvent_OpenParry()
    {
        if (ownerUnit != null) Debug.Log($"[Animation Event] {ownerUnit.unitName} MỞ cửa sổ Parry!");
        ParrySystem.Instance.OpenWindow();
    }

    public void AnimEvent_CloseParry()
    {
        if (ownerUnit != null) Debug.Log($"[Animation Event] {ownerUnit.unitName} ĐÓNG cửa sổ Parry!");
        ParrySystem.Instance.CloseWindow();
    }

    public void AnimEvent_DealDamage()
    {
        if (ownerUnit != null) Debug.Log($"[Animation Event] {ownerUnit.unitName} chạm mục tiêu (DealDamage)!");
        CombatManager.Instance.ApplyDamageFromAnimation(ownerUnit);
        ParrySystem.Instance.CloseWindow();
    }

    public void AnimEvent_EndAttack()
    {
        if (ownerUnit != null) Debug.Log($"[Animation Event] {ownerUnit.unitName} ĐÃ ĐÁNH XONG HOÀN TOÀN!");

        // Bật cờ lên để CombatManager biết là có thể cho nhảy về
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.isAttackAnimationFinished = true;
        }
    }

    public void AnimEvent_PlayVFX()
    {
        if (ownerUnit != null && CombatManager.Instance != null)
        {
            CombatManager.Instance.PlayVFXFromAnimation(ownerUnit);
        }
    }
    public void AnimEvent_PlayCastVFX()
    {
        if (ownerUnit != null && CombatManager.Instance != null)
        {
            CombatManager.Instance.PlayCastVFXFromAnimation(ownerUnit);
        }
    }
    public void AnimEvent_StartTrail()
    {
        if (swordTrail != null)
        {
            swordTrail.StartTrail();
        }
    }

    public void AnimEvent_StopTrail()
    {
        if (swordTrail != null)
        {
            swordTrail.StopTrail();
        }
    }
}