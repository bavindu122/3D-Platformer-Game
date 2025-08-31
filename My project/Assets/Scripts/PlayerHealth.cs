using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class HealthChangedEvent : UnityEvent<int, int> { }

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] int maxHealth = 100;
    public int MaxHealth => maxHealth;
    public int Current { get; private set; }

    PlayerAnimation anim;
    PlayerMovement movement;

    public HealthChangedEvent onHealthChanged = new HealthChangedEvent();

    void Awake()
    {
        Current = maxHealth;
        anim = GetComponent<PlayerAnimation>();
        movement = GetComponent<PlayerMovement>();
    }

    void Start()
    {
        // Notify UI once after Awake so initial values propagate
        onHealthChanged.Invoke(Current, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        Debug.Log($"[PH] TakeDamage {amount}, current={Current}");
        if (amount <= 0) return;
        if (anim != null && anim.IsDead()) return;

        int old = Current;
        Current = Mathf.Max(0, Current - amount);
        if (Current != old) onHealthChanged.Invoke(Current, maxHealth);

        if (Current == 0)
        {
            if (movement != null) movement.Die();
            else if (anim != null) anim.Die(true);
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        int old = Current;
        Current = Mathf.Min(maxHealth, Current + amount);
        if (Current != old) onHealthChanged.Invoke(Current, maxHealth);
    }

    public void SetMaxHealth(int newMax, bool clampToMax = true)
    {
        maxHealth = Mathf.Max(1, newMax);
        if (clampToMax) Current = Mathf.Min(Current, maxHealth);
        onHealthChanged.Invoke(Current, maxHealth);
    }

    public void FullRestore()
    {
        Current = maxHealth;
        onHealthChanged.Invoke(Current, maxHealth);
    }
}
