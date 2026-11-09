using System.Collections.Generic;
using UnityEngine;

internal sealed class CombatCameraTargetResolver
{
    static readonly Vector3 BossDetailExtents = new Vector3(2.5f, 2.5f, 2.5f);

    sealed class Subject
    {
        public Vector3 fallback;
        public Vector3 centerOffset;
        public Vector3 extents;
        public CombatCameraFraming framing;
        public Transform humanoidHead;
    }

    readonly Dictionary<BattleUnit, Subject> subjects = new Dictionary<BattleUnit, Subject>();
    readonly CombatManager combat;

    public CombatCameraTargetResolver(CombatManager combat) => this.combat = combat;

    public void RegisterParty()
    {
        foreach (var unit in combat.playerParty) if (unit != null) GetSubject(unit);
        foreach (var unit in combat.enemyParty) if (unit != null) GetSubject(unit);
    }

    Subject GetSubject(BattleUnit unit)
    {
        if (subjects.TryGetValue(unit, out Subject subject)) return subject;

        subject = new Subject { fallback = unit.transform.position, framing = unit.GetComponent<CombatCameraFraming>() };
        Animator animator = unit.animator;
        if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.isHuman)
            subject.humanoidHead = animator.GetBoneTransform(HumanBodyBones.Head);
        var bounds = new Bounds(unit.transform.position + Vector3.up, new Vector3(1, 2, 1));
        bool hasBounds = false;
        foreach (var renderer in unit.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
            var candidate = renderer.bounds;
            if (candidate.size.sqrMagnitude < .0001f) continue;
            if (!hasBounds) bounds = candidate;
            else bounds.Encapsulate(candidate);
            hasBounds = true;
        }
        subject.centerOffset = bounds.center - unit.transform.position;
        subject.extents = bounds.extents;
        subjects.Add(unit, subject);
        return subject;
    }

    public Vector3 BattlePosition(BattleUnit unit)
    {
        var team = unit.isPlayer ? combat.playerParty : combat.enemyParty;
        var slots = unit.isPlayer ? combat.playerSlots : combat.enemySlots;
        int index = team.IndexOf(unit);
        return slots != null && index >= 0 && index < slots.Length && slots[index] != null
            ? slots[index].position : GetSubject(unit).fallback;
    }

    public Vector3 Aim(BattleUnit unit, float liveWeight, bool attack, bool compactBoss)
    {
        var subject = GetSubject(unit);
        var framing = subject.framing;
        Transform anchor = framing != null ? (attack && framing.attackAnchor != null ? framing.attackAnchor : framing.focusAnchor) : null;
        Vector3 root = Vector3.Lerp(BattlePosition(unit), unit.transform.position, liveWeight);
        return root + (anchor != null ? anchor.position - unit.transform.position : CenterOffset(subject, compactBoss));
    }

    public Vector3 SelectionAim(BattleUnit unit, Vector3 fallback)
    {
        var subject = GetSubject(unit);
        var framing = subject.framing;
        if (framing != null)
        {
            if (framing.selectionAnchor != null) return framing.selectionAnchor.position;
            if (framing.focusAnchor != null) return framing.focusAnchor.position;
        }
        return subject.humanoidHead != null ? Vector3.Lerp(fallback, subject.humanoidHead.position, .6f) : fallback;
    }

    public bool TryAddSelectionFramePoints(List<Vector3> points, BattleUnit unit, Vector3 pivot)
    {
        var framing = GetSubject(unit).framing;
        if (framing == null || !HasPositiveDimensions(framing.selectionBoundsSize)) return false;
        Vector3 extents = framing.selectionBoundsSize * .5f;
        AddBox(points, pivot, extents, pivot.y - extents.y);
        return true;
    }

    public void AddFramePoints(List<Vector3> points, BattleUnit unit, float liveWeight, float padding, bool compactBoss)
    {
        var subject = GetSubject(unit);
        var framing = subject.framing;
        Vector3 root = Vector3.Lerp(BattlePosition(unit), unit.transform.position, liveWeight);
        Vector3 center = root + CenterOffset(subject, compactBoss);
        bool explicitSize = HasExplicitSize(framing);
        bool compactAutomatic = compactBoss && !explicitSize;
        Vector3 extents = explicitSize ? framing.boundsSize * .5f : subject.extents;
        if (compactAutomatic) extents = Vector3.Min(extents, BossDetailExtents);
        extents *= padding;
        float bottom = compactAutomatic ? Mathf.Max(root.y, center.y - extents.y) : center.y - extents.y;

        AddBox(points, center, extents, bottom);
        if (framing == null || framing.frameAnchors == null) return;
        foreach (Transform anchor in framing.frameAnchors)
            if (anchor != null) points.Add(root + anchor.position - unit.transform.position);
    }

    static void AddBox(List<Vector3> points, Vector3 center, Vector3 extents, float bottom)
    {
        for (int i = 0; i < 8; i++)
            points.Add(new Vector3(center.x + ((i & 1) == 0 ? -extents.x : extents.x),
                (i & 2) == 0 ? bottom : center.y + extents.y, center.z + ((i & 4) == 0 ? -extents.z : extents.z)));
    }

    static bool HasExplicitSize(CombatCameraFraming framing) => framing != null &&
        HasPositiveDimensions(framing.boundsSize);

    static bool HasPositiveDimensions(Vector3 size) => size.x > 0 && size.y > 0 && size.z > 0;
    static Vector3 CenterOffset(Subject subject, bool compactBoss)
    {
        if (HasExplicitSize(subject.framing)) return subject.framing.boundsCenterOffset;
        if (!compactBoss) return subject.centerOffset;

        // Imported skinned bounds can cover the full sweep of wings and tail.
        // Keep the automatic close-shot center near the body and above its slot.
        Vector3 center = subject.centerOffset;
        Vector3 horizontal = Vector3.ClampMagnitude(new Vector3(center.x, 0, center.z), 1.25f);
        return horizontal + Vector3.up * Mathf.Clamp(center.y, 1.25f, BossDetailExtents.y);
    }

    public BattleUnit Closest(List<BattleUnit> team, BattleUnit origin, bool preferBoss = false)
    {
        Vector3 point = origin != null ? BattlePosition(origin) : combat.transform.position;
        float closest = float.PositiveInfinity;
        BattleUnit best = null;
        foreach (var unit in team)
        {
            if (unit == null || unit.IsDead) continue;
            if (preferBoss && unit.isBoss) return unit;
            float distance = (BattlePosition(unit) - point).sqrMagnitude;
            if (distance < closest)
            {
                closest = distance;
                best = unit;
            }
        }
        return best;
    }

    public Vector3 TeamCenter(List<BattleUnit> team, Vector3 fallback, bool live, bool includeDefeated = false)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var unit in team)
        {
            if (unit == null || (!includeDefeated && unit.IsDead)) continue;
            sum += live ? unit.transform.position : BattlePosition(unit);
            count++;
        }
        return count == 0 ? fallback : sum / count;
    }
}
