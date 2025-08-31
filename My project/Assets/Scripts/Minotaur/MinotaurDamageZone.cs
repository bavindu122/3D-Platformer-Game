using UnityEngine;
using System.Collections.Generic;

public class MinotaurDamageZone : MonoBehaviour
{
    [SerializeField] int damage = 20;
    [SerializeField] LayerMask targetMask;           // ONLY the player's hitbox layer
    [SerializeField] bool hitOnAnimationEvent = true;

    SphereCollider trigger;
    readonly HashSet<PlayerHealth> hitThisSwing = new HashSet<PlayerHealth>();

    void Awake()
    {
        trigger = GetComponent<SphereCollider>();
        if (!trigger)
        {
            Debug.LogError("Add SphereCollider (IsTrigger) to AttackZone.");
            return;
        }
        trigger.isTrigger = true;
    }

    // Call this as an Animation Event at the impact frame
    public void DamageZone_Hit()
    {
        if (!hitOnAnimationEvent) return;
        Debug.Log("[MDZ] DamageZone_Hit fired");
        hitThisSwing.Clear();
        DoOverlapDamageOnce();
    }

    // Optional: call at the start of each attack clip to reset hit cache
    public void DamageZone_ResetSwing()
    {
        hitThisSwing.Clear();
    }

   void DoOverlapDamageOnce()
{
    if (!trigger) { Debug.LogWarning("[MDZ] No trigger"); return; }

        Vector3 c = transform.position;
        float r = trigger.radius * Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));

        Debug.Log($"[MDZ] OverlapSphere center={c} r={r} mask={targetMask.value}");

        Collider[] hits = Physics.OverlapSphere(c, r, targetMask, QueryTriggerInteraction.Collide);
        Debug.Log($"[MDZ] hits count={hits.Length}");

        foreach (var h in hits)
        {
            var health = h.GetComponentInParent<PlayerHealth>();
            Debug.Log($"[MDZ] hit {h.name} -> health={(health != null)}");
            if (health != null && !hitThisSwing.Contains(health))
            {
                hitThisSwing.Add(health);
                Debug.Log($"[MDZ] Applying damage {damage} to {health.name}");
                health.TakeDamage(damage);
            }
        }
    }


    void OnDrawGizmosSelected()
    {
        if (!trigger) return;
        Gizmos.color = Color.red;
        float r = trigger.radius * Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        Gizmos.DrawWireSphere(transform.position, r);
    }
}
