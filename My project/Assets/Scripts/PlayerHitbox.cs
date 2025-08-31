using UnityEngine;

public class PlayerHitbox : MonoBehaviour
{
    [SerializeField] PlayerHealth health;
    [SerializeField] LayerMask enemyDamageMask; // layers that can damage the player
    [SerializeField] int debugFallbackDamage = 10; // for quick tests

    void Reset()
    {
        if (!health) health = GetComponentInParent<PlayerHealth>();
    }

    void Awake()
    {
        if (!health) health = GetComponentInParent<PlayerHealth>();
    }

    void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    void OnTriggerStay(Collider other)
    {
        // Keep or remove based on design. Keeping allows continuous damage zones.
        // TryApplyDamage(other);
    }

    void TryApplyDamage(Collider other)
    {
        // Layer filter
        if (enemyDamageMask.value != 0 && (enemyDamageMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        // 1) Interface
        var dealer = other.GetComponentInParent<IDamageDealer>();
        if (dealer != null)
        {
            int amount = Mathf.Max(0, dealer.GetDamageAmount());
            if (amount > 0) health?.TakeDamage(amount);
            return;
        }

        // 2) Simple component with 'damage' field
        var info = other.GetComponentInParent<SimpleDamageDealer>();
        if (info != null)
        {
            if (info.damage > 0) health?.TakeDamage(info.damage);
            return;
        }

        // 3) Fallback for testing
        if (debugFallbackDamage > 0) health?.TakeDamage(debugFallbackDamage);
    }
}

public interface IDamageDealer
{
    int GetDamageAmount();
}

public class SimpleDamageDealer : MonoBehaviour, IDamageDealer
{
    public int damage = 15;
    public int GetDamageAmount() => damage;
}
