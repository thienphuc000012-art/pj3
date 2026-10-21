using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Draws short ribbons from enemy claw bones while a claw action animates.</summary>
public sealed class ClawAttackTrailController : MonoBehaviour
{
    private const int TrailsPerHand = 3;
    private static Material sharedMaterial;

    private readonly Dictionary<Transform, TrailRenderer[]> trailsByHand =
        new Dictionary<Transform, TrailRenderer[]>();
    private BattleUnit owner;

    public void Begin(ActionData action)
    {
        StopTrail();
        if (action == null || !action.useClawAttackTrailVfx) return;

        if (owner == null) owner = GetComponent<BattleUnit>();
        if (owner == null) return;

        List<Transform> hands = ResolveAttackHands(action.animationTriggerName);
        if (hands.Count == 0) return;

        foreach (Transform hand in hands)
        {
            TrailRenderer[] handTrails = GetOrCreateHandTrails(hand);
            float handWidth = EstimateHandWidth(hand);
            float baseWidth = handWidth * Mathf.Max(.1f, action.clawAttackTrailWidth);
            float spreadWorld = handWidth * Mathf.Max(0f, action.clawAttackTrailHandSpread);
            float rightScale = hand.TransformVector(Vector3.right).magnitude;
            float spreadLocal = spreadWorld / Mathf.Max(.0001f, rightScale);

            for (int i = 0; i < handTrails.Length; i++)
            {
                TrailRenderer trail = handTrails[i];
                trail.Clear();
                trail.time = Mathf.Max(.05f, action.clawAttackTrailLifetime);
                // Use three distinct claw lines instead of stacking equal-width
                // ribbons into one broad, opaque-looking band.
                trail.widthMultiplier = baseWidth * GetLaneWidth(i);
                trail.transform.localPosition = Vector3.right * ((i - 1) * spreadLocal);
                trail.colorGradient = CreateTrailGradient(action.clawAttackTrailColor, GetLaneOpacity(i));
                trail.emitting = true;
            }
        }
    }

    private float EstimateHandWidth(Transform hand)
    {
        Animator animator = owner != null ? owner.animator : null;
        if (animator != null && animator.isHuman)
        {
            bool left = animator.GetBoneTransform(HumanBodyBones.LeftHand) == hand;
            HumanBodyBones[] fingerBones = left
                ? new[]
                {
                    HumanBodyBones.LeftIndexProximal,
                    HumanBodyBones.LeftMiddleProximal,
                    HumanBodyBones.LeftRingProximal,
                    HumanBodyBones.LeftLittleProximal
                }
                : new[]
                {
                    HumanBodyBones.RightIndexProximal,
                    HumanBodyBones.RightMiddleProximal,
                    HumanBodyBones.RightRingProximal,
                    HumanBodyBones.RightLittleProximal
                };
            var fingerPositions = new List<Vector3>(fingerBones.Length);
            foreach (HumanBodyBones fingerBone in fingerBones)
            {
                Transform finger = animator.GetBoneTransform(fingerBone);
                if (finger != null && finger != hand) fingerPositions.Add(finger.position);
            }

            float fingerSpan = GreatestDistance(fingerPositions);
            if (fingerSpan > .01f) return Mathf.Clamp(fingerSpan * 2.2f, .06f, .65f);
        }

        Transform[] descendants = hand.GetComponentsInChildren<Transform>(true);
        var clawPositions = new List<Vector3>();
        foreach (Transform candidate in descendants)
        {
            if (candidate == hand) continue;
            string partName = candidate.name.ToLowerInvariant();
            if (partName.Contains("claw") || partName.Contains("talon") ||
                partName.Contains("finger") || partName.Contains("digit"))
                clawPositions.Add(candidate.position);
        }

        float clawSpan = Mathf.Max(
            ProjectedSpan(clawPositions, hand.right),
            ProjectedSpan(clawPositions, hand.up));
        if (clawSpan > .015f) return Mathf.Clamp(clawSpan, .06f, .65f);

        if (hand.parent != null)
        {
            float limbLength = Vector3.Distance(hand.position, hand.parent.position);
            if (limbLength > .015f) return Mathf.Clamp(limbLength * .55f, .06f, .65f);
        }

        return .12f;
    }

    private static float GreatestDistance(List<Vector3> points)
    {
        float greatest = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
                greatest = Mathf.Max(greatest, Vector3.Distance(points[i], points[j]));
        }
        return greatest;
    }

    private static float ProjectedSpan(List<Vector3> points, Vector3 axis)
    {
        if (points.Count < 2) return 0f;
        axis.Normalize();
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        foreach (Vector3 point in points)
        {
            float projection = Vector3.Dot(point, axis);
            min = Mathf.Min(min, projection);
            max = Mathf.Max(max, projection);
        }
        return max - min;
    }

    public void StopTrail()
    {
        foreach (TrailRenderer[] handTrails in trailsByHand.Values)
        {
            if (handTrails == null) continue;
            foreach (TrailRenderer trail in handTrails)
                if (trail != null) trail.emitting = false;
        }
    }

    private void OnDisable() => StopTrail();

    private List<Transform> ResolveAttackHands(string triggerName)
    {
        var hands = new List<Transform>(2);
        string trigger = (triggerName ?? string.Empty).ToLowerInvariant();
        bool wantsLeft = trigger.Contains("left");
        bool wantsRight = trigger.Contains("right");

        Transform left = ResolveHand(true);
        Transform right = ResolveHand(false);
        if (wantsLeft && left != null) AddUnique(hands, left);
        else if (wantsRight && right != null) AddUnique(hands, right);
        else
        {
            AddUnique(hands, left);
            AddUnique(hands, right);
        }

        if (hands.Count == 0)
            AddUnique(hands, owner.handTransform != null ? owner.handTransform : owner.leftHandTransform);
        return hands;
    }

    private Transform ResolveHand(bool left)
    {
        Transform configured = left ? owner.leftHandTransform : owner.handTransform;
        Animator animator = owner.animator;
        if (animator != null && animator.isHuman)
        {
            Transform humanoid = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (humanoid != null) return humanoid;
        }

        Transform named = FindNamedHand(left);
        return named != null ? named : configured;
    }

    private Transform FindNamedHand(bool left)
    {
        string side = left ? "left" : "right";
        string shortSide = left ? "l" : "r";
        Transform[] descendants = owner.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in descendants)
        {
            string name = candidate.name.ToLowerInvariant().Replace('_', ' ').Trim();
            bool matchingSide = name.Contains(side) || name.Contains(shortSide + " hand") ||
                                name.Contains("hand " + shortSide) || name.Contains("wing" + shortSide + " hand");
            if (matchingSide && (name.Contains("hand") || name.Contains("paw") || name.Contains("claw")))
                return candidate;
        }
        return null;
    }

    private TrailRenderer[] GetOrCreateHandTrails(Transform hand)
    {
        if (trailsByHand.TryGetValue(hand, out TrailRenderer[] existing) && existing != null)
            return existing;

        var trails = new TrailRenderer[TrailsPerHand];
        for (int i = 0; i < trails.Length; i++)
        {
            var trailObject = new GameObject("Claw Slash Trail " + (i + 1));
            trailObject.transform.SetParent(hand, false);
            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = GetSharedMaterial();
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.minVertexDistance = .006f;
            trail.numCornerVertices = 4;
            trail.numCapVertices = 4;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.autodestruct = false;
            trail.emitting = false;
            trail.widthCurve = CreateWidthCurve();
            trails[i] = trail;
        }
        trailsByHand[hand] = trails;
        return trails;
    }

    private static AnimationCurve CreateWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, .015f),
            new Keyframe(.12f, .58f),
            new Keyframe(.34f, 1f),
            new Keyframe(.62f, .62f),
            new Keyframe(.86f, .22f),
            new Keyframe(1f, .01f));
    }

    private static float GetLaneWidth(int index)
    {
        switch (index)
        {
            case 1: return .5f;
            default: return .3f;
        }
    }

    private static float GetLaneOpacity(int index)
    {
        switch (index)
        {
            case 1: return .72f;
            default: return .34f;
        }
    }

    private static Gradient CreateTrailGradient(Color color, float opacity)
    {
        var gradient = new Gradient();
        Color opaque = color;
        opaque.a = 1f;
        float peak = Mathf.Clamp01(opacity * color.a);
        gradient.SetKeys(
            new[] { new GradientColorKey(opaque, 0f), new GradientColorKey(opaque, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(peak * .62f, .12f),
                new GradientAlphaKey(peak, .34f),
                new GradientAlphaKey(peak * .68f, .62f),
                new GradientAlphaKey(peak * .22f, .86f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial != null) return sharedMaterial;
        sharedMaterial = Resources.Load<Material>("Combat/Materials/M_Enemy_Claw_Trail");
        if (sharedMaterial != null) return sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Sprites/Default");
        if (shader == null) return null;

        sharedMaterial = new Material(shader)
        {
            name = "Runtime Enemy Claw Trail Material",
            hideFlags = HideFlags.DontSave
        };
        if (sharedMaterial.HasProperty("_BaseColor")) sharedMaterial.SetColor("_BaseColor", Color.white);
        if (sharedMaterial.HasProperty("_Color")) sharedMaterial.SetColor("_Color", Color.white);
        if (sharedMaterial.HasProperty("_Surface")) sharedMaterial.SetFloat("_Surface", 1f);
        if (sharedMaterial.HasProperty("_Blend")) sharedMaterial.SetFloat("_Blend", 0f);
        if (sharedMaterial.HasProperty("_SrcBlend")) sharedMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (sharedMaterial.HasProperty("_DstBlend")) sharedMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (sharedMaterial.HasProperty("_ZWrite")) sharedMaterial.SetFloat("_ZWrite", 0f);
        sharedMaterial.SetOverrideTag("RenderType", "Transparent");
        sharedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        sharedMaterial.renderQueue = (int)RenderQueue.Transparent;
        return sharedMaterial;
    }

    private static void AddUnique(List<Transform> list, Transform value)
    {
        if (value != null && !list.Contains(value)) list.Add(value);
    }
}
