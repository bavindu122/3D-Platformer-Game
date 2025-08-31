using UnityEngine;

public class AttackStateHandler : StateMachineBehaviour
{
    public int attackIndex; // assign per-state in Inspector

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var pfm = animator.GetComponent<PlayerFightMovement>();
        if (pfm != null)
        {
            pfm.SetCurrentAttack(attackIndex); // let your PlayerFightMovement track which attack is active
            Debug.Log($"Entered attack state {attackIndex}");
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var pfm = animator.GetComponent<PlayerFightMovement>();
        if (pfm != null)
        {
            pfm.ResetCurrentAttack();
            Debug.Log("Exited attack state");
        }
    }
}
