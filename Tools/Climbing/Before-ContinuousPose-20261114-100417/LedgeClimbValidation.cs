using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Read-only scene inspection and isolated climbing checks. Does not start Play Mode.</summary>
public static class LedgeClimbValidation
{
    static float worstFootBefore, worstFootAfter;
    const string Request = "Library/CombatValidation/LedgeClimb.request";
    const string Result = "Library/CombatValidation/LedgeClimb.result.txt";
    [InitializeOnLoadMethod] static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Requested; return; }
        string mode = File.ReadAllText(Request).Trim(); File.Delete(Request);
        if (mode == "validate") Validate(); else Inspect();
    }
    [MenuItem("Tools/Adventure/Climbing/Inspect Ledges")]
    public static void Inspect()
    {
        var lines = new List<string> { "Read-only. Play Mode=" + EditorApplication.isPlaying };
        try
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerClimb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                lines.Add(p.gameObject.scene.name + "/" + p.name + ": root=" + p.transform.position.ToString("F4") + " rot=" + p.transform.eulerAngles +
                    " hang=" + p.isClimbing + " hop=" + p.isHopping + " ledge=" + p.StableLedgeTopPoint.ToString("F4"));
                var anim = p.GetComponent<Animator>();
                lines.Add("Animator=" + AssetDatabase.GetAssetPath(anim.runtimeAnimatorController) + " scale=" + p.transform.lossyScale);
                foreach (var clip in anim.runtimeAnimatorController.animationClips.Distinct().Where(c => c.name.ToLowerInvariant().Contains("hop") || c.name.ToLowerInvariant().Contains("climb") || c.name.ToLowerInvariant().Contains("hang")))
                    lines.Add("Clip " + clip.name + " seconds=" + clip.length + " loop=" + clip.isLooping + " " + AssetDatabase.GetAssetPath(clip));
            }
            foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(c => c.gameObject.scene.name == "m" && c.name.StartsWith("climb", StringComparison.OrdinalIgnoreCase)).OrderBy(c => c.bounds.max.y))
            {
                lines.Add("Ledge " + c.name + " id=" + c.GetInstanceID() + " layer=" + LayerMask.LayerToName(c.gameObject.layer) + " pos=" + c.transform.position.ToString("F4") +
                    " size=" + c.bounds.size.ToString("F4") + " top=" + c.bounds.max.y.ToString("F4") + " forward=" + c.transform.forward.ToString("F4"));
            }
            var controller = UnityEngine.Object.FindObjectsByType<PlayerClimb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault()?.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
            if (controller != null) Describe(controller.layers[0].stateMachine, lines);
        }
        catch (Exception error) { lines.Add(error.ToString()); }
        Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllLines(Result, lines);
    }
    static void Describe(AnimatorStateMachine machine, List<string> lines)
    {
        foreach (var child in machine.states)
        {
            var s = child.state;
            if (!(s.name.Contains("hang") || s.name.Contains("climb") || s.name.Contains("Climb"))) continue;
            lines.Add("State " + s.name + " speed=" + s.speed + " clip=" + s.motion?.name);
            foreach (var t in s.transitions) lines.Add("  -> " + t.destinationState?.name + " exit=" + t.hasExitTime + "/" + t.exitTime + " duration=" + t.duration +
                " conditions=" + string.Join(",", t.conditions.Select(c => c.parameter + "=" + c.threshold)));
        }
        foreach (var child in machine.stateMachines) Describe(child.stateMachine, lines);
    }

    [MenuItem("Tools/Adventure/Climbing/Validate m Ledge Route")]
    public static void Validate()
    {
        worstFootBefore = float.PositiveInfinity; worstFootAfter = float.PositiveInfinity;
        var lines = new List<string>(); Scene scene = default; Scene originalScene = SceneManager.GetActiveScene(); GameObject stage = null;
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in Edit Mode.");
            var original = UnityEngine.Object.FindObjectsByType<PlayerClimb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(p => p.gameObject.scene.name == "m");
            var sources = UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(c => c.gameObject.scene.name == "m" && c.transform.parent != null && c.transform.parent.name == "obstacle").ToArray();
            Check(sources.Length > 0, "Missing authored obstacle route.");
            // Preview scenes do not register colliders for ordinary Physics.Raycast in Edit Mode.
            // An unsaved additive test scene at a remote offset exercises the actual physics queries.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(originalScene);
            stage = new GameObject("Isolated m climb route"); stage.SetActive(false);
            SceneManager.MoveGameObjectToScene(stage, scene); Vector3 offset = new Vector3(2000, 2000, 2000);
            var copies = new Dictionary<string, BoxCollider>();
            foreach (var source in sources)
            {
                var go = new GameObject(source.name); go.transform.SetParent(stage.transform, false); go.layer = source.gameObject.layer;
                go.transform.SetPositionAndRotation(source.transform.position + offset, source.transform.rotation); go.transform.localScale = source.transform.lossyScale;
                var c = go.AddComponent<BoxCollider>(); c.center = source.center; c.size = source.size; c.isTrigger = source.isTrigger;
                if (!copies.ContainsKey(source.name)) copies[source.name] = c;
            }
            var actor = UnityEngine.Object.Instantiate(original.gameObject, stage.transform, false);
            foreach (var script in actor.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) script.enabled = false;
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var audio in actor.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var ps in actor.GetComponentsInChildren<ParticleSystem>(true)) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var main = ps.main; main.playOnAwake = false; }
            var player = actor.GetComponent<PlayerClimb>(); var roof = actor.GetComponent<LedgeToRoofClimb>(); var anim = actor.GetComponent<Animator>();
            Invoke(player, "Awake"); Invoke(roof, "Awake");
            Invoke(actor.GetComponent<PlayerScript>(), "Awake");
            anim.enabled = true; anim.fireEvents = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; anim.applyRootMotion = true;
            const string hangState = "Base Layer.New StateMachine.ledgeMovement";
            actor.SetActive(true); stage.SetActive(true); anim.Rebind();
            Check(anim.HasState(0, Animator.StringToHash(hangState)), "Missing hanging state path.");
            anim.Play(hangState, 0, 0); anim.Update(0); Physics.SyncTransforms();
            actor.GetComponent<PlayerScript>().SetControl(false);
            player.isClimbing = true; player.playerState = PlayerState.ClimbingState;
            Check(anim.isHuman, "Missing humanoid avatar.");
            Check(Mathf.Abs(player.upHopUpPos - (-.0434f)) < .0001f, "The scene/prefab still overrides the requested lower Hop Up hand offset.");
            Check(Mathf.Abs(player.upHopDownPos - (-.0404f)) < .0001f, "Hop Down grip is still at the old height.");
            Check(player.climbKey == KeyCode.Space, "Climbing still uses C instead of Space.");
            Check(ExecutionOrder(player) < ExecutionOrder(actor.GetComponent<FarkourControllerScript>()), "Climb input must run before ordinary jump/parkour.");
            player.ConsumeClimbInput(); Check(player.ConsumedClimbInputThisFrame, "Jump input ownership was not recorded.");
            lines.Add("Hop Up hand offset = -0.0434 m: lowered by 0.2414 m from the previous 0.198 m setting.");
            var first = copies["climb1"];
            float startX = Mathf.Clamp(first.transform.InverseTransformPoint(original.transform.position + offset).x, -.35f, .35f);
            int side = Vector3.Dot(original.transform.position + offset - first.bounds.center, first.transform.forward) >= 0 ? 1 : -1;
            foreach (float dx in new[] { -.08f, 0, .08f })
            {
                Check(TryAnchor(first, startX + dx, side, out var wall, out var top), "Cannot obtain climb1 anchor.");
                player.SetStableLedge(wall, top);
                for (int index = 1; index <= 6; index++)
                {
                    string expected = index < 6 ? "climb" + (index + 1) : "roof";
                    foreach (float rootHeight in new[] { 1.15f, 1.45f, 1.8f })
                        foreach (bool staleRoof in new[] { false, true })
                        {
                            player.transform.SetPositionAndRotation(player.StableLedgeTopPoint - player.StableLedgeForward * .4f - Vector3.up * rootHeight,
                                Quaternion.LookRotation(player.StableLedgeForward));
                            roof.foundLedgeToRoofClimb = staleRoof;
                            var action = player.FindClimbAction(1, out var nextWall, out var nextTop);
                            lines.Add("probe x=" + dx + " " + index + " rootHeight=" + rootHeight + " stale=" + staleRoof + " -> " + action + " " + nextTop.collider?.name);
                            Check(action == (index < 6 ? HangingLedgeAction.HopUp : HangingLedgeAction.ClimbToRoof), "Unexpected action from climb" + index + " toward " + expected);
                            if (index < 6) Check(nextTop.collider.name == expected, "Hop skipped/selected wrong ledge: " + nextTop.collider?.name);
                        }
                    var chosen = player.FindClimbAction(1, out var commitWall, out var commitTop);
                    if (chosen == HangingLedgeAction.HopUp)
                    {
                        Check(player.FindClimbAction(0, out _, out _) == HangingLedgeAction.None, "C alone incorrectly climbs an intermediate strip.");
                        player.SetStableLedge(commitWall, commitTop);
                    }
                }
            }
            // Step the real hop coroutine/Animator in the isolated scene; holding W vs pausing cannot change its target.
            Check(TryAnchor(first, startX, side, out var beginWall, out var beginTop), "Missing initial anchor.");
            player.SetStableLedge(beginWall, beginTop); player.transform.rotation = Quaternion.LookRotation(player.StableLedgeForward);
            anim.Play(hangState, 0, 0); anim.Update(0);
            AlignInitialHand(player); Physics.SyncTransforms();
            VerifyShimmy(player, anim, hangState, lines);
            for (int i = 1; i < 6; i++)
            {
                Check(player.TryFindHopTarget(1, out var wall, out var top), "Animated route lost target after hop " + i);
                Set(player, "hopLedgeForwardHit", wall); Set(player, "hopLedgeDownHit", top);
                var routine = (IEnumerator)Method(player, "HopUp").Invoke(player, null);
                var stack = new Stack<IEnumerator>(); stack.Push(routine); int ticks = 0; float recoveryPeak = 0;
                while (stack.Count > 0 && ticks++ < 900)
                {
                    var current = stack.Peek();
                    Vector3 beforeResume = player.transform.position;
                    if (!current.MoveNext()) { Check(Vector3.Distance(beforeResume, player.transform.position) < .001f, "Hop completion still teleports the player."); stack.Pop(); continue; }
                    if (current.Current is IEnumerator child) { stack.Push(child); continue; }
                    Invoke(player, "MatchTargetToLedge");
                    AdvanceAnimator(player, 1f / 120);
                    recoveryPeak = Mathf.Max(recoveryPeak, player.LastGripCorrection.magnitude);
                }
                Check(stack.Count == 0 && !player.isHopping, "Hop did not release its lock after outgoing blend.");
                Check(player.StableLedgeTopPoint == top.point, "Hop committed another target.");
                Check(player.CanStartLedgeAction, "Controls released before/after the wrong Animator state.");
                var hand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                var target = top.point + player.StableLedgeForward * player.forwardHopUpPos + Vector3.up * player.upHopUpPos;
                Check(Vector3.Distance(hand.position, target) < .015f, "Hanging hand drifted off committed target.");
                Check(recoveryPeak < .04f, "Hop recovery still jumps by " + recoveryPeak + " m in one frame.");
                lines.Add("Animated hop " + i + " -> " + top.collider.name + ": no completion snap; peak recovery correction=" + recoveryPeak.ToString("F4") + " m/frame.");
                VerifyShimmy(player, anim, hangState, lines);
            }
            Check(player.FindClimbAction(1, out _, out _) == HangingLedgeAction.ClimbToRoof, "Animated climb6 cannot climb to the roof.");
            VerifyHopFrameRates(player, anim, hangState, copies, startX, side, lines);
            VerifyHopDown(player, anim, hangState, stage.transform, lines);
            Check(roof.RefreshRoofTarget(), "Missing landing target on climb6.");
            var landing = (Vector3)roof.GetType().GetField("roofRootTarget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(roof);
            Set(roof, "isClimbingToRoof", true); player.BeginRoofClimbInputLock();
            var roofRoutine = (IEnumerator)Method(roof, "LedgeToClimb").Invoke(roof, null);
            int roofTicks = 0;
            while (roofRoutine.MoveNext() && roofTicks++ < 900)
            {
                Invoke(roof, "Update");
                AdvanceAnimator(player, 1f / 120);
            }
            Check(roofTicks < 900 && !roof.IsClimbingToRoof && !player.isClimbing && !player.IsRoofClimbInputLocked,
                "Roof animation did not finish or left input locked.");
            Check(actor.GetComponent<CharacterController>().enabled && actor.GetComponent<PlayerScript>().HasPlayerControl,
                "Roof exit did not restore CharacterController and movement.");
            Check(Vector3.Distance(actor.transform.position, landing) < .02f, "Roof did not land at the selected surface.");
            lines.Add("climb6 -> roof: real Animator/coroutine completed, landed safely and restored movement/controller.");
            lines.Add("Feet/toes minimum wall distance: before=" + worstFootBefore.ToString("F4") + "; after=" + worstFootAfter.ToString("F4") + " m.");
            Check(worstFootAfter >= .05f, "Feet remained inside the wall after pose correction: " + worstFootAfter);
            lines.Insert(0, "PASS: Space input ownership, lower Hop Down grip, continuous Hop recovery without completion teleport, foot clearance while shimmying, full ledge route and roof exit. No Play Mode/campaign/saves.");
            Debug.Log(lines[0]);
        }
        catch (Exception error) { lines.Insert(0, "FAILED: " + error); Debug.LogException(error); }
        finally
        {
            if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllLines(Result, lines);
            Directory.CreateDirectory("Tools/Climbing"); File.WriteAllLines("Tools/Climbing/Validation.txt", lines);
        }
    }
    static bool TryAnchor(BoxCollider box, float localX, int side, out RaycastHit wall, out RaycastHit top)
    {
        Vector3 face = box.transform.TransformPoint(box.center + new Vector3(localX, 0, side * box.size.z * .5f));
        var forward = box.transform.forward * -side;
        bool hit = box.Raycast(new Ray(face - forward, forward), out wall, 2f);
        bool down = box.Raycast(new Ray(face + forward * .025f + Vector3.up * 2, Vector3.down), out top, 4f);
        return hit && down;
    }
    static void VerifyShimmy(PlayerClimb player, Animator anim, string hangState, List<string> lines)
    {
        var shimmy = player.GetComponent<ShimmyController>(); Invoke(shimmy, "Awake");
        var initialWall = player.rayLedgeForwardHit; var initialTop = player.rayLedgeDownHit;
        Vector3 root = player.transform.position, anchor = player.StableLedgeTopPoint;
        Vector3 right = Vector3.Cross(Vector3.up, player.StableLedgeForward).normalized;
        string ledgeName = initialTop.collider.name;
        foreach (float height in new[] { 1.15f, 1.45f, 1.8f })
        {
            player.transform.position = anchor - Vector3.up * height - player.StableLedgeForward * .4f;
            shimmy.StepShimmy(0, 1f / 60);
            Check(shimmy.canMoveLeft && shimmy.canMoveRight, ledgeName + ": shimmy detection depends on root height " + height);
        }
        player.transform.position = root;
        for (int i = 0; i < 20; i++)
        {
            shimmy.StepShimmy(-1, .05f); AdvanceAnimator(player, .05f);
        }
        float leftTravel = Vector3.Dot(player.transform.position - root, right);
        Check(Mathf.Abs(leftTravel + shimmy.ledgeMoveSpeed) < .06f, ledgeName + ": left input failed or animation applied extra movement: " + leftTravel);
        Check(Vector3.Distance(player.transform.position - root, player.StableLedgeTopPoint - anchor) < .02f, "Shimmy root and anchor moved apart.");
        for (int i = 0; i < 20; i++)
        {
            shimmy.StepShimmy(1, .05f); AdvanceAnimator(player, .05f);
            if (ledgeName == "climb1" && i == 8) CaptureShimmyPose(player);
        }
        Check(Vector3.Distance(player.transform.position, root) < .035f && Vector3.Distance(player.StableLedgeTopPoint, anchor) < .035f,
            ledgeName + ": right input did not return along the same ledge. root=" + (player.transform.position - root).ToString("F4") +
            " anchor=" + (player.StableLedgeTopPoint - anchor).ToString("F4") + " right=" + shimmy.canMoveRight + " anim delta=" + anim.deltaPosition.ToString("F4"));
        player.SetStableLedge(initialWall, initialTop); player.transform.position = root;
        anim.SetFloat("movementvalue", 0); anim.Play(hangState, 0, 0); anim.Update(0);
        player.isHopping = true; shimmy.StepShimmy(1, .05f);
        Check(player.transform.position == root && !shimmy.rightBtn, "Shimmy moved during a hop."); player.isHopping = false;
        Set(player, "roofClimbInputLocked", true); shimmy.StepShimmy(-1, .05f);
        Check(player.transform.position == root && !shimmy.leftBtn, "Shimmy moved during roof climb."); Set(player, "roofClimbInputLocked", false);
        var box = (BoxCollider)initialTop.collider;
        int side = Vector3.Dot(initialWall.normal, box.transform.forward) >= 0 ? 1 : -1;
        float edgeX = Mathf.Sign(Vector3.Dot(right, box.transform.right)) * (box.size.x * .5f - .03f / box.transform.lossyScale.x);
        Check(TryAnchor(box, edgeX, side, out var edgeWall, out var edgeTop), "Cannot obtain edge anchor.");
        player.SetStableLedge(edgeWall, edgeTop); player.transform.position = root + edgeTop.point - anchor;
        Vector3 edgeRoot = player.transform.position; shimmy.StepShimmy(1, .05f);
        Check(!shimmy.canMoveRight && shimmy.canMoveLeft && player.transform.position == edgeRoot, ledgeName + ": shimmy did not stop at the edge.");
        shimmy.StepShimmy(-1, .05f);
        Check(Vector3.Dot(player.transform.position - edgeRoot, right) < -.01f, "Could not shimmy back from the edge.");
        player.SetStableLedge(initialWall, initialTop); player.transform.position = root;
        anim.SetFloat("movementvalue", 0); anim.Play(hangState, 0, 0); anim.Update(0); Physics.SyncTransforms();
        lines.Add(ledgeName + ": left/right travel, synchronized ledge anchor, root-height independence, edge stop/reverse and action locks passed.");
    }
    static void VerifyHopDown(PlayerClimb player, Animator anim, string hangState, Transform stage, List<string> lines)
    {
        var savedWall = player.rayLedgeForwardHit; var savedTop = player.rayLedgeDownHit;
        Vector3 savedPosition = player.transform.position; Quaternion savedRotation = player.transform.rotation;
        float maxDrop = player.hopDownMaxDrop;
        for (int index = 6; index > 1; index--)
        {
            Check(player.TryFindHopTarget(-1, out var wall, out var top), "No Hop Down target from climb" + index);
            Check(top.collider.name == "climb" + (index - 1), "Hop Down skipped a ledge.");
            Set(player, "hopLedgeForwardHit", wall); Set(player, "hopLedgeDownHit", top);
            var stack = new Stack<IEnumerator>(); stack.Push((IEnumerator)Method(player, "HopDown").Invoke(player, null));
            int ticks = 0;
            while (stack.Count > 0 && ticks++ < 900)
            {
                var current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is IEnumerator child) { stack.Push(child); continue; }
                Invoke(player, "MatchTargetToLedge"); AdvanceAnimator(player, 1f / 120);
            }
            Check(stack.Count == 0 && !player.isHopping && player.CanStartLedgeAction, "Hop Down failed to return to hanging controls.");
            Vector3 expectedHand = top.point + player.StableLedgeForward * player.forwardHopDownPos + Vector3.up * player.upHopDownPos;
            Check(Vector3.Distance(anim.GetBoneTransform(HumanBodyBones.LeftHand).position, expectedHand) < .015f, "Hop Down hand missed its target.");
            lines.Add("Animated Hop Down climb" + index + " -> " + top.collider.name + ": grip and controls passed.");
        }
        var upper = new GameObject("Down range test upper"); upper.transform.SetParent(stage, false); upper.transform.position = new Vector3(5000, 5000, 5000);
        upper.layer = savedTop.collider.gameObject.layer;
        var upperBox = upper.AddComponent<BoxCollider>(); upperBox.size = new Vector3(4, .3f, .6f);
        var lower = new GameObject("Down range test lower"); lower.transform.SetParent(stage, false); lower.transform.position = upper.transform.position - Vector3.up * 2.4f;
        lower.layer = upper.layer; var lowerBox = lower.AddComponent<BoxCollider>(); lowerBox.size = upperBox.size;
        Physics.SyncTransforms();
        Check(TryAnchor(upperBox, 0, 1, out var upperWall, out var upperTop), "Missing range test anchor.");
        player.SetStableLedge(upperWall, upperTop);
        player.hopDownMaxDrop = 2.5f;
        Check(player.TryFindHopTarget(-1, out _, out var dropTop) && dropTop.collider == lowerBox, "2.5 m range failed to find a ledge 2.4 m below.");
        player.hopDownMaxDrop = 2.3f;
        Check(!player.TryFindHopTarget(-1, out _, out _), "Hop Down exceeded the configured Max Drop.");
        Check(!player.TryFindHopTarget(1, out _, out _), "Extending Hop Down accidentally changed Hop Up detection.");
        player.hopDownMaxDrop = maxDrop;
        player.SetStableLedge(savedWall, savedTop); player.transform.SetPositionAndRotation(savedPosition, savedRotation);
        anim.SetFloat("movementvalue", 0); anim.Play(hangState, 0, 0); anim.Update(0); Physics.SyncTransforms();
        lines.Add("Hop Down: found 2.4 m drop with Max Drop=2.5; rejected it at 2.3; Hop Up unchanged.");
    }
    static void VerifyHopFrameRates(PlayerClimb player, Animator anim, string hangState, Dictionary<string, BoxCollider> ledges, float x, int side, List<string> lines)
    {
        var savedWall = player.rayLedgeForwardHit; var savedTop = player.rayLedgeDownHit;
        Vector3 savedPosition = player.transform.position; Quaternion savedRotation = player.transform.rotation;
        foreach (int fps in new[] { 30, 60 })
        {
            Check(TryAnchor(ledges["climb1"], x, side, out var wall, out var top), "Missing frame-rate test anchor.");
            player.SetStableLedge(wall, top); player.transform.rotation = Quaternion.LookRotation(player.StableLedgeForward);
            anim.SetFloat("movementvalue", 0); anim.Play(hangState, 0, 0); anim.Update(0); AlignInitialHand(player);
            Check(player.TryFindHopTarget(1, out var nextWall, out var nextTop), "Missing frame-rate test destination.");
            Set(player, "hopLedgeForwardHit", nextWall); Set(player, "hopLedgeDownHit", nextTop);
            var stack = new Stack<IEnumerator>(); stack.Push((IEnumerator)Method(player, "HopUp").Invoke(player, null));
            int ticks = 0; float peak = 0;
            while (stack.Count > 0 && ticks++ < fps * 8)
            {
                var current = stack.Peek(); Vector3 before = player.transform.position;
                if (!current.MoveNext()) { Check(Vector3.Distance(before, player.transform.position) < .001f, "Hop completion snapped at " + fps + " FPS."); stack.Pop(); continue; }
                if (current.Current is IEnumerator child) { stack.Push(child); continue; }
                Invoke(player, "MatchTargetToLedge"); AdvanceAnimator(player, 1f / fps);
                peak = Mathf.Max(peak, player.LastGripCorrection.magnitude);
            }
            Check(stack.Count == 0 && player.CanStartLedgeAction, "Hop did not finish at " + fps + " FPS.");
            Vector3 handTarget = nextTop.point + Vector3.up * player.upHopUpPos + player.StableLedgeForward * player.forwardHopUpPos;
            Check(Vector3.Distance(player.animator.GetBoneTransform(HumanBodyBones.LeftHand).position, handTarget) < .015f, "Grip drifted at " + fps + " FPS.");
            Check(peak < .05f, "Excessive recovery correction at " + fps + " FPS: " + peak);
            lines.Add("Hop Up at " + fps + " FPS: no completion snap, accurate final grip, peak recovery=" + peak.ToString("F4") + " m/frame.");
        }
        player.SetStableLedge(savedWall, savedTop); player.transform.SetPositionAndRotation(savedPosition, savedRotation);
        anim.SetFloat("movementvalue", 0); anim.Play(hangState, 0, 0); anim.Update(0);
    }
    static void AdvanceAnimator(PlayerClimb player, float deltaTime)
    {
        Vector3 position = player.transform.position; Quaternion rotation = player.transform.rotation;
        player.animator.Update(deltaTime);
        // Editor playback does not consistently invoke disabled gameplay callbacks; run the real root-motion owner if needed.
        if ((player.transform.position - position).sqrMagnitude < .0000001f && Quaternion.Angle(rotation, player.transform.rotation) < .001f)
            Invoke(player.GetComponent<PlayerScript>(), "OnAnimatorMove");
        Invoke(player, "LateUpdate");
        var shimmy = player.GetComponent<ShimmyController>();
        if (shimmy != null && player.UsesManualHangingMovement)
        {
            worstFootBefore = Mathf.Min(worstFootBefore, FootDistance(player));
            shimmy.CorrectHangingFeet();
            worstFootAfter = Mathf.Min(worstFootAfter, FootDistance(player));
        }
    }
    static float FootDistance(PlayerClimb player)
    {
        float distance = float.PositiveInfinity;
        foreach (var bone in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightFoot, HumanBodyBones.RightToes })
        {
            var transform = player.animator.GetBoneTransform(bone);
            if (transform != null) distance = Mathf.Min(distance, Vector3.Dot(transform.position - player.StableLedgeWallPoint, player.StableLedgeWallNormal));
        }
        return distance;
    }
    static void CaptureShimmyPose(PlayerClimb player)
    {
        var go = new GameObject("Temporary pose camera");
        SceneManager.MoveGameObjectToScene(go, player.gameObject.scene);
        var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.scene = player.gameObject.scene;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.25f, .29f, .33f);
        camera.fieldOfView = 35; camera.nearClipPlane = .05f; camera.farClipPlane = 20;
        Vector3 center = player.transform.position + Vector3.up;
        Vector3 normal = player.StableLedgeWallNormal, tangent = Vector3.Cross(Vector3.up, -normal);
        camera.transform.position = center + normal * 1.8f + tangent * 3.8f + Vector3.up * .4f;
        camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position);
        var lightObject = new GameObject("Temporary pose light"); lightObject.transform.SetParent(go.transform, false);
        var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
        lightObject.transform.rotation = camera.transform.rotation;
        var rt = new RenderTexture(800, 800, 24); var tex = new Texture2D(800, 800, TextureFormat.RGB24, false);
        var old = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 800, 800), 0, 0); tex.Apply();
            Directory.CreateDirectory("Tools/Climbing"); File.WriteAllBytes("Tools/Climbing/ShimmyRight.png", tex.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = old; rt.Release();
            UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
        }
    }
    static void AlignInitialHand(PlayerClimb player)
    {
        var hand = player.animator.GetBoneTransform(HumanBodyBones.LeftHand);
        player.transform.position += player.StableLedgeTopPoint + Vector3.up * player.upHopUpPos + player.StableLedgeForward * player.forwardHopUpPos - hand.position;
    }
    static MethodInfo Method(object obj, string name) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
    static int ExecutionOrder(MonoBehaviour script)
    {
        int explicitOrder = MonoImporter.GetExecutionOrder(MonoScript.FromMonoBehaviour(script));
        return explicitOrder != 0 ? explicitOrder : script.GetType().GetCustomAttribute<DefaultExecutionOrder>()?.order ?? 0;
    }
    static void Invoke(object obj, string name) => Method(obj, name).Invoke(obj, null);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
