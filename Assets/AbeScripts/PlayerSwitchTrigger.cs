using UnityEngine;

public class PlayerSwitchTrigger : MonoBehaviour
{

    //When the Player ENTERS this trigger collider we...
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("The Player entered");//replace with whatever function you want
        }
    }

    //When the Player STAYS this trigger collider we...
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("The Player stays");//replace with whatever function you want
        }
    }

    //When the Player EXITS this trigger collider we...
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("The Player exits");//replace with whatever function you want
        }
    }
}