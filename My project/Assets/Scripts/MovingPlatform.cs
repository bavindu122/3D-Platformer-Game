using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;

    private Vector3 target;
    private Transform playerOnPlatform; // store player reference

    void Start()
    {
        target = pointB.position;
    }

    void Update()
    {
        Vector3 oldPosition = transform.position;

        // Move platform
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // Move player manually if standing on platform
        if (playerOnPlatform != null)
        {
            Vector3 delta = transform.position - oldPosition;
            playerOnPlatform.position += delta;
        }

        // Switch direction at endpoints
        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            target = (target == pointA.position) ? pointB.position : pointA.position;
        }
    }

    // Detect player using trigger collider
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerOnPlatform = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (playerOnPlatform == other.transform)
                playerOnPlatform = null;
        }
    }
}
