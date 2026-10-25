using UnityEngine;

public class ControllFallingMovement : StateMachineBehaviour
{
    // Falling remains driven by PlayerScript/CharacterController. Disabling its
    // update loop here also disables gravity and grounded detection, which can
    // strand the Animator in a fall state after an interrupted jump.
}
