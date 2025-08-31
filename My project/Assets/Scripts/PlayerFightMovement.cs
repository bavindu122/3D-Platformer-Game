using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class PlayerFightMovement : MonoBehaviour
{
    /* ── TUNABLE VALUES ──────────────────────────────── */
    [Header("Movement")]
    [SerializeField] float speed = 6f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float jumpHeight = 2f;
    [SerializeField] float rotationSpeed = 10f;

    [Header("Dodge / Evade")]
    [SerializeField] KeyCode dodgeKey = KeyCode.LeftControl;
    [SerializeField] float dodgeSpeed = 10f;            // horizontal burst
    [SerializeField] float dodgeDuration = 0.35f;       // seconds
    [SerializeField] float dodgeCooldown = 0.5f;
    [SerializeField] float dodgeIFrames = 0.3f;         // invulnerability window
    [SerializeField] bool dodgeUsesMoveDirection = true;// else forward

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
    [SerializeField] float inputDeadzone = 0.2f;

    [Header("Respawn / Checkpoint")]
    [SerializeField] Vector3 fallbackSpawnPoint; // optional; else uses start pos
    [SerializeField] KeyCode respawnKey = KeyCode.E;

    [Header("Combat")]
    [SerializeField] KeyCode lightAttackKey = KeyCode.Mouse0;
    [SerializeField] KeyCode heavyAttackKey = KeyCode.Mouse1;
    [SerializeField] float lightAttackCooldown = 0.25f;
    [SerializeField] float heavyAttackCooldown = 0.6f;

    enum AttackType { None, Light, Heavy }
    AttackType currentAttack = AttackType.None;
    int currentIndex = -1;
    bool  bufferHeavy;

    // --- Attacks / Combo authoring ---
    [Header("Attacks / Combos")]
    [SerializeField] string[] lightStates = { "Light_1", "Light_2", "Light_3" };
    [SerializeField] string[] heavyStates = { "Heavy_1", "Heavy_2" };
    [SerializeField] float chainBufferWindow = 0.25f;   // seconds before end to accept next input
    [SerializeField] float chainCutoffNormalized = 0.5f; // after this, start listening for next
    [SerializeField] float moveLockUntilNormalized = 0.35f; // lock movement early in swing
    [SerializeField] bool attacksUseRootMotion = false; // if clips move character

    float lastAttackInputTime = -999f;

    /* ── INTERNAL STATE ──────────────────────────────── */
    Vector3 currentCheckpoint;
    bool hasCheckpoint = false;
    Quaternion modelDefaultRot;
    float lastGroundedTime;
    float lastJumpPressedTime;
    CharacterController controller;
    Camera cam;
    private Animator mainAnim;
    public Animator[] anims;
    PlayerAnimation anim; // drives Dead/Dance if you reuse it
    Vector3 velocity;
    bool isGrounded;
    bool isRewinding = false;
    float lastAirVerticalSpeed;

    // Dodge state
    bool isDodging = false;
    float dodgeEndTime = 0f;
    float dodgeNextTime = 0f;
    Vector3 dodgeDir = Vector3.zero;
    bool dodgeIFrameActive = false;
    float dodgeIFrameEnd = 0f;

    // Attack state (basic lockouts)
    float nextLightTime = 0f;
    float nextHeavyTime = 0f;
    private bool bufferLight = false; // did user press attack key?
    private float lastPressedTime = -10f; // when?

    // Inspector-tunable chain window
    [SerializeField] private float comboWindowStart = 0.7f;
    [SerializeField] private float comboWindowEnd = 0.95f;

    // Rewind circular buffer
    PlayerSnapshot[] snapshotBuffer;
    int snapshotIndex = 0;
    int snapshotCount = 0;

    ThirdPersonCamera tpc;

    // Animator hashes
    readonly int SpeedHash = Animator.StringToHash("Speed");
    readonly int GroundedHash = Animator.StringToHash("Grounded");
    readonly int LandingHash = Animator.StringToHash("Landing");
    readonly int FallingHash = Animator.StringToHash("Falling");
    readonly int DodgeTrigHash = Animator.StringToHash("Dodge");
    readonly int LightTrigHash = Animator.StringToHash("LightAttack");
    readonly int HeavyTrigHash = Animator.StringToHash("HeavyAttack");
    readonly int RollTrigHash = Animator.StringToHash("Roll");

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main;
        anim = GetComponent<PlayerAnimation>();
        mainAnim = GetComponent<Animator>();
        if (anims == null || anims.Length == 0)
            anims = GetComponentsInChildren<Animator>();

        // Replace deprecated FindObjectOfType with FindFirstObjectByType
        tpc = Object.FindFirstObjectByType<ThirdPersonCamera>();

        int maxFrames = Mathf.Max(8, Mathf.RoundToInt(maxRewindTime / Time.fixedDeltaTime));
        snapshotBuffer = new PlayerSnapshot[maxFrames];
        for (int i = 0; i < maxFrames; i++)
            snapshotBuffer[i] = new PlayerSnapshot();

        modelDefaultRot = modelRoot ? modelRoot.localRotation : Quaternion.identity;
        currentCheckpoint = (fallbackSpawnPoint != Vector3.zero) ? fallbackSpawnPoint : transform.position;
        hasCheckpoint = true;
    } // [1]

    void Update()
    {
        if (anim != null && anim.IsDead())
        {
            if (Input.GetKeyDown(respawnKey)) Respawn();
            return;
        }

        HandleRewindInput();
        if (isRewinding) return;

        if (transform.position.y < killY && anim != null && !anim.IsDead())
        {
            Die();
            return;
        }

        if (Input.GetKeyDown(KeyCode.K)) { if (anim != null) anim.Die(true); }
        if (Input.GetKeyDown(KeyCode.T) && anim != null && !anim.IsDead() && !isRewinding) anim.PlayDance();

        isGrounded = controller.isGrounded;
        if (isGrounded) lastGroundedTime = Time.time;

        Vector3 move = ReadMovementInput();
        float moveMag = move.magnitude;
        float currentSpeed = speed;

        RotateToward(move);

        if (Input.GetButtonDown("Jump")) lastJumpPressedTime = Time.time;
        if (Time.time - lastGroundedTime <= coyoteTime &&
            Time.time - lastJumpPressedTime <= jumpBufferTime &&
            !isDodging)
        {
            DoJump();
            lastJumpPressedTime = -999f;
        }

        HandleDodge(move);
        if (Input.GetKeyDown(lightAttackKey))
        {
            bufferLight = true;
            lastPressedTime = Time.time;
        }
        HandleAttacks();

        if (!isDodging)
        {
            velocity.y += gravity * Time.deltaTime;
            if (isGrounded && velocity.y < 0f) velocity.y = -2f;
        }

        Vector3 finalMove;
        if (isDodging)
        {
            Vector3 horiz = dodgeDir * dodgeSpeed;
            finalMove = new Vector3(horiz.x, velocity.y, horiz.z);
        }
        else
        {
            finalMove = move * currentSpeed + new Vector3(0f, velocity.y, 0f);
        }

        controller.Move(finalMove * Time.deltaTime);

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

        bool isLandingSoon = PredictLanding();
        bool isFalling = ComputeFallingState();

        foreach (Animator a in anims)
        {
            a.speed = 1f;
            a.SetFloat(SpeedHash, moveMag);
            a.SetBool(GroundedHash, isGrounded);
            a.SetBool(LandingHash, isLandingSoon);
            a.SetBool(FallingHash, isFalling);
        }

        if (dodgeIFrameActive && Time.time >= dodgeIFrameEnd)
            dodgeIFrameActive = false;
    } // [1]

    void FixedUpdate()
    {
        if (isRewinding) RewindStep();
        else
        {
            if (anim != null && anim.IsDead()) return;
            RecordSnapshot();
        }
    } // [1]

    /* ── HELPERS ─────────────────────────────────────── */
    Vector3 ReadMovementInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 f = cam.transform.forward; f.y = 0f;
        Vector3 r = cam.transform.right; r.y = 0f;
        f.Normalize(); r.Normalize();
        return (f * v + r * h);
    } // [1]

    void RotateToward(Vector3 move)
    {
        if (move.sqrMagnitude < 0.01f) return;
        Quaternion target = Quaternion.LookRotation(move);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
    } // [1]

    void DoJump()
    {
        velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
        TriggerAll("Jump");
    } // [1]

    void TriggerAll(string trig)
    {
        foreach (Animator a in anims) a.SetTrigger(trig);
    } // [1]

    Vector3 ReadRawMovementDir()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(h) < inputDeadzone) h = 0f;
        if (Mathf.Abs(v) < inputDeadzone) v = 0f;
        Vector3 f = cam.transform.forward; f.y = 0f;
        Vector3 r = cam.transform.right; r.y = 0f;
        f.Normalize(); r.Normalize();
        Vector3 dir = f * v + r * h;
        return dir;
    } // [1]

    // DODGE
    void HandleDodge(Vector3 move)
    {
        if (isDodging && Time.time >= dodgeEndTime)
        {
            isDodging = false;
            return;
        }

        if (!isDodging && isGrounded && Time.time >= dodgeNextTime && Input.GetKeyDown(dodgeKey))
        {
            tpc?.HardLock(Mathf.Min(dodgeDuration * 0.5f, 0.12f));
            tpc?.BoostFollow(0.25f, 6f);

            Vector3 rawDir = ReadRawMovementDir();
            bool hasInput = rawDir.sqrMagnitude > 0.0004f;
            bool backPressed = false;

            Vector3 camF = cam.transform.forward; camF.y = 0f; camF.Normalize();
            float forwardDot = hasInput ? Vector3.Dot(rawDir.normalized, camF) : 0f;

            if (hasInput && forwardDot < -0.5f) backPressed = true;

            if (!hasInput || backPressed)
            {
                dodgeDir = (-transform.forward);
                foreach (Animator a in anims) a.SetTrigger(DodgeTrigHash);
            }
            else
            {
                dodgeDir = rawDir.normalized;
                foreach (Animator a in anims) a.SetTrigger(RollTrigHash);
            }

            dodgeDir = new Vector3(dodgeDir.x, 0f, dodgeDir.z).normalized;

            isDodging = true;
            dodgeEndTime = Time.time + dodgeDuration;
            dodgeNextTime = Time.time + dodgeCooldown;

            velocity.y = 0f;
            controller.Move(dodgeDir * dodgeSpeed * Time.deltaTime);
            tpc?.HardLock(0.06f);

            dodgeIFrameActive = true;
            dodgeIFrameEnd = Time.time + dodgeIFrames;
        }
    } // [1]

    public bool IsInvulnerable()
    {
        return dodgeIFrameActive || (anim != null && anim.IsDead());
    } // [1]

    // ATTACKS  (Rewritten: buffered chains + light->heavy cancel + safe reset)
    void HandleAttacks()
    {
        if (anims == null || anims.Length == 0 || !mainAnim)
        {
            Debug.LogWarning("HandleAttacks skipped: No anims or mainAnim");
            return;
        }

        // Buffer inputs (prefer heavy if both)
        if (Input.GetKeyDown(lightAttackKey))
        {
            bufferLight = true;
            lastAttackInputTime = Time.time;
            Debug.Log("Light buffered");
        }
        if (Input.GetKeyDown(heavyAttackKey))
        {
            bufferHeavy = true;
            lastAttackInputTime = Time.time;
            Debug.Log("Heavy buffered");
        }

        // Get state info
        var st = mainAnim.GetCurrentAnimatorStateInfo(0);
        float nt = st.normalizedTime % 1.0f; // Modulo to clamp if looping/blending >1
       
        // Detect if in attack
        bool inAnyAttack = false;
        int detectedIndex = -1;
        AttackType detectedType = AttackType.None;
        for (int i = 0; i < lightStates.Length; i++)
        {
            if (st.IsName(lightStates[i]))
            {
                inAnyAttack = true;
                detectedType = AttackType.Light;
                detectedIndex = i;
                Debug.Log($"In Light {i}: {lightStates[i]}");
                break;
            }
        }
        if (!inAnyAttack)
        {
            for (int i = 0; i < heavyStates.Length; i++)
            {
                if (st.IsName(heavyStates[i]))
                {
                    inAnyAttack = true;
                    detectedType = AttackType.Heavy;
                    detectedIndex = i;
                    Debug.Log($"In Heavy {i}: {heavyStates[i]}");
                    break;
                }
            }
        }

        // Reset if left attack
        if (!inAnyAttack && currentAttack != AttackType.None)
        {
            Debug.Log("Attack ended, reset");
            currentAttack = AttackType.None;
            currentIndex = -1;
            bufferLight = bufferHeavy = false;
        }

        // Start new combo if idle and buffered
        if (!inAnyAttack && currentAttack == AttackType.None && !isDodging)
        {
            if (bufferHeavy && heavyStates.Length > 0)
            {
                Debug.Log("Start Heavy 0");
                StartAttack(AttackType.Heavy, 0);
                bufferHeavy = bufferLight = false;
                return;
            }
            if (bufferLight && lightStates.Length > 0)
            {
                Debug.Log("Start Light 0");
                StartAttack(AttackType.Light, 0);
                bufferLight = bufferHeavy = false;
                return;
            }
            return;
        }

        // Chain logic if in attack
        if (inAnyAttack)
        {
            Debug.Log($"In {detectedType} index {detectedIndex}, nt: {nt:F2}");

            // Force reset if nt stalled (safety for blending issues)
            if (nt < 0.01f && Time.time - lastAttackInputTime > 0.5f)
            {
                Debug.LogWarning("nt stalled - forcing reset");
                currentAttack = AttackType.None;
                currentIndex = -1;
                return;
            }

            // Mid-cancel to heavy
            const float cancelStart = 0.3f, cancelEnd = 0.7f;
            if (detectedType == AttackType.Light && bufferHeavy && nt >= cancelStart && nt <= cancelEnd && heavyStates.Length > 0)
            {
                Debug.Log("Cancel to Heavy 0");
                StartAttack(AttackType.Heavy, 0);
                bufferLight = bufferHeavy = false;
                return;
            }

            // Chain if in window and buffered
            bool chainPhase = nt >= chainCutoffNormalized;
            bool buffered = (Time.time - lastAttackInputTime) <= chainBufferWindow;
            if (chainPhase && buffered)
            {
                int next = detectedIndex + 1;
                if (detectedType == AttackType.Light && bufferLight && next < lightStates.Length)
                {
                    Debug.Log($"Chain Light to {next}");
                    StartAttack(AttackType.Light, next);
                    bufferLight = bufferHeavy = false;
                }
                else if (detectedType == AttackType.Heavy && bufferHeavy && next < heavyStates.Length)
                {
                    Debug.Log($"Chain Heavy to {next}");
                    StartAttack(AttackType.Heavy, next);
                    bufferLight = bufferHeavy = false;
                }
                else
                {
                    Debug.Log("No chain possible");
                }
            }
            else
            {
                Debug.Log($"Not chaining: phase {chainPhase}, buffered {buffered}");
            }

            // Reset at end
            if (nt >= 0.99f)
            {
                Debug.Log("Attack end, reset");
                currentAttack = AttackType.None;
                currentIndex = -1;
                bufferLight = bufferHeavy = false;
            }
        }
    }


    void HandleAttack()
    {
        AnimatorStateInfo state = mainAnim.GetCurrentAnimatorStateInfo(0);
        // Only progress combo if input buffered AND during chain window
        if (bufferLight && state.normalizedTime >= comboWindowStart && state.normalizedTime <= comboWindowEnd)
        {
            // Advance combostring/animation as desired
            int next = currentIndex + 1;
            if (currentAttack == AttackType.Light && next < lightStates.Length)
            {
                StartAttack(AttackType.Light, next); // starts the next attack
            }
            bufferLight = false; // consume input so it can't repeat
        }
    }

    public void SetCurrentAttack(int index)
    {
        currentAttack = AttackType.Light; // or .Heavy, set by state
        currentIndex = index;
    }

    public void ResetCurrentAttack()
    {
        currentAttack = AttackType.None;
        currentIndex = -1;
    }
        



    bool IsInAttackState(AnimatorStateInfo st)
    {
        foreach (var n in lightStates) if (!string.IsNullOrEmpty(n) && st.IsName(n)) return true;
        foreach (var n in heavyStates) if (!string.IsNullOrEmpty(n) && st.IsName(n)) return true;
        return false;
    } // [1]

    void StartAttack(AttackType type, int index)
    {
        currentAttack = type;
        currentIndex = index;
        string stateName = (type == AttackType.Light) ? lightStates[index] : heavyStates[index];

        foreach (var a in anims)
        {
            a.CrossFadeInFixedTime(stateName, 0.05f, 0, 0f);
            a.applyRootMotion = attacksUseRootMotion;
        }
    } // [1]

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
    } // [1]

    bool PredictLanding()
    {
        if (isGrounded || velocity.y >= 0f) return false;
        float vy = velocity.y;
        float t = -vy / -gravity;
        return t <= landingAnticipation;
    } // [1]

    /* ── REWIND (unchanged) ─────────────────────────── */
    void HandleRewindInput()
    {
        if (anim != null && anim.IsDead()) return;
        if (Input.GetKeyDown(KeyCode.R) && snapshotCount > 0) BeginRewind();
        else if (Input.GetKeyUp(KeyCode.R)) EndRewind();
    } // [1]

    void BeginRewind()
    {
        isRewinding = true;
        foreach (var a in anims) a.speed = 0f;
    } // [1]

    void EndRewind()
    {
        isRewinding = false;
        if (snapshotCount > 0)
        {
            int lastIndex = (snapshotIndex - 1 + snapshotBuffer.Length) % snapshotBuffer.Length;
            ApplySnapshot(snapshotBuffer[lastIndex], applyVelocity: true, applyAnim: true);
        }
    } // [1]

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
    } // [1]

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
    } // [1]

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
                a.SetBool(FallingHash, false);
                a.Play(af.fullPathHash, 0, af.normalizedTime);
            }
        }
    } // [1]

    float SafeGetFloat(Animator a, string name)
    {
        foreach (var p in a.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Float)
                return a.GetFloat(name);
        return 0f;
    } // [1]

    bool SafeGetBool(Animator a, string name)
    {
        foreach (var p in a.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Bool)
                return a.GetBool(name);
        return false;
    } // [1]

    void OnDrawGizmosSelected()
    {
        if (!controller) return;
    } // [1]

    /* ── DEATH / RESPAWN (unchanged) ─────────────────── */
    public void Die()
    {
        if (anim != null && anim.IsDead()) return;
        if (anim != null) anim.Die(true);
        velocity = Vector3.zero;
        foreach (var a in anims) a.applyRootMotion = false;

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
    } // [1]

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
    } // [1]

    public void ReviveUpright()
    {
        if (modelRoot == null) return;
        modelRoot.localRotation = Quaternion.identity;
    } // [1]

    public void SetCheckpoint(Vector3 pos)
    {
        currentCheckpoint = pos;
        hasCheckpoint = true;
    } // [1]

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
    } // [1]
}
