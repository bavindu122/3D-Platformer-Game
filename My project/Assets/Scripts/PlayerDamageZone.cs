using UnityEngine;

public interface IDamageable { void TakeDamage(int amount); }

public class PlayerDamageZone : MonoBehaviour
{
    [SerializeField] int damage = 15;
    [SerializeField] LayerMask targetMask; // set to Enemy/Boss only
    [SerializeField] Collider hitbox; // trigger on the weapon
    [SerializeField] bool useAnimationEvents = true;

    bool active;

    void Reset()
    {
        if (!hitbox) hitbox = GetComponent<Collider>();
        if (hitbox) hitbox.isTrigger = true;
    }

    void Awake()
    {
        if (hitbox) hitbox.isTrigger = true;
        SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Hitbox triggered with: " + other.name); // Debug to confirm detection
        if ((targetMask.value & (1 << other.gameObject.layer)) == 0) return; // must be Enemy/Boss
        if (other.transform.root == transform.root) return; // ignore self

        var damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            Debug.Log("Applied damage to: " + other.name);
        }
        else
        {
            Debug.LogWarning("No IDamageable on: " + other.name);
        }
    }

    // Animation events called from the player root proxy (see below)
    public void EnableHitbox() { SetActive(true); }
    public void DisableHitbox() { SetActive(false); }

    void SetActive(bool v)
    {
        active = v;
        if (hitbox) hitbox.enabled = v; // keeps the trigger off when not attacking
    }
}
