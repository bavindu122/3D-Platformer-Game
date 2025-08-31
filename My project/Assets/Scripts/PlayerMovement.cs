using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    /* ── TUNABLE VALUES ──────────────────────────────── */
    [Header("Movement")]
    [SerializeField] float speed = 6f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float jumpHeight = 2f;
    [SerializeField] float rotationSpeed = 10f;

    [Header("Crouch (toggle)")]
    [SerializeField] KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] float crouchSpeedMultiplier = 0.5f;
    [SerializeField] float standHeight = 1.8f;
    [SerializeField] float crouchHeight = 1.2f;
    [SerializeField] float standCenterY = 0.97f;
    [SerializeField] float crouchCenterY = 0.67f;
    [SerializeField] float uncrouchCeilingCheck = 0.02f;

    [Header("Stand-up Check")]
    [SerializeField] LayerMask standUpMask = ~0;
    [SerializeField] float standClearance = 0.02f;

    [Header("Landing Prediction")]
    [SerializeField] float landingAnticipation = 0.2f;

    [Header("Falling")]
    [SerializeField] float fallSpeedThreshold = -6f;
    [SerializeField] float fallMinTime = 0.06f;
    [SerializeField] float fallGraceOnLeave = 0.00f;
    float fallArmedTime = -1f;

    [Header("Time Rewind")]
    [SerializeField] float maxRewindTime = 5f;
    [SerializeField] float rewindSpeed = 1f;

    [Header("Jump Forgiveness")]
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBufferTime = 0.1f;

    [Header("Death (Fall)")]
    [SerializeField] float killY = -50f;
    [SerializeField] float lethalImpactSpeed = -16f;
    [SerializeField] Transform modelRoot; // visual child to tilt
    [SerializeField] float deathTiltTime = 0.35f;
    [SerializeField] Vector3 deathTiltAxis = Vector3.forward;

    [Header("Respawn / Checkpoint")]
    [SerializeField] Vector3 fallbackSpawnPoint; // optional; else uses start pos
    [SerializeField] KeyCode respawnKey = KeyCode.E;

    /* ── INTERNAL STATE ──────────────────────────────── */
    Vector3 currentCheckpoint;
    bool hasCheckpoint = false;
    Quaternion modelDefaultRot;

    float lastGroundedTime;
    float lastJumpPressedTime;

    CharacterController controller;
    Camera cam;
    public Animator[] anims;
    PlayerAnimation anim;

    Vector3 velocity;
    bool isGrounded;
    bool isCrouched;
    bool isRewinding = false;

    float lastAirVerticalSpeed;

    // Rewind circular buffer
    PlayerSnapshot[] snapshotBuffer;
    int snapshotIndex = 0;
    int snapshotCount = 0;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main;
        anim = GetComponent<PlayerAnimation>();

        if (anims == null || anims.Length == 0)
            anims = GetComponentsInChildren<Animator>();

        SetControllerHeight(standHeight, standCenterY);

        float minHeight = controller.radius * 2f + 0.01f;
        standHeight = Mathf.Max(standHeight, minHeight);
        crouchHeight = Mathf.Max(crouchHeight, minHeight * 0.75f);

        int maxFrames = Mathf.Max(8, Mathf.RoundToInt(maxRewindTime / Time.fixedDeltaTime));
        snapshotBuffer = new PlayerSnapshot[maxFrames];
        for (int i = 0; i < maxFrames; i++)
            snapshotBuffer[i] = new PlayerSnapshot();

        modelDefaultRot = modelRoot ? modelRoot.localRotation : Quaternion.identity;

        currentCheckpoint = (fallbackSpawnPoint != Vector3.zero) ? fallbackSpawnPoint : transform.position;
        hasCheckpoint = true;
    }

    void Update()
    {
        // Dead: allow respawn and skip rest
        if (anim != null && anim.IsDead())
        {
            if (Input.GetKeyDown(respawnKey)) Respawn();
            return;
        }

        HandleRewindInput();
        if (isRewinding) return;

        // Death by Y threshold
        if (transform.position.y < killY && anim != null && !anim.IsDead())
        {
            Die();
            return;
        }

        // Test hotkeys
        if (Input.GetKeyDown(KeyCode.K)) { if (anim != null) anim.Die(true); }
        if (Input.GetKeyDown(KeyCode.T) && anim != null && !anim.IsDead() && !isRewinding) anim.PlayDance();

        // Ground check + coyote
        isGrounded = controller.isGrounded;
        if (isGrounded) lastGroundedTime = Time.time;

        // Crouch toggle
        if (Input.GetKeyDown(crouchKey))
        {
            if (isCrouched) TryExitCrouch();
            else EnterCrouch();
        }

        // Movement input
        Vector3 move = ReadMovementInput();
        float moveMag = move.magnitude;
        float currentSpeed = speed * (isCrouched ? crouchSpeedMultiplier : 1f);

        // Rotate
        RotateToward(move);

        // Jump buffer
        if (Input.GetButtonDown("Jump")) lastJumpPressedTime = Time.time;
        if (!isCrouched &&
            Time.time - lastGroundedTime <= coyoteTime &&
            Time.time - lastJumpPressedTime <= jumpBufferTime)
        {
            DoJump();
            lastJumpPressedTime = -999f;
        }

        // Gravity
        velocity.y += gravity * Time.deltaTime;
        if (isGrounded && velocity.y < 0f) velocity.y = -2f;

        // Freeze movement while dancing
        bool inDance = IsInDance();
        Vector3 finalMove = inDance ? Vector3.zero : (move * currentSpeed);
        finalMove += new Vector3(0f, velocity.y, 0f);
        controller.Move(finalMove * Time.deltaTime);

        // Hard landing death
        if (!isGrounded)
        {
            lastAirVerticalSpeed = velocity.y;
        }
        else
        {
            if (lastAirVerticalSpeed <= lethalImpactSpeed && anim != null && !anim.IsDead())
            {
                Die();
                return;
            }
        }

        // Landing/fall flags
        bool isLandingSoon = PredictLanding();
        bool isFalling = ComputeFallingState();

        // Drive animator only if not in Dance
        if (!inDance)
        {
            foreach (Animator a in anims)
            {
                a.speed = 1f;
                a.SetFloat("Speed", moveMag);
                a.SetBool("Grounded", isGrounded);
                a.SetBool("Landing", isLandingSoon);
                a.SetBool("Crouch", isCrouched);
                a.SetBool("Falling", isFalling);
                if (isCrouched) FreezeCrouchPoseIfIdle(a);
            }
        }
    }

    void FixedUpdate()
    {
        if (isRewinding) RewindStep();
        else
        {
            if (anim != null && anim.IsDead()) return;
            RecordSnapshot();
        }
    }

    /* ── HELPERS ─────────────────────────────────────── */
    Vector3 ReadMovementInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 f = cam.transform.forward; f.y = 0f;
        Vector3 r = cam.transform.right; r.y = 0f;
        f.Normalize(); r.Normalize();
        return f * v + r * h;
    }

    void RotateToward(Vector3 move)
    {
        if (move.sqrMagnitude < 0.01f) return;
        Quaternion target = Quaternion.LookRotation(move);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
    }

    void DoJump()
    {
        velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
        TriggerAll("Jump");
    }

    void TriggerAll(string trig)
    {
        foreach (Animator a in anims) a.SetTrigger(trig);
    }

    bool ComputeFallingState()
    {
        if (isGrounded || velocity.y > 0f)
        {
            fallArmedTime = -1f;
            return false;
        }
        if (fallGraceOnLeave > 0f && fallArmedTime < 0f && velocity.y <= 0f)
        {
            fallArmedTime = Time.time + fallGraceOnLeave;
            return false;
        }
        if (fallArmedTime > 0f && Time.time < fallArmedTime)
            return false;

        if (velocity.y <= fallSpeedThreshold)
        {
            if (fallArmedTime < 0f) fallArmedTime = Time.time;
            if (Time.time - fallArmedTime >= Mathf.Max(0f, fallMinTime))
                return true;
        }
        else
        {
            if (fallArmedTime >= 0f && Time.time - fallArmedTime > fallMinTime)
                fallArmedTime = -1f;
        }
        return false;
    }

    bool PredictLanding()
    {
        if (isGrounded || velocity.y >= 0f) return false;
        float vy = velocity.y;
        float t = -vy / -gravity;
        return t <= landingAnticipation;
    }

    void EnterCrouch()
    {
        isCrouched = true;
        SetControllerHeight(crouchHeight, crouchCenterY);
    }

    void TryExitCrouch()
    {
        if (CanStandUp())
        {
            isCrouched = false;
            SetControllerHeight(standHeight, standCenterY);
            foreach (var a in anims) a.speed = 1f;
        }
        else
        {
            Debug.Log("Cannot uncrouch: blocked.");
        }
    }

    void SetControllerHeight(float h, float centerY)
    {
        controller.height = h;
        Vector3 c = controller.center; c.y = centerY; controller.center = c;
    }

    bool CanStandUp()
    {
        float radius = controller.radius;
        float heightStand = Mathf.Max(standHeight, radius * 2f + 0.01f);
        Vector3 c = transform.position + new Vector3(0f, standCenterY, 0f);
        float halfStand = Mathf.Max(heightStand * 0.5f - radius, 0f);
        Vector3 bottom = c + Vector3.down * halfStand + Vector3.up * standClearance;
        Vector3 top = c + Vector3.up * halfStand - Vector3.up * standClearance;

        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        bool blocked = Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
        controller.enabled = wasEnabled;
        return !blocked;
    }

    void FreezeCrouchPoseIfIdle(Animator a)
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 0.0001f) { a.speed = 1f; return; }
        var st = a.GetCurrentAnimatorStateInfo(0);
        if (st.IsName("Base Layer.Crouch") || st.IsName("Crouch"))
        {
            float t = Mathf.Repeat(st.normalizedTime, 1f);
            a.Play(st.fullPathHash, 0, t);
            a.speed = 0f;
        }
    }

    bool IsInDance()
    {
        if (anims == null || anims.Length == 0) return false;
        var st = anims[0].GetCurrentAnimatorStateInfo(0);
        return st.IsName("Dance") || st.IsName("Base Layer.Dance");
    }

    /* ── REWIND ──────────────────────────────────────── */
    void HandleRewindInput()
    {
        if (anim != null && anim.IsDead()) return;
        if (Input.GetKeyDown(KeyCode.R) && snapshotCount > 0) BeginRewind();
        else if (Input.GetKeyUp(KeyCode.R)) EndRewind();
    }

    void BeginRewind()
    {
        isRewinding = true;
        foreach (var a in anims) a.speed = 0f;
    }

    void EndRewind()
    {
        isRewinding = false;
        if (snapshotCount > 0)
        {
            int lastIndex = (snapshotIndex - 1 + snapshotBuffer.Length) % snapshotBuffer.Length;
            ApplySnapshot(snapshotBuffer[lastIndex], applyVelocity: true, applyAnim: true);
        }
    }

    void RecordSnapshot()
    {
        PlayerSnapshot snap = snapshotBuffer[snapshotIndex];
        snap.position = transform.position;
        snap.rotation = transform.rotation;
        snap.velocity = velocity;
        snap.animators.Clear();
        foreach (Animator a in anims)
        {
            var st = a.GetCurrentAnimatorStateInfo(0);
            snap.animators.Add(new AnimatorFrame
            {
                fullPathHash = st.fullPathHash,
                normalizedTime = Mathf.Repeat(st.normalizedTime, 1f),
                speedParam = SafeGetFloat(a, "Speed"),
                groundedParam = SafeGetBool(a, "Grounded"),
                landingParam = SafeGetBool(a, "Landing")
            });
        }
        snapshotIndex = (snapshotIndex + 1) % snapshotBuffer.Length;
        snapshotCount = Mathf.Min(snapshotCount + 1, snapshotBuffer.Length);
    }

    void RewindStep()
    {
        int framesToStep = Mathf.Clamp(Mathf.RoundToInt(rewindSpeed), 1, 10);
        while (framesToStep-- > 0 && snapshotCount > 0)
        {
            snapshotIndex = (snapshotIndex - 1 + snapshotBuffer.Length) % snapshotBuffer.Length;
            ApplySnapshot(snapshotBuffer[snapshotIndex], applyVelocity: true, applyAnim: true);
            snapshotCount--;
        }
        if (snapshotCount == 0) EndRewind();
    }

    void ApplySnapshot(PlayerSnapshot snap, bool applyVelocity, bool applyAnim)
    {
        controller.enabled = false;
        transform.position = snap.position;
        transform.rotation = snap.rotation;
        controller.enabled = true;

        if (applyVelocity) velocity = snap.velocity;

        if (applyAnim)
        {
            for (int i = 0; i < anims.Length && i < snap.animators.Count; i++)
            {
                var a = anims[i];
                var af = snap.animators[i];
                a.SetFloat("Speed", af.speedParam);
                a.SetBool("Grounded", af.groundedParam);
                a.SetBool("Landing", af.landingParam);
                a.SetBool("Crouch", isCrouched);
                a.SetBool("Falling", false);
                a.Play(af.fullPathHash, 0, af.normalizedTime);
            }
        }
    }

    float SafeGetFloat(Animator a, string name)
    {
        foreach (var p in a.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Float)
                return a.GetFloat(name);
        return 0f;
    }

    bool SafeGetBool(Animator a, string name)
    {
        foreach (var p in a.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Bool)
                return a.GetBool(name);
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (!controller) return;
        float radius = controller.radius;
        float heightStand = standHeight;
        Vector3 c = transform.position + new Vector3(0f, standCenterY, 0f);
        float halfStand = Mathf.Max(heightStand * 0.5f - radius, 0f);
        Vector3 bottom = c + Vector3.down * halfStand + Vector3.up * standClearance;
        Vector3 top = c + Vector3.up * halfStand - Vector3.up * standClearance;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(bottom, radius);
        Gizmos.DrawWireSphere(top, radius);
        Gizmos.DrawLine(bottom + Vector3.right * radius, top + Vector3.right * radius);
        Gizmos.DrawLine(bottom - Vector3.right * radius, top - Vector3.right * radius);
        Gizmos.DrawLine(bottom + Vector3.forward * radius, top + Vector3.forward * radius);
        Gizmos.DrawLine(bottom - Vector3.forward * radius, top - Vector3.forward * radius);
    }

    /* ── DEATH / RESPAWN ─────────────────────────────── */
    public void Die()
    {
        if (anim != null && anim.IsDead()) return;
        if (anim != null) anim.Die(true);

        velocity = Vector3.zero;
        foreach (var a in anims) a.applyRootMotion = false;

        // Snap to ground (raycast, then spherecast fallback)
        bool snapped = false;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit rayHit, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            float bottomToCenter = controller.height * 0.5f - controller.radius;
            Vector3 newPos = rayHit.point + Vector3.up * (bottomToCenter + 0.005f);
            controller.enabled = false;
            transform.position = newPos;
            controller.enabled = true;
            snapped = true;
        }

        if (!snapped)
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.2f, Vector3.down);
            if (Physics.SphereCast(ray, controller.radius * 0.95f, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                float bottomToCenter = controller.height * 0.5f - controller.radius;
                Vector3 newPos = hit.point + Vector3.up * (bottomToCenter + 0.005f);
                controller.enabled = false;
                transform.position = newPos;
                controller.enabled = true;
            }
        }

        controller.enabled = false;

        if (modelRoot != null)
        {
            StopCoroutine(nameof(TiltDownOnDeath));
            StartCoroutine(TiltDownOnDeath());
        }
    }

    System.Collections.IEnumerator TiltDownOnDeath()
    {
        Quaternion start = modelRoot.localRotation;
        Quaternion target = Quaternion.AngleAxis(90f, deathTiltAxis.normalized) * start;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, deathTiltTime);
            modelRoot.localRotation = Quaternion.Slerp(start, target, t);
            yield return null;
        }
        modelRoot.localRotation = target;
    }

    public void ReviveUpright()
    {
        if (modelRoot == null) return;
        modelRoot.localRotation = Quaternion.identity;
    }

    public void SetCheckpoint(Vector3 pos)
    {
        currentCheckpoint = pos;
        hasCheckpoint = true;
    }

    public void Respawn()
    {
        Vector3 spawn = hasCheckpoint ? currentCheckpoint : ((fallbackSpawnPoint != Vector3.zero) ? fallbackSpawnPoint : transform.position);

        if (!controller.enabled) controller.enabled = true;

        controller.enabled = false;
        transform.position = spawn;
        controller.enabled = true;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit rayHit, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            float bottomToCenter = controller.height * 0.5f - controller.radius;
            Vector3 newPos = rayHit.point + Vector3.up * (bottomToCenter + 0.005f);
            controller.enabled = false;
            transform.position = newPos;
            controller.enabled = true;
        }

        velocity = Vector3.zero;
        lastAirVerticalSpeed = 0f;
        isRewinding = false;

        if (modelRoot) modelRoot.localRotation = modelDefaultRot;
        if (anim != null) anim.Revive();

        snapshotIndex = 0;
        snapshotCount = 0;

        if (isCrouched)
        {
            isCrouched = false;
            SetControllerHeight(standHeight, standCenterY);
        }
    }
}
