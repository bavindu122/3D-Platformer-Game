using UnityEngine;

public class LandingLock : StateMachineBehaviour
{
    // called when Jump End starts
    override public void OnStateEnter(Animator animator,
                                      AnimatorStateInfo stateInfo,
                                      int layerIndex)
    {
        animator.SetBool("IsLanding", true);
    }

    // called when Jump End finishes
    public override void OnStateExit(Animator animator,
                                 AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool("IsLanding", false);          // already there
        animator.ResetTrigger("Jump");                 // new: prevents stale Jump
    }

}
