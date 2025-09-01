using System.Collections;
using UnityEngine;

public class TrapDoor : MonoBehaviour
{
    [Header("References")]
    public Animator trapDoorAnim;    // Animator for the trap door
    private Collider trapCollider;   // Collider so player can stand on it

    void Awake()
    {
        if (trapDoorAnim == null)
            trapDoorAnim = GetComponent<Animator>();

        trapCollider = GetComponent<Collider>();

        // Start the demo cycle
        StartCoroutine(OpenCloseTrap());
    }

    IEnumerator OpenCloseTrap()
    {
        // ---- OPEN ----
        trapDoorAnim.SetTrigger("open");
        // wait for door to visually open (adjust time = animation length)
        yield return new WaitForSeconds(0.1f);  
        trapCollider.enabled = false; // player falls through

        yield return new WaitForSeconds(2f); // keep open for a while

        // ---- CLOSE ----
        trapDoorAnim.SetTrigger("close");
        yield return new WaitForSeconds(0.5f); 
        trapCollider.enabled = true; // player can stand again

        yield return new WaitForSeconds(2f);

        // repeat
        StartCoroutine(OpenCloseTrap());
    }
}
