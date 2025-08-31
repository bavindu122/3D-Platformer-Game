// On the player root (the object with the Animator)
using UnityEngine;

public class PlayerAttackEvents : MonoBehaviour
{
    [SerializeField] PlayerDamageZone zone;

    public void EnableHitbox() { if (zone) zone.EnableHitbox(); }
    public void DisableHitbox() { if (zone) zone.DisableHitbox(); }
}
