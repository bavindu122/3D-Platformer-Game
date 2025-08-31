using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] Animator[] animators;

    bool isDead;
    public bool IsDead() => isDead;

    readonly int SpeedHash = Animator.StringToHash("Speed");
    readonly int GroundedHash = Animator.StringToHash("Grounded");
    readonly int LandingHash = Animator.StringToHash("Landing");
    readonly int CrouchHash = Animator.StringToHash("Crouch");
    readonly int FallingHash = Animator.StringToHash("Falling");
    readonly int DeadHash = Animator.StringToHash("Dead");
    readonly int DieTrigHash = Animator.StringToHash("Die");
    readonly int DanceTrigHash = Animator.StringToHash("Dance");

    void Awake()
    {
        if (animators == null || animators.Length == 0)
            animators = GetComponentsInChildren<Animator>();
    }

    public void SetLocomotion(float speed01, bool grounded, bool landing, bool crouch, bool falling)
    {
        if (isDead) return;
        foreach (var a in animators)
        {
            a.SetFloat(SpeedHash, speed01);
            a.SetBool(GroundedHash, grounded);
            a.SetBool(LandingHash, landing);
            a.SetBool(CrouchHash, crouch);
            a.SetBool(FallingHash, falling);
        }
    }

    public void TriggerJump()
    {
        if (isDead) return;
        foreach (var a in animators)
            a.SetTrigger("Jump");
    }

    public void Die(bool trigger)
    {
        if (isDead) return;
        isDead = true;
        foreach (var a in animators)
        {
            if (trigger) a.SetTrigger(DieTrigHash);
            a.SetBool(DeadHash, true);
        }
    }

    public void Revive()
    {
        if (!isDead) return;
        isDead = false;
        foreach (var a in animators)
        {
            a.ResetTrigger(DieTrigHash);
            a.SetBool(DeadHash, false);
        }
    }

    public void PlayDance()
    {
        if (isDead) return;
        foreach (var a in animators)
        {
            a.ResetTrigger(DieTrigHash);
            a.ResetTrigger(Animator.StringToHash("Jump"));
            a.SetTrigger(DanceTrigHash);
        }
    }
}
