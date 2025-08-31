using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Find PlayerMovement in this object or its parents
        var pm = other.GetComponentInParent<PlayerMovement>();
        if (pm != null)
        {
            pm.SetCheckpoint(transform.position);
            // Optional feedback
            Debug.Log("Checkpoint set to: " + transform.position);
        }
    }
}
