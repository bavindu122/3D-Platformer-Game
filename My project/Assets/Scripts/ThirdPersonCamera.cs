using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] Transform target;          // player root
    [SerializeField] Transform pivot;           // child that tilts (Pitch)
    [SerializeField] Camera cam;                // main camera

    [Header("Orbit")]
    [SerializeField] float mouseXSens = 500f;   // yaw deg/sec per mouse unit
    [SerializeField] float mouseYSens = 120f;   // pitch deg/sec per mouse unit
    [SerializeField] float minPitch = -35f;
    [SerializeField] float maxPitch = 65f;
    [SerializeField] bool invertY = false;

    [Header("Follow")]
    [SerializeField] float followLerp = 12f;    // position smoothing
    [SerializeField] Vector3 targetOffset = new Vector3(0f, 1.6f, 0f); // follow chest/head

    [Header("Boom / Distance")]
    [SerializeField] float defaultDistance = 4.0f;
    [SerializeField] float minDistance = 1.0f;
    [SerializeField] float maxDistance = 5.5f;
    [SerializeField] float zoomSpeed = 3.0f;    // mouse wheel zoom

    [Header("Collision")]
    [SerializeField] LayerMask collisionMask = ~0;
    [SerializeField] float collisionRadius = 0.2f;   // spherecast radius
    [SerializeField] float collisionLerp = 20f;

    [Header("Auto Align")]
    [SerializeField] bool autoAlignWhenMoving = true;
    [SerializeField] float alignSpeed = 180f;         // deg/sec to yaw toward movement
    [SerializeField] float alignThreshold = 0.2f;     // movement magnitude to trigger
    [SerializeField] KeyCode holdAlignKey = KeyCode.LeftAlt; // while held, prevent align (free look)

    [Header("Auto Align Timing")]
    [SerializeField] float alignDelay = 0.35f;   // wait after last mouse move
    [SerializeField] float moveStickTime = 0.20f; // must be moving this long before aligning
    [SerializeField] float mouseDeadZone = 0.0025f; // ignore tiny mouse jitters

    [Header("Responsiveness")]
    [SerializeField] float snapDistance = 1.5f;       // snap if rig gets this far from target
    [SerializeField] float followBoostFactor = 4f;    // multiply followLerp briefly
    [SerializeField] float followBoostTime = 0.25f;   // seconds of boosted follow
    [SerializeField] float hardLockMax = 0.15f;       // safety cap (seconds)
    float followBoostTimer = 0f;
    float hardLockTimer = 0f;

    float yaw;             // around Y (world)
    float pitch;           // around X (local on pivot)
    float desiredDistance;
    float currentDistance;

    float lastMouseMoveTime;
    float moveStartTime = -999f;
    bool wasMoving;

    Transform cachedTransform;

    [SerializeField] bool ignoreTargetColliders = true; // new
    Collider[] targetCols;                               // new
    HashSet<Collider> targetColSet;                      // new

    void Awake()
    {
        cachedTransform = transform;

        if (!cam) cam = Camera.main;
        if (!target) Debug.LogWarning("ThirdPersonCamera: Target not set.");
        if (!pivot) Debug.LogWarning("ThirdPersonCamera: Pivot not set.");

        desiredDistance = Mathf.Clamp(defaultDistance, minDistance, maxDistance);
        currentDistance = desiredDistance;

        // Initialize yaw/pitch from current rig orientation
        Vector3 eul = cachedTransform.eulerAngles;
        yaw = eul.y;
        pitch = pivot ? NormalizePitch(pivot.localEulerAngles.x) : 0f;

        // Optional: capture cursor for consistent mouse deltas
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;
        if (ignoreTargetColliders && target)
        {
            targetCols = target.GetComponentsInChildren<Collider>(true);
            targetColSet = new HashSet<Collider>(targetCols);
        }
    }

    void LateUpdate()
    {
        if (!target || !pivot || !cam) return;
        Vector3 targetPos = target.position + targetOffset;
        float distToTarget = Vector3.Distance(cachedTransform.position, targetPos);

        bool doHardLock = hardLockTimer > 0f;
        if (doHardLock) hardLockTimer -= Time.deltaTime;

        if (doHardLock || distToTarget >= snapDistance)
        {
            // Hard glue on rewind/teleport or while hard-locked (e.g., dodge burst)
            cachedTransform.position = targetPos;
            followBoostTimer = 0f; // ignore boost while hard-locking
        }
        else
        {
            // Optional short boost after burst moves
            float boost = (followBoostTimer > 0f) ? followBoostFactor : 1f;
            if (followBoostTimer > 0f) followBoostTimer -= Time.deltaTime;

            float lambda = followLerp * boost; // frame-rate independent damping
            cachedTransform.position = Vector3.Lerp(
                cachedTransform.position,
                targetPos,
                1f - Mathf.Exp(-lambda * Time.deltaTime)
            );
        }

        // 2) Read input
        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");
        float wheel = Input.mouseScrollDelta.y;

        // Zoom
        if (Mathf.Abs(wheel) > 0.001f)
        {
            desiredDistance = Mathf.Clamp(
                desiredDistance - wheel * zoomSpeed,
                minDistance, maxDistance
            );
        }

        bool freeLook = Input.GetMouseButton(1) || Input.GetKey(holdAlignKey); // RMB or key for free look

        // 3) Orbit (mouse look)
        if (Mathf.Abs(mx) > mouseDeadZone || Mathf.Abs(my) > mouseDeadZone)
        {
            yaw += mx * mouseXSens * Time.deltaTime;
            float yInput = invertY ? my : -my;
            pitch += yInput * mouseYSens * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            lastMouseMoveTime = Time.time; // update last mouse activity
        }

        // 4) Movement sampling and auto-align gating
        Vector3 moveDirSample = GetApproxPlayerMoveDirection();
        bool isMovingNow = moveDirSample.sqrMagnitude > alignThreshold * alignThreshold;

        if (isMovingNow)
        {
            if (!wasMoving) moveStartTime = Time.time; // just started moving
            wasMoving = true;
        }
        else
        {
            wasMoving = false;
        }

        // 5) Auto-align behind movement direction (with timers)
        //if (autoAlignWhenMoving && !freeLook)
        //{
        //    bool noRecentMouse = (Time.time - lastMouseMoveTime) >= alignDelay;
        //    bool movingLongEnough = wasMoving && (Time.time - moveStartTime) >= moveStickTime;

        //    if (noRecentMouse && movingLongEnough)
        //    {
        //        float targetYaw = Mathf.Atan2(moveDirSample.x, moveDirSample.z) * Mathf.Rad2Deg;
        //        yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, alignSpeed * Time.deltaTime);
        //    }
        //}

        // 6) Apply yaw to rig, pitch to pivot
        cachedTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
        pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // 7) Collision (filtered): position camera along pivot’s -Z with spherecast
        float wanted = desiredDistance;
        Vector3 camLocal = new Vector3(0f, 0f, -wanted);
        Vector3 from = pivot.position + pivot.forward * 0.05f; // start slightly in front to avoid self-hit
        Vector3 to = pivot.TransformPoint(camLocal);
        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        dir /= Mathf.Max(dist, 0.0001f);

        // Remove the 'static' modifier from the declaration of hitsBuf, as instance fields cannot be static.  
        const int MaxHits = 16;
        RaycastHit[] hitsBuf = new RaycastHit[MaxHits];
        int hitCount = Physics.SphereCastNonAlloc(from, collisionRadius, dir, hitsBuf, dist, collisionMask, QueryTriggerInteraction.Ignore);

        // Find closest valid hit (ignore target colliders)
        float hitDist = dist;
        for (int i = 0; i < hitCount; i++)
        {
            var h = hitsBuf[i];
            if (h.collider == null) continue;

            if (ignoreTargetColliders && targetColSet != null && targetColSet.Contains(h.collider))
                continue; // skip self

            if (h.distance < hitDist)
                hitDist = h.distance;
        }

        // Pad away from obstacle
        if (hitDist < dist)
            hitDist = Mathf.Max(0.1f, hitDist - 0.05f);

        // Clamp to a small safe minimum so we never get inside the head
        float safeMin = 0.35f; // tune to taste
        float targetDistance = Mathf.Min(wanted, Mathf.Max(hitDist, safeMin));

        // If hard-locked/snap this frame, set boom instantly; else smooth
        if (doHardLock || distToTarget >= snapDistance)
            currentDistance = targetDistance;
        else
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, 1f - Mathf.Exp(-collisionLerp * Time.deltaTime));

        cam.transform.localPosition = new Vector3(0f, 0f, -currentDistance);
        cam.transform.localRotation = Quaternion.identity;

    }

    Vector3 GetApproxPlayerMoveDirection()
    {
        // Prefer CharacterController velocity if available
        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc != null)
        {
            Vector3 v = cc.velocity;
            v.y = 0f;
            if (v.sqrMagnitude > 0.0001f) return v.normalized;
        }

        // Fallback to target.forward (useful if your movement code rotates the player)
        return target.forward;
    }

    float NormalizePitch(float rawX)
    {
        // convert 0..360 to -180..180 then clamp to pitch range
        float x = rawX;
        if (x > 180f) x -= 360f;
        return Mathf.Clamp(x, minPitch, maxPitch);
    }

    // Optional public setters
    public void SetTarget(Transform t) { target = t; }
    
    // Hard snap camera rig to target and optionally match yaw (use on teleports)
    public void SnapToTarget(bool snapYawToTarget = false)
    {
        if (!target) return;
        Vector3 targetPos = target.position + targetOffset;
        transform.position = targetPos;           // no smoothing this frame
        currentDistance = desiredDistance;        // reset boom instantly
        if (snapYawToTarget) yaw = target.eulerAngles.y;
    }

    // Briefly disable smoothing and glue camera to target every frame
    public void HardLock(float duration)
    {
        hardLockTimer = Mathf.Clamp(Mathf.Max(hardLockTimer, duration), 0f, hardLockMax);
    }

    // Briefly boost follow rate (keeps smoothing but sticks tighter)
    public void BoostFollow(float duration, float factor)
    {
        followBoostTime = duration;
        followBoostFactor = factor;
        followBoostTimer = duration;
    }
}

