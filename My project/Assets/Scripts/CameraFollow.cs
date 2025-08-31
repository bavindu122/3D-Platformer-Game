using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public Vector3 offset = new Vector3(0, 5, -10);
    public float rotationSmooth = 5f;

    void LateUpdate()
    {
        // Calculate the desired position rotated around the player
        Vector3 rotatedOffset = player.rotation * offset;
        Vector3 desiredPosition = player.position + rotatedOffset;

        // Smoothly move to desired position
        transform.position = Vector3.Lerp(transform.position, desiredPosition,
                                        rotationSmooth * Time.deltaTime);

        // Always look at the player
        Vector3 lookDirection = player.position - transform.position;
        Quaternion desiredRotation = Quaternion.LookRotation(lookDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation,
                                            rotationSmooth * Time.deltaTime);
    }
}
