using UnityEngine;

public class TrunkTrap : MonoBehaviour
{
    public float swingAngle = 45f;     // Max swing angle
    public float swingSpeed = 2f;      // How fast it swings
    public bool swingOnYAxis = true;   // Toggle between Y or X axis
    public float knockbackForce = 15f; // How strong the player gets knocked

    private Quaternion initialRotation;

    void Start()
    {
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        float angle = Mathf.Sin(Time.time * swingSpeed) * swingAngle;

        if (swingOnYAxis)
        {
            transform.localRotation = initialRotation * Quaternion.Euler(0f, angle, 0f);
        }
        else
        {
            transform.localRotation = initialRotation * Quaternion.Euler(angle, 0f, 0f);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Direction from trunk to player
                Vector3 knockDir = (other.transform.position - transform.position).normalized;
                knockDir.y = 0.5f; // Add upward knock
                rb.AddForce(knockDir * knockbackForce, ForceMode.Impulse);
            }
        }
    }
}
