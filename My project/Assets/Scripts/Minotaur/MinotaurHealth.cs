using UnityEngine;

public class MinotaurHealth : MonoBehaviour, IDamageable
{
    [SerializeField] int maxHealth = 100;
    public int Current { get; private set; }
    public bool IsDead => Current <= 0;
    MinotaurAI ai;

    void Awake()
    {
        Current = maxHealth;
        ai = GetComponent<MinotaurAI>();
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        if (ai != null)
            ai.OnDamagedByPlayer(GameObject.FindGameObjectWithTag("Player").transform);
        Current = Mathf.Max(0, Current - amount);
        Debug.Log("Minotaur took " + amount + " damage. Health: " + Current);
        if (IsDead)
        {
            if (ai) ai.OnDeath();
        }
    }
}
