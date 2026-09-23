using UnityEngine;

public class BattleUnit_AnimationEvents : MonoBehaviour
{
    public BattleUnit ownerUnit;
    [Header("Sword Trail")]
    public SwordBladeTrail swordTrail;
    public void AnimEvent_OpenParry()
    {
        if (ownerUnit == null || ownerUnit.IsDead)
            return;

        // Shoot / Beam dùng VFX Parry Timing thì bỏ qua event Parry cũ.
        if (CombatManager.Instance != null &&
            CombatManager.Instance.ShouldUseVfxParryTiming(ownerUnit))
        {
            return;
        }

        Debug.Log(
            $"[Animation Event] {ownerUnit.unitName} MỞ cửa sổ Parry!"
        );

        if (ParrySystem.Instance != null)
        {
            ParrySystem.Instance.OpenWindow();
        }
    }

    public void AnimEvent_CloseParry()
    {
        if (ownerUnit == null || ownerUnit.IsDead)
            return;

        // Shoot / Beam dùng VFX Parry Timing thì CombatManager tự đóng.
        if (CombatManager.Instance != null &&
            CombatManager.Instance.ShouldUseVfxParryTiming(ownerUnit))
        {
            return;
        }

        Debug.Log(
            $"[Animation Event] {ownerUnit.unitName} ĐÓNG cửa sổ Parry!"
        );

        if (ParrySystem.Instance != null)
        {
            ParrySystem.Instance.CloseWindow();
        }
    }

    public void AnimEvent_DealDamage()
    {
        if (ownerUnit == null || ownerUnit.IsDead)
            return;

        if (CombatManager.Instance == null)
            return;

        // Shoot / Beam gây damage khi VFX impact.
        // Nếu clip cũ vẫn còn DealDamage event thì bỏ qua hoàn toàn.
        if (CombatManager.Instance.IsVfxImpactDamageAction(ownerUnit))
        {
            Debug.Log(
                $"[Animation Event] {ownerUnit.unitName}: " +
                "bỏ qua DealDamage vì Shoot/Beam dùng VFX Impact Damage."
            );

            return;
        }

        Debug.Log(
            $"[Animation Event] {ownerUnit.unitName} " +
            "chạm mục tiêu (DealDamage)!"
        );

        CombatManager.Instance.ApplyDamageFromAnimation(
            ownerUnit
        );

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
