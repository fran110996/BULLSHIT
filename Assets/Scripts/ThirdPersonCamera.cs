using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cámara en tercera persona estilo orbital.
/// - La cámara SIGUE al player (target).
/// - Se orbita con el MOUSE: mover izq/der gira en horizontal, arriba/abajo gira en vertical.
/// - El eje vertical tiene CLAMP (no se puede mirar más allá de minPitch / maxPitch).
///
/// Cómo usarlo:
///  1. Poné este script en la cámara (la Main Camera).
///  2. Arrastrá el Transform del player al campo "Target".
///  3. Listo. Combina perfecto con ThirdPersonMovement (movimiento relativo a la cámara).
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("El player al que sigue la cámara. Si está vacío, busca un ThirdPersonMovement en escena.")]
    public Transform target;
    [Tooltip("Desplazamiento del punto al que mira (ej: subir a la altura de la cabeza/pecho).")]
    public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Distancia")]
    [Tooltip("Qué tan lejos se ubica la cámara del player.")]
    public float distance = 5f;

    [Header("Sensibilidad del mouse")]
    public float mouseSensitivityX = 0.18f;
    public float mouseSensitivityY = 0.14f;
    [Tooltip("Invertir el eje vertical del mouse.")]
    public bool invertY = false;

    [Header("Clamp vertical")]
    [Tooltip("Ángulo mínimo (mirar hacia arriba). Negativo = arriba.")]
    public float minPitch = -35f;
    [Tooltip("Ángulo máximo (mirar hacia abajo).")]
    public float maxPitch = 70f;

    [Header("Suavizado")]
    [Tooltip("Suavizado del seguimiento de posición. 0 = instantáneo.")]
    public float positionSmoothTime = 0.05f;
    [Tooltip("Suavizado de la rotación de la cámara. 0 = instantáneo.")]
    public float rotationSmoothTime = 0.04f;

    [Header("Colisión (opcional)")]
    [Tooltip("Acerca la cámara si hay una pared/objeto entre ella y el player.")]
    public bool enableCollision = true;
    [Tooltip("Capas que la cámara considera obstáculos.")]
    public LayerMask collisionLayers = ~0;
    [Tooltip("Radio del chequeo de colisión.")]
    public float collisionRadius = 0.25f;
    [Tooltip("Margen para que la cámara no quede pegada a la pared.")]
    public float collisionPadding = 0.2f;

    [Header("Cursor")]
    [Tooltip("Bloquea y oculta el cursor al iniciar.")]
    public bool lockCursorOnStart = true;

    // Ángulos actuales de la órbita.
    private float yaw;
    private float pitch;

    // Variables para el suavizado.
    private Vector3 positionVelocity;
    private float currentDistance;

    void Awake()
    {
        // Si no se asignó target, intentamos encontrar el player automáticamente.
        if (target == null)
        {
            var movement = FindFirstObjectByType<ThirdPersonMovement>();
            if (movement != null) target = movement.transform;
        }

        currentDistance = distance;
    }

    void Start()
    {
        // Arrancamos con la órbita alineada a cómo está rotada la cámara.
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
        pitch = NormalizeAngle(pitch);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        HandleMouseInput();
        UpdateCameraTransform();
    }

    /// <summary>Lee el mouse y actualiza los ángulos de la órbita, con clamp en el eje vertical.</summary>
    private void HandleMouseInput()
    {
        // Si hay un menú abierto, no movemos la cámara (consistente con NetworkPlayerController).
        if (IsAnyMenuOpen()) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue();

        // Eje horizontal: izquierda / derecha.
        yaw += delta.x * mouseSensitivityX;

        // Eje vertical: arriba / abajo (mover el mouse hacia arriba => mirar hacia arriba).
        float verticalInput = delta.y * mouseSensitivityY;
        pitch -= invertY ? -verticalInput : verticalInput;

        // CLAMP VERTICAL: limita cuánto se puede mirar arriba/abajo.
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    /// <summary>Posiciona y orienta la cámara detrás del player según los ángulos de la órbita.</summary>
    private void UpdateCameraTransform()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Punto al que la cámara mira (player + offset de altura).
        Vector3 pivot = target.position + targetOffset;

        // Distancia deseada, ajustada si hay una pared en el medio.
        float desiredDistance = distance;
        if (enableCollision)
            desiredDistance = GetCollisionAdjustedDistance(pivot, rotation, distance);

        // Suavizamos los cambios de distancia para que no "salte" al chocar paredes.
        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, 1f - Mathf.Exp(-12f * Time.deltaTime));

        // Posición final: detrás del pivot, a la distancia calculada.
        Vector3 desiredPosition = pivot - (rotation * Vector3.forward) * currentDistance;

        // Suavizado de posición.
        if (positionSmoothTime > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);
        else
            transform.position = desiredPosition;

        // Suavizado de rotación.
        if (rotationSmoothTime > 0f)
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-(1f / rotationSmoothTime) * Time.deltaTime));
        else
            transform.rotation = rotation;
    }

    /// <summary>Si hay un obstáculo entre el player y la cámara, devuelve una distancia más corta.</summary>
    private float GetCollisionAdjustedDistance(Vector3 pivot, Quaternion rotation, float wishDistance)
    {
        Vector3 dir = -(rotation * Vector3.forward);
        if (Physics.SphereCast(pivot, collisionRadius, dir, out RaycastHit hit, wishDistance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Max(0f, hit.distance - collisionPadding);
        }
        return wishDistance;
    }

    /// <summary>True si algún menú del juego está abierto (settings o misiones).</summary>
    private bool IsAnyMenuOpen()
    {
        if (UIVoice.Instance != null && UIVoice.Instance.IsSettingsOpen) return true;
        if (PhotoMissionUI.Instance != null && PhotoMissionUI.Instance.IsUIOpen) return true;
        return false;
    }

    /// <summary>Convierte un ángulo a un rango de -180 a 180 para poder hacer clamp correctamente.</summary>
    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Vector3 pivot = target.position + targetOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pivot, 0.15f);
        Gizmos.DrawLine(pivot, transform.position);
    }
}
