using UnityEngine;

public class ControllFallingMovement : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponent<PlayerScript>().HasPlayerControl = false;
    }
    


    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponent<PlayerScript>().HasPlayerControl = true;
    }
}
