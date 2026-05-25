using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cámara tercera persona estándar (estilo GTA / Fortnite).
/// - Posición: sigue al jugador en todo momento.
/// - Rotación: SOLO con el mouse.
/// - Scroll: zoom.
///
/// Setup: poner este script en la Main Camera y arrastrar el Player a 'target'.
/// Si target queda vacío, busca un GameObject llamado "Player" o con tag "Player".
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform target;
    [Tooltip("Offset vertical respecto al pivot del target.")]
    public float heightOffset = 1.5f;

    [Header("Distancia / Zoom")]
    public float distance = 5f;
    public float minDistance = 2f;
    public float maxDistance = 10f;
    public float zoomSpeed = 1.5f;

    [Header("Mouse")]
    [Tooltip("Sensibilidad general del mouse. Subí este valor si la cámara casi no se mueve.")]
    public float mouseSensitivity = 1f;
    public bool invertY = false;
    public float minPitch = -35f;
    public float maxPitch = 70f;

    [Header("Suavizado posición")]
    [Range(0f, 0.3f)] public float positionSmooth = 0.05f;

    [Header("Cursor")]
    public bool lockCursor = true;

    [Header("Anti-paredes")]
    public bool avoidWalls = true;

    [Header("Debug")]
    [Tooltip("Imprimir el delta del mouse en consola para depurar.")]
    public bool debugLogs = false;

    // Acciones del Input System creadas en código (no hay que asignar nada).
    private InputAction lookAction;
    private InputAction zoomAction;

    private float yaw;
    private float pitch = 15f;
    private Vector3 posVel;

    void Awake()
    {
        // Crear InputActions directamente. Bindeados al mouse del sistema.
        lookAction = new InputAction("Look",
            type: InputActionType.Value,
            binding: "<Mouse>/delta",
            expectedControlType: "Vector2");
        zoomAction = new InputAction("Zoom",
            type: InputActionType.Value,
            binding: "<Mouse>/scroll",
            expectedControlType: "Vector2");
    }

    void OnEnable()
    {
        lookAction.Enable();
        zoomAction.Enable();
    }

    void OnDisable()
    {
        lookAction.Disable();
        zoomAction.Disable();
    }

    void Start()
    {
        // Auto-buscar target si quedó vacío.
        if (target == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go == null) go = GameObject.Find("Player");
            if (go != null) target = go.transform;
        }

        if (target == null)
            Debug.LogWarning("[CameraFollow] No hay target asignado y no encontré un GameObject 'Player'. Arrastrá el player al campo Target.");

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (target != null) yaw = target.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // --- Rotación: SOLO con el mouse ---
        Vector2 lookDelta = lookAction.ReadValue<Vector2>();
        if (debugLogs && lookDelta.sqrMagnitude > 0.001f)
            Debug.Log($"[CameraFollow] mouse delta = {lookDelta}");

        // sensibilidad base (0.1) x multiplicador.
        float sens = 0.1f * mouseSensitivity;
        yaw   += lookDelta.x * sens;
        pitch += (invertY ? lookDelta.y : -lookDelta.y) * sens;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Zoom con scroll.
        Vector2 scroll = zoomAction.ReadValue<Vector2>();
        if (Mathf.Abs(scroll.y) > 0.001f)
        {
            distance -= (scroll.y / 120f) * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        // --- Posición: detrás del player según yaw/pitch ---
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = target.position + Vector3.up * heightOffset;
        Vector3 desiredPos = focus - rot * Vector3.forward * distance;

        if (avoidWalls && Physics.Linecast(focus, desiredPos, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            desiredPos = hit.point + hit.normal * 0.15f;

        transform.rotation = rot;
        transform.position = positionSmooth <= 0.001f
            ? desiredPos
            : Vector3.SmoothDamp(transform.position, desiredPos, ref posVel, positionSmooth);
    }
}
