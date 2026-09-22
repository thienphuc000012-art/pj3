using UnityEngine;

public class BattleUnit_AnimationEvents : MonoBehaviour
{
    public BattleUnit ownerUnit;
    [Header("Sword Trail")]
    public SwordBladeTrail swordTrail;
    public void AnimEvent_OpenParry()
    {
        if (ownerUnit == null || ownerUnit.IsDead) return;
        if (ownerUnit != null) Debug.Log($"[Animation Event] {ownerUnit.unitName} MỞ cửa sổ Parry!");
        ParrySystem.Instance.OpenWindow();
    }

    public void AnimEvent_CloseParry()
    {
        if (ownerUnit == null || ownerUnit.IsDead) return;
        if (ownerUnit != null) Debug.Log($"[Animation Event] {ownerUnit.unitName} ĐÓNG cửa sổ Parry!");
        ParrySystem.Instance.CloseWindow();
    }

    public void AnimEvent_DealDamage()
    {
        if (ownerUnit == null || ownerUnit.IsDead)
            return;

        Debug.Log(
            $"[Animation Event] {ownerUnit.unitName} DealDamage event. " +
            "Shoot/Beam sẽ không damage ở event này; damage được resolve khi VFX impact."
        );

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.ApplyDamageFromAnimation(ownerUnit);
        }

        // Event vẫn có thể dùng để đóng timing Parry.
        // parrySuccessful không bị xóa ở đây; Shoot/Beam sẽ đọc nó lúc impact.
        if (ParrySystem.Instance != null)
        {
            ParrySystem.Instance.CloseWindow();
        }
    }

    public void AnimEvent_EndAttack()
    {
        if (ownerUnit == null || ownerUnit.IsDead) return;
        if (ownerUnit != null) Debug.Log($"[Animation Event] {ownerUnit.unitName} ĐÃ ĐÁNH XONG HOÀN TOÀN!");

        // Bật cờ lên để CombatManager biết là có thể cho nhảy về
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.StopBeamVFX(ownerUnit);
            CombatManager.Instance.isAttackAnimationFinished = true;
        }
    }

    public void AnimEvent_PlayVFX()
    {
        if (ownerUnit == null || ownerUnit.IsDead) return;
        if (ownerUnit != null && CombatManager.Instance != null)
        {
            CombatManager.Instance.PlayVFXFromAnimation(ownerUnit);
        }
    }
    public void AnimEvent_PlayCastVFX()
    {
        if (ownerUnit == null || ownerUnit.IsDead) return;
        if (ownerUnit != null && CombatManager.Instance != null)
        {
            CombatManager.Instance.PlayCastVFXFromAnimation(ownerUnit);
        }
    }
    public void AnimEvent_StartTrail()
    {
        if (ownerUnit == null || ownerUnit.IsDead) return;
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
