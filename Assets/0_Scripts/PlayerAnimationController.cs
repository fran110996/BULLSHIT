using UnityEngine;

/// <summary>
/// Lee los parametros de animacion de NetworkPlayerController (que los sincroniza por red)
/// y los aplica al Animator local. NO es un NetworkBehaviour - es solo un lector.
/// </summary>
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Suavizado")]
    public float dampTime = 0.1f;

    [Header("Referencias (se buscan automaticamente)")]
    public Animator animator;

    private NetworkPlayerController playerController;

    private int hashMoveX;
    private int hashMoveZ;
    private int hashSpeed;
    private int hashIsGrounded;
    private int hashIsJumping;

    private float smoothMoveX;
    private float smoothMoveZ;

    // Para fijar el hueso raiz y evitar que el salto mueva el modelo
    private Transform hipsBone;
    private float hipsBaseY;
    private bool hipsBaseRecorded = false;
    private bool initialized = false;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        if (initialized) return;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        playerController = GetComponent<NetworkPlayerController>();

        if (animator == null || playerController == null) return;

        animator.applyRootMotion = false;

        // Buscar el hueso Hips
        hipsBone = FindHipsBone(animator.transform);
        if (hipsBone != null)
            Debug.Log($"[AnimController] Hips encontrado: {hipsBone.name}");

        hashMoveX = Animator.StringToHash("MoveX");
        hashMoveZ = Animator.StringToHash("MoveZ");
        hashSpeed = Animator.StringToHash("Speed");
        hashIsGrounded = Animator.StringToHash("IsGrounded");
        hashIsJumping = Animator.StringToHash("IsJumping");

        initialized = true;
        Debug.Log($"[AnimController] OK - Owner: {playerController.IsOwner}");
    }

    Transform FindHipsBone(Transform parent)
    {
        foreach (Transform child in parent)
        {
            string name = child.name.ToLower();
            if (name.Contains("hips") || name.Contains("pelvis"))
                return child;
            Transform found = FindHipsBone(child);
            if (found != null) return found;
        }
        return null;
    }

    void Update()
    {
        if (!initialized)
        {
            Initialize();
            if (!initialized) return;
        }

        // Grabar posicion base del Hips
        if (hipsBone != null && !hipsBaseRecorded)
        {
            hipsBaseY = hipsBone.localPosition.y;
            hipsBaseRecorded = true;
        }

        // SIEMPRE leer de las NetworkVariables (funcionan tanto para owner como para remotos)
        float targetMoveX = playerController.NetMoveX.Value;
        float targetMoveZ = playerController.NetMoveZ.Value;
        bool isGrounded = playerController.NetIsGrounded.Value;
        bool isJumping = playerController.NetIsJumping.Value;

        // Suavizar
        float lerpFactor = 1f - Mathf.Exp(-10f * Time.deltaTime / Mathf.Max(dampTime, 0.01f));
        smoothMoveX = Mathf.Lerp(smoothMoveX, targetMoveX, lerpFactor);
        smoothMoveZ = Mathf.Lerp(smoothMoveZ, targetMoveZ, lerpFactor);

        if (Mathf.Abs(smoothMoveX) < 0.02f && Mathf.Abs(targetMoveX) < 0.01f) smoothMoveX = 0f;
        if (Mathf.Abs(smoothMoveZ) < 0.02f && Mathf.Abs(targetMoveZ) < 0.01f) smoothMoveZ = 0f;

        float speed = new Vector2(smoothMoveX, smoothMoveZ).magnitude;

        animator.SetFloat(hashMoveX, smoothMoveX);
        animator.SetFloat(hashMoveZ, smoothMoveZ);
        animator.SetFloat(hashSpeed, speed);
        animator.SetBool(hashIsGrounded, isGrounded);
        animator.SetBool(hashIsJumping, isJumping);
    }

    void LateUpdate()
    {
        if (hipsBone == null || !hipsBaseRecorded) return;

        // Cuando esta en el suelo, forzar hips a posicion original
        bool grounded = playerController != null 
            ? playerController.NetIsGrounded.Value 
            : true;

        if (grounded)
        {
            Vector3 hipsPos = hipsBone.localPosition;
            hipsPos.y = hipsBaseY;
            hipsBone.localPosition = hipsPos;
        }
    }
}
