using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(WorldInteraction))]
public sealed class EnemyEncounterBehaviour : MonoBehaviour
{
    public enum TravelMode { Ground, Flying, GroundAndFlying }
    public enum Activity { Idle, Patrol, Chase, Return, Attack, TakeOff, Landing }
    [Header("Map Model")]
    public Animator mapAnimator;
    public TravelMode travelMode;
    [Header("Ground + Air Patrol")]
    public bool startInAir;
    [Min(1)] public float groundPatrolSeconds = 14;
    [Min(1)] public float airPatrolSeconds = 18;
    [Min(.2f)] public float flightTransitionSeconds = 1.5f;
    public string takeOffState = "TakeOffToGlide";
    public string landingState = "GlideToLanding";
    public bool IsAirborne => airborne;
    bool airborne;
    float modeUntil, transitionProgress;
    Vector3 transitionStart, transitionEnd;
    [Header("Territory (world units)")]
    [Min(1)] public float patrolRadius = 12;
    [Min(1)] public float leashRadius = 20;
    [Tooltip("Optional world-space patrol points. Empty = random positions within Patrol Radius.")]
    public Transform[] waypoints;
    [Min(0)] public float idleSeconds = 2;
    [Header("Movement")]
    [Min(.1f)] public float walkSpeed = 2;
    [Min(.1f)] public float runSpeed = 5;
    [Min(.1f)] public float bodyRadius = .5f;
    [Min(.2f)] public float bodyHeight = 2;
    [Min(0)] public float flightHeight = 4;
    public LayerMask obstacleLayers = Physics.DefaultRaycastLayers;
    [Header("Attack -> Combat Encounter")]
    [Min(.2f)] public float attackRange = 2.5f;
    [Min(.1f), Tooltip("Time to play the attack before entering combat. Range and line of sight are rechecked.")]
    public float attackWindup = .7f;
    [Min(.1f)] public float attackCooldown = 2;
    [Header("Animator State Names")]
    public string movementLayer = "Move Layer";
    public string idleState = "Idle";
    public string walkState = "Walk";
    public string runState = "Run";
    public string attackState = "Attack";
    public string flyIdleState = "FlyStationary";
    public string flyMoveState = "FlyNormal";
    public string flyAttackState = "FlyStationarySpitFireBall";
    public Activity CurrentActivity { get; private set; }
    WorldInteraction point;
    NavMeshAgent agent;
    Vector3 home, destination, lastPosition;
    float idleUntil, nextPath, stuckTime, attackAt, nextAttack;
    int waypointIndex, animationHash;
    Vector3[] patrolPoints;
    bool initialized, warnedNavMesh;

    void Start()
    {
        point = GetComponent<WorldInteraction>();
        if (point.kind != WorldInteractionKind.Encounter) { enabled = false; return; }
        home = transform.position;
        patrolPoints = waypoints == null ? new Vector3[0] : System.Array.ConvertAll(waypoints, w => w == null ? home : w.position);
        if (mapAnimator == null) mapAnimator = GetComponentInChildren<Animator>(true);
        if (mapAnimator == null)
        {
            var prefab = point.enemyPrefabs.Find(p => p != null);
            if (prefab != null)
            {
                // Prevent combat scripts from running Awake/OnEnable on the map.
                var holder = new GameObject("Map Enemy Model"); holder.SetActive(false); holder.transform.SetParent(transform, false);
                var model = Instantiate(prefab, holder.transform);
                model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
                mapAnimator = model.animator != null ? model.animator : model.GetComponentInChildren<Animator>(true);
                PrepareModel(model.gameObject);
                model.gameObject.SetActive(true); holder.SetActive(true);
            }
        }
        if (mapAnimator != null)
        {
            PrepareModel(mapAnimator.gameObject);
            mapAnimator.applyRootMotion = false; mapAnimator.fireEvents = false;
            int layer = mapAnimator.GetLayerIndex(movementLayer);
            if (layer >= 0) mapAnimator.SetLayerWeight(layer, 1);
        }
        airborne = travelMode == TravelMode.Flying;
        agent = GetComponent<NavMeshAgent>();
        if (travelMode != TravelMode.Flying)
        {
            if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
            agent.enabled = true;
            agent.radius = bodyRadius; agent.height = bodyHeight; agent.angularSpeed = 360;
            agent.acceleration = 12; agent.stoppingDistance = .3f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            if (NavMesh.SamplePosition(home, out var hit, 2, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask })) agent.Warp(hit.position);
            if (!agent.isOnNavMesh) Debug.LogWarning("Bake Patrol NavMesh before running this ground enemy.", this);
        }
        else
        {
            if (agent != null) agent.enabled = false;
            // Place at flight height only when the vertical route is clear.
            Vector3 rise = Vector3.up * flightHeight;
            if (!Blocked(transform.position, rise, rise.magnitude)) transform.position += rise;
        }
        initialized = true; lastPosition = transform.position;
        Idle();
        modeUntil = Time.time + (travelMode == TravelMode.GroundAndFlying && startInAir ? 0 : groundPatrolSeconds);
    }
    public static void PrepareModel(GameObject model)
    {
        foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(script is WorldInteraction) && !(script is EnemyEncounterBehaviour)) script.enabled = false;
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var nav in model.GetComponentsInChildren<NavMeshAgent>(true)) nav.enabled = false;
        foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
    }
    static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    public bool CanInteract(Transform player, float range)
    {
        if (player == null || CurrentActivity == Activity.TakeOff || CurrentActivity == Activity.Landing) return false;
        float distance = airborne ? HorizontalDistance(transform.position, player.position) : Vector3.Distance(transform.position, player.position);
        return distance <= range && Mathf.Abs(transform.position.y - player.position.y) <= Mathf.Max(bodyHeight, flightHeight + 2) && Visible(player);
    }
    bool Visible(Transform target)
    {
        Vector3 start = transform.position + Vector3.up * (!airborne ? bodyHeight * .5f : 0);
        Vector3 delta = target.position + Vector3.up - start;
        foreach (var hit in Physics.RaycastAll(start, delta.normalized, delta.magnitude, obstacleLayers, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target)) return false;
        return true;
    }
    void Update()
    {
        if (!initialized) return;
        var session = CampaignSession.Instance;
        if (session == null || !session.OnMap || session.MapPlayer == null || CampaignSession.InputBlocked)
        { StopMovement(); return; }
        if (CurrentActivity == Activity.TakeOff || CurrentActivity == Activity.Landing)
        { UpdateFlightTransition(); return; }
        var player = session.MapPlayer.transform;
        if (CurrentActivity == Activity.Attack)
        {
            StopMovement(); Face(player.position);
            if (Time.time >= attackAt)
            {
                nextAttack = Time.time + attackCooldown;
                if (CanInteract(player, Mathf.Min(attackRange, point.range))) session.BeginEncounter(point);
                Idle();
            }
            return;
        }
        float territory = Mathf.Max(patrolRadius, leashRadius);
        bool detected = HorizontalDistance(home, player.position) <= patrolRadius && Visible(player);
        bool chasing = CurrentActivity == Activity.Chase && HorizontalDistance(home, player.position) <= territory;
        if ((detected || chasing) && HorizontalDistance(home, transform.position) <= territory)
        {
            if (CanInteract(player, Mathf.Min(attackRange, point.range)) && Time.time >= nextAttack)
            { CurrentActivity = Activity.Attack; attackAt = Time.time + attackWindup; StopMovement(); Play(attackState, flyAttackState); return; }
            CurrentActivity = Activity.Chase;
            destination = player.position + (airborne ? Vector3.up * flightHeight : Vector3.zero);
            Move(runSpeed); return;
        }
        if (CurrentActivity == Activity.Chase || HorizontalDistance(home, transform.position) > territory)
        { CurrentActivity = Activity.Return; destination = home + (airborne ? Vector3.up * flightHeight : Vector3.zero); }
        if (travelMode == TravelMode.GroundAndFlying && Time.time >= modeUntil &&
            (CurrentActivity == Activity.Idle || CurrentActivity == Activity.Patrol))
        {
            if (TrySwitchTravelMode()) return;
            modeUntil = Time.time + 3; // Retry later; never snap onto invalid ground.
        }
        if (CurrentActivity == Activity.Idle)
        {
            if (Time.time < idleUntil) return;
            if (!ChooseDestination()) { Idle(); return; }
            CurrentActivity = Activity.Patrol;
        }
        if (Vector3.Distance(transform.position, destination) < .6f) { Idle(); return; }
        Move(walkSpeed);
    }
    bool TrySwitchTravelMode()
    {
        if (agent == null) return false;
        Vector3 target;
        if (!airborne)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            target = transform.position + Vector3.up * flightHeight;
        }
        else
        {
            // Require physical ground AND a baked walkable polygon below us.
            target = Vector3.zero;
            float best = float.PositiveInfinity;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * .1f, Vector3.down,
                Mathf.Max(2, flightHeight * 2 + 2), obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= best) continue;
                if (!NavMesh.SamplePosition(hit.point, out var ground, .5f, filter) ||
                    HorizontalDistance(home, ground.position) > patrolRadius ||
                    HorizontalDistance(transform.position, ground.position) > .5f) continue;
                if (TransitionBlocked(transform.position, ground.position)) continue;
                best = hit.distance; target = ground.position;
            }
            if (float.IsInfinity(best)) return false;
        }
        if (TransitionBlocked(transform.position, target)) return false;
        StopMovement(); agent.enabled = false;
        transitionStart = transform.position; transitionEnd = target; transitionProgress = 0;
        CurrentActivity = airborne ? Activity.Landing : Activity.TakeOff;
        string state = airborne ? landingState : takeOffState;
        animationHash = 0; Play(state, state);
        return true;
    }
    bool TransitionBlocked(Vector3 from, Vector3 to)
    {
        float radius = Mathf.Max(.1f, bodyRadius);
        Vector3 bottom = Vector3.up * (radius + .05f);
        Vector3 top = Vector3.up * Mathf.Max(radius + .05f, bodyHeight - radius);
        foreach (var collider in Physics.OverlapCapsule(to + bottom, to + top, radius, obstacleLayers, QueryTriggerInteraction.Ignore))
            if (!collider.transform.IsChildOf(transform)) return true;
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < .00001f) return false;
        foreach (var hit in Physics.CapsuleCastAll(from + bottom, from + top, radius, delta.normalized, delta.magnitude, obstacleLayers, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform)) return true;
        return false;
    }
    void UpdateFlightTransition()
    {
        transitionProgress = Mathf.Clamp01(transitionProgress + Time.deltaTime / Mathf.Max(.2f, flightTransitionSeconds));
        Vector3 next = Vector3.Lerp(transitionStart, transitionEnd, Mathf.SmoothStep(0, 1, transitionProgress));
        if (TransitionBlocked(transform.position, next))
        {
            if (CurrentActivity == Activity.TakeOff && !TransitionBlocked(transform.position, transitionStart))
            {
                transform.position = transitionStart;
                agent.enabled = true;
                if (agent.Warp(transitionStart) && agent.isOnNavMesh)
                { airborne = false; modeUntil = Time.time + 3; Idle(); return; }
                agent.enabled = false;
            }
            // A moving obstacle entered the route. Stay airborne and retry safely.
            airborne = true; modeUntil = Time.time + 3; Idle(); return;
        }
        transform.position = next;
        if (transitionProgress < 1) return;
        bool landing = CurrentActivity == Activity.Landing;
        airborne = !landing;
        if (landing)
        {
            agent.enabled = true;
            if (!agent.Warp(transitionEnd) || !agent.isOnNavMesh)
            { agent.enabled = false; airborne = true; modeUntil = Time.time + 3; Idle(); return; }
            agent.ResetPath(); nextPath = 0;
        }
        lastPosition = transform.position; stuckTime = 0; animationHash = 0;
        modeUntil = Time.time + (airborne ? airPatrolSeconds : groundPatrolSeconds);
        Idle();
    }
    bool ChooseDestination()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * patrolRadius;
            Vector3 target = home + new Vector3(offset.x, 0, offset.y);
            if (patrolPoints.Length > 0)
            {
                target = patrolPoints[waypointIndex++ % patrolPoints.Length];
                if (HorizontalDistance(home, target) > patrolRadius) continue;
            }
            if (airborne) { destination = target + Vector3.up * flightHeight; return true; }
            if (agent != null && agent.isOnNavMesh && NavMesh.SamplePosition(target, out var hit, 2, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }))
            {
                var path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                { destination = hit.position; return true; }
            }
        }
        return false;
    }
    void Move(float speed)
    {
        if (!airborne)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                if (!warnedNavMesh) { warnedNavMesh = true; Debug.LogWarning("Enemy Encounter needs a baked NavMesh. Select this point and Bake Patrol NavMesh.", this); }
                Idle(); return;
            }
            agent.isStopped = false; agent.speed = speed;
            if (Time.time >= nextPath)
            {
                nextPath = Time.time + .3f;
                if (!NavMesh.SamplePosition(destination, out var hit, 2, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }) || !agent.SetDestination(hit.position)) { Idle(); return; }
            }
            if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid) { Idle(); return; }
        }
        else Fly(speed);
        bool moving = Vector3.Distance(transform.position, lastPosition) > .01f;
        stuckTime = moving ? 0 : stuckTime + Time.deltaTime;
        lastPosition = transform.position;
        if (stuckTime > 2) { stuckTime = 0; Idle(); return; }
        Play(moving ? (CurrentActivity == Activity.Chase ? runState : walkState) : idleState, moving ? flyMoveState : flyIdleState);
    }
    bool Blocked(Vector3 origin, Vector3 direction, float distance)
    {
        if (distance <= 0) return false;
        foreach (var collider in Physics.OverlapSphere(origin + direction.normalized * distance, bodyRadius, obstacleLayers, QueryTriggerInteraction.Ignore))
            if (!collider.transform.IsChildOf(transform)) return true;
        foreach (var hit in Physics.SphereCastAll(origin, bodyRadius, direction.normalized, distance, obstacleLayers, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform)) return true;
        return false;
    }
    void Fly(float speed)
    {
        Vector3 desired = (destination - transform.position).normalized;
        float step = Mathf.Min(speed * Time.deltaTime, Vector3.Distance(transform.position, destination));
        Vector3 best = Vector3.zero; float score = float.NegativeInfinity;
        foreach (float angle in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f })
        {
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * desired;
            if (Blocked(transform.position, dir, Mathf.Max(step, bodyRadius * 2))) continue;
            float value = Vector3.Dot(dir, desired);
            if (value > score) { score = value; best = dir; }
        }
        // Stop rather than passing through geometry if all candidate routes are blocked.
        if (best == Vector3.zero) return;
        transform.position += best * step; Face(transform.position + best);
    }
    void Face(Vector3 target)
    {
        Vector3 delta = target - transform.position; delta.y = 0;
        if (delta.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), 360 * Time.deltaTime);
    }
    void Play(string ground, string flying)
    {
        if (mapAnimator == null) return;
        string state = airborne ? flying : ground;
        int layer = mapAnimator.GetLayerIndex(movementLayer);
        if (layer < 0) layer = 0;
        int hash = Animator.StringToHash(mapAnimator.GetLayerName(layer) + "." + state);
        if (!mapAnimator.HasState(layer, hash)) return;
        if (animationHash == hash) return;
        animationHash = hash; mapAnimator.CrossFadeInFixedTime(hash, .15f, layer);
    }
    void Idle() { CurrentActivity = Activity.Idle; idleUntil = Time.time + idleSeconds; StopMovement(); Play(idleState, flyIdleState); }
    void StopMovement() { if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true; }
    void OnDisable() { StopMovement(); }
    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying && initialized ? home : transform.position;
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(center, patrolRadius);
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(center, Mathf.Max(patrolRadius, leashRadius));
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
