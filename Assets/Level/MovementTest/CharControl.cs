using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador plataformero estilo Crash, con movimiento relativo a la cámara.
/// - W = adelante (hacia donde mira la cámara)
/// - S = atrás
/// - A = izquierda (strafe)
/// - D = derecha (strafe)
/// - Shift = correr
/// - Space = saltar (variable, mantener para más alto)
/// El personaje rota hacia donde se mueve.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CharControl : MonoBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 6f;
    public float runSpeed  = 10f;
    [Range(0f, 1f)] public float airControl = 0.7f;
    [Tooltip("Qué tan rápido el personaje gira hacia la dirección de movimiento.")]
    public float rotationSpeed = 18f;

    [Header("Salto (estilo Crash)")]
    public float jumpVelocity = 9f;
    public float fallMultiplier = 2.6f;
    public float lowJumpMultiplier = 2.2f;
    [Range(0.1f, 1f)] public float apexGravityMultiplier = 0.4f;
    public float apexThreshold = 2f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.15f;

    [Header("Detección de suelo")]
    public float groundCheckRadius = 0.25f;
    public float groundCheckDistance = 0.15f;
    public LayerMask groundLayer = ~0;

    [Header("Referencias")]
    [Tooltip("Cámara para orientar el movimiento. Si está vacía usa Camera.main.")]
    public Transform cameraTransform;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Vector2 moveInput;
    private bool sprinting;
    private bool jumpHeld;

    private bool isGrounded;
    private float coyoteCounter;
    private float jumpBufferCounter;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        capsule = GetComponent<CapsuleCollider>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f, v = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v += 1f;
        moveInput = new Vector2(h, v);
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

        sprinting = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        jumpHeld  = kb.spaceKey.isPressed;

        if (kb.spaceKey.wasPressedThisFrame)
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        CheckGround();

        if (isGrounded) coyoteCounter = coyoteTime;
        else            coyoteCounter -= Time.fixedDeltaTime;

        // --- Movimiento RELATIVO A LA CÁMARA ---
        Vector3 wishDir;
        if (cameraTransform != null && moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 camFwd = cameraTransform.forward; camFwd.y = 0f; camFwd.Normalize();
            Vector3 camRgt = cameraTransform.right;   camRgt.y = 0f; camRgt.Normalize();
            wishDir = camFwd * moveInput.y + camRgt * moveInput.x;
        }
        else
        {
            wishDir = new Vector3(moveInput.x, 0f, moveInput.y);
        }

        float speed = sprinting ? runSpeed : walkSpeed;
        Vector3 targetVel  = wishDir * speed;
        Vector3 currentVel = rb.linearVelocity;

        float control = isGrounded ? 1f : airControl;
        rb.linearVelocity = new Vector3(
            Mathf.Lerp(currentVel.x, targetVel.x, control),
            currentVel.y,
            Mathf.Lerp(currentVel.z, targetVel.z, control)
        );

        // Rotar el personaje hacia donde se mueve.
        if (wishDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(wishDir, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
        }

        // --- Salto ---
        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            Vector3 v = rb.linearVelocity;
            v.y = jumpVelocity;
            rb.linearVelocity = v;
            coyoteCounter = 0f;
            jumpBufferCounter = 0f;
        }

        // --- Gravedad estilo Crash ---
        float vy = rb.linearVelocity.y;
        float g  = Physics.gravity.magnitude;
        if (vy < 0f)
            rb.AddForce(Vector3.down * (fallMultiplier - 1f) * g, ForceMode.Acceleration);
        else if (vy > 0f && !jumpHeld)
            rb.AddForce(Vector3.down * (lowJumpMultiplier - 1f) * g, ForceMode.Acceleration);
        else if (Mathf.Abs(vy) < apexThreshold && !isGrounded)
            rb.AddForce(Vector3.up * (1f - apexGravityMultiplier) * g, ForceMode.Acceleration);
    }

    void CheckGround()
    {
        Vector3 origin = transform.position;
        float radius = groundCheckRadius;
        if (capsule != null)
        {
            origin = transform.TransformPoint(capsule.center) + Vector3.down * (capsule.height * 0.5f - capsule.radius);
            radius = capsule.radius * 0.95f;
        }
        isGrounded = Physics.SphereCast(
            origin + Vector3.up * 0.05f,
            radius,
            Vector3.down,
            out _,
            groundCheckDistance + 0.05f,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        float radius = groundCheckRadius;
        var cap = GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            origin = transform.TransformPoint(cap.center) + Vector3.down * (cap.height * 0.5f - cap.radius);
            radius = cap.radius * 0.95f;
        }
        Gizmos.color = Application.isPlaying && isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(origin + Vector3.down * groundCheckDistance, radius);
    }
}
