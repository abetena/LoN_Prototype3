using UnityEngine;

[RequireComponent(typeof(CharacterController))] //Ensures player controller component is there
public class PushByPlayerController : MonoBehaviour
{
    public float pushForce = 2f; //How hard are we pushing other objects?

//Function that detects collision. Put whatever logic you want in it.
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rb = hit.rigidbody; //grab the rigidbody of the colliding object
        if (rb == null || rb.isKinematic) return; //if no rigidbody or rigidbody does not move by physics, skip

//This logic applies the push force to the other object. 
//You could also replace this with other functions (activate a game object, make sound, etc)
        Vector3 pushDir = hit.moveDirection;
        pushDir.y = 0f; // keep push horizontal
        rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
    }
}