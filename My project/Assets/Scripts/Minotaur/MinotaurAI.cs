using UnityEngine;
using UnityEngine.AI;

public class MinotaurAI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform player;
    [SerializeField] Animator animator;
    [SerializeField] NavMeshAgent agent;
    [SerializeField] MinotaurHealth health;
    [SerializeField] Transform attackZone; // child trigger area (optional for range/visual)

    [Header("Behavior")]
    [SerializeField] float sightRange = 20f;
    [SerializeField] float attackRange = 2.2f;
    [SerializeField] float attackCooldown = 1.2f; // seconds between attacks
    [SerializeField] float repathInterval = 0.2f;  // how often to set destination

    [Header("LOS (optional)")]
    [SerializeField] bool requireLineOfSight = false;
    [SerializeField] LayerMask losBlockMask = ~0;

    [Header("Field of View")]
    [SerializeField] float fieldOfViewAngle = 110f; // degrees  

    float lastRepathTime;
    float lastAttackTime;

    // Animator hashes
    readonly int SpeedHash = Animator.StringToHash("Speed");
    readonly int AttackTrigHash = Animator.StringToHash("Attack");
    readonly int DeadHash = Animator.StringToHash("Dead");

    // Cached validation flags so we don’t log every frame
    bool hasSpeedFloat;
    bool hasAttackTrigger;
    bool hasDeadBool;

    bool isAttacking = false;

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!health) health = GetComponent<MinotaurHealth>();
        if (!player)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    void Start()
    {
        // Validate animator parameters once
        if (animator)
        {
            hasSpeedFloat = animator.HasParameterOfType("Speed", AnimatorControllerParameterType.Float);
            hasAttackTrigger = animator.HasParameterOfType("Attack", AnimatorControllerParameterType.Trigger);
            hasDeadBool = animator.HasParameterOfType("Dead", AnimatorControllerParameterType.Bool);
            if (!hasSpeedFloat) Debug.LogWarning($"{name}: Animator missing Float parameter 'Speed'.");
            if (!hasAttackTrigger) Debug.LogWarning($"{name}: Animator missing Trigger parameter 'Attack'.");
            if (!hasDeadBool) Debug.LogWarning($"{name}: Animator missing Bool parameter 'Dead'.");
        }
        else
        {
            Debug.LogWarning($"{name}: Animator reference is missing.");
        }
    }

    void Update()
    {
        // Basic null/dead guards
        if (!player || !animator || !agent || health == null) return;
        if (health.IsDead)
        {
            // Ensure agent is safely stopped
            StopAgentSafe();
            return;
        }

        // Drive locomotion parameter (only if it exists)
        if (hasSpeedFloat)
        {
            float speedParam = (agent.enabled ? agent.velocity.magnitude : 0f);
            animator.SetFloat(SpeedHash, speedParam);
        }

        // Perception
        float dist = Vector3.Distance(transform.position, player.position);
        bool inSight = dist <= sightRange && IsInFieldOfView() && (!requireLineOfSight || HasLineOfSight());
        if (!inSight)
        {
            StopAgentSafe();
            return;
        }

        // Chase or attack
        if (dist > attackRange)
            ChaseSafe();
        else
            TryAttackSafe();
    }

    public void OnDamagedByPlayer(Transform playerSource)
    {
        if (playerSource == null) return;
        Vector3 direction = playerSource.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    bool IsInFieldOfView()
    {
        Vector3 toPlayer = player.position - transform.position;
        float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
        return angle <= fieldOfViewAngle / 2f;
    }

    bool HasLineOfSight()
    {
        Vector3 origin = transform.position + Vector3.up * 1.6f;
        Vector3 target = player.position + Vector3.up * 1.4f;
        if (Physics.Linecast(origin, target, out RaycastHit hit, losBlockMask, QueryTriggerInteraction.Ignore))
            return hit.transform == player;
        return true;
    }

    void ChaseSafe()
    {
        if (!AgentUsable()) return;
        if (Time.time - lastRepathTime >= repathInterval)
        {
            agent.isStopped = false;
            agent.stoppingDistance = Mathf.Max(0.1f, attackRange * 0.9f);
            agent.SetDestination(player.position);
            lastRepathTime = Time.time;
        }
    }

    void TryAttackSafe()
    {
        if (AgentUsable())
            agent.isStopped = true;

        // Face the target
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 720f * Time.deltaTime);
        }

        if (Time.time - lastAttackTime >= attackCooldown)
        {
            if (hasAttackTrigger)
            {
                animator.ResetTrigger(AttackTrigHash); // safety
                animator.SetTrigger(AttackTrigHash);
                lastAttackTime = Time.time;
                isAttacking = true;
            }
        }
    }

    void StopAgentSafe()
    {
        if (AgentUsable())
            agent.isStopped = true;
    }

    public void OnAttackAnimationEnd()
    {
        isAttacking = false;
        agent.isStopped = false;
        agent.updatePosition = true;
        agent.updateRotation = true;
    }

    bool AgentUsable()
    {
        // Prevent "Stop can only be called..." and similar
        return agent && agent.enabled && agent.isOnNavMesh;
    }

    // Called by MinotaurHealth on death
    public void OnDeath()
    {
        StopAgentSafe();
        if (agent) agent.enabled = false;
        if (hasDeadBool && animator)
            animator.SetBool(DeadHash, true);
        // Optional: Destroy after a delay
        // Destroy(gameObject, 4f);
    }
}
