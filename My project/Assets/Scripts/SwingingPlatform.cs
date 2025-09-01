using UnityEngine;

public class SwingingPlatform : MonoBehaviour
{
    public float swingAngle = 30f;   // Maximum swing angle
    public float swingSpeed = 2f;    // Swing speed
    private Quaternion startRotation;
    private float swingOffset;

    void Start()
    {
        startRotation = transform.localRotation;
        // Random offset so multiple platforms don't swing identically
        swingOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    void FixedUpdate() // physics-friendly updates
    {
        // Calculate swing angle using sine wave
        float angle = Mathf.Sin(Time.time * swingSpeed + swingOffset) * swingAngle;

        // Apply ONLY on Z-axis (side-to-side swing)
        transform.localRotation = startRotation * Quaternion.Euler(angle, 0f, 0f);
    }
}
