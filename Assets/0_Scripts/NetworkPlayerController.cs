using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6.5f;
    public float jumpForce = 2f;
    public float gravity = -20f;

    [Header("Camara FPS")]
    public Transform cameraHolder;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;

    [Header("Referencias")]
    public GameObject bodyMesh;
    public GameObject floatingNamePrefab;
    public PhotoCameraSystem cameraSystem;
    public PlayerInteraction interactionSystem;

    // --- Propiedades publicas para el sistema de animacion ---
    public float InputH { get; private set; }
    public float InputV { get; private set; }
    public float RawInputH { get; private set; }
    public float RawInputV { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsJumping { get; private set; }
    public bool IsSprinting { get; private set; }

    // Sincronizacion robusta del nombre
    public NetworkVariable<FixedString32Bytes> NetworkPlayerName = new NetworkVariable<FixedString32Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // --- NetworkVariables para sincronizar animaciones ---
    public NetworkVariable<float> NetMoveX = new NetworkVariable<float>(0f);
    public NetworkVariable<float> NetMoveZ = new NetworkVariable<float>(0f);
    public NetworkVariable<bool> NetIsGrounded = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> NetIsJumping = new NetworkVariable<bool>(false);

    public string PlayerName => NetworkPlayerName.Value.ToString();

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;
    private FloatingName floatingNameInstance;
    private bool wasGrounded = true;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private FloatingName GetOrCreateFloatingName()
    {
        if (floatingNameInstance != null) return floatingNameInstance;

        if (floatingNamePrefab != null)
        {
            GameObject nameObj = Instantiate(floatingNamePrefab, transform.position + Vector3.up * 2.5f, Quaternion.identity);
            floatingNameInstance = nameObj.GetComponent<FloatingName>();
        }

        return floatingNameInstance;
    }

    public override void OnNetworkSpawn()
    {
        NetworkPlayerName.OnValueChanged += OnNameChanged;

        // Forzar actualizacion inicial si ya tiene nombre (ej: el host cuando entra un cliente)
        if (!string.IsNullOrEmpty(NetworkPlayerName.Value.ToString()))
        {
            UpdatePlayerVisuals(NetworkPlayerName.Value.ToString());
        }

        if (!IsOwner)
        {
            if (cameraHolder != null)
            {
                Camera cam = cameraHolder.GetComponentInChildren<Camera>();
                if (cam != null) cam.gameObject.SetActive(false);
            }
            return;
        }

        if (cameraHolder != null)
        {
            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam != null) cam.gameObject.SetActive(true);
        }

        if (bodyMesh != null) bodyMesh.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        string finalName = "Player_" + Random.Range(100, 999);
        if (GameInitializer.Instance != null && GameInitializer.Instance.IsSteamAvailable)
        {
            finalName = Steamworks.SteamFriends.GetPersonaName();
        }
        
        SetNameServerRpc(finalName);
    }

    public override void OnNetworkDespawn()
    {
        NetworkPlayerName.OnValueChanged -= OnNameChanged;
    }

    private void OnNameChanged(FixedString32Bytes oldName, FixedString32Bytes newName)
    {
        UpdatePlayerVisuals(newName.ToString());
    }

    private void UpdatePlayerVisuals(string name)
    {
        if (IsOwner) return;

        var fn = GetOrCreateFloatingName();
        if (fn != null)
        {
            fn.Initialize(transform, name);
        }
    }

    [ServerRpc]
    private void SetNameServerRpc(string name)
    {
        NetworkPlayerName.Value = name;
    }

    [ServerRpc]
    private void SyncAnimationServerRpc(float moveX, float moveZ, bool grounded, bool jumping)
    {
        NetMoveX.Value = moveX;
        NetMoveZ.Value = moveZ;
        NetIsGrounded.Value = grounded;
        NetIsJumping.Value = jumping;
    }

    void Update()
    {
        if (IsOwner)
        {
            HandleLook();
            HandleMovement();

            // Calcular valores de animacion y enviar al server
            SyncAnimationServerRpc(RawInputH * (IsSprinting ? 1f : 0.5f), RawInputV * (IsSprinting ? 1f : 0.5f), IsGrounded, IsJumping);
        }
    }

    void HandleLook()
    {
        // BLOQUEO DE ROTACION SI SETTINGS ESTA ABIERTO
        if (UIVoice.Instance != null && UIVoice.Instance.IsSettingsOpen) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(0f, mouseX, 0f);
    }

    void HandleMovement()
    {
        // Opcional: Bloquear movimiento en settings
        if (UIVoice.Instance != null && UIVoice.Instance.IsSettingsOpen) return;

        IsGrounded = controller.isGrounded;

        if (IsGrounded && !wasGrounded)
        {
            IsJumping = false;
        }
        wasGrounded = IsGrounded;

        if (IsGrounded && velocity.y < 0f)
            velocity.y = -2f;

        InputH = Input.GetAxisRaw("Horizontal");
        InputV = Input.GetAxisRaw("Vertical");
        RawInputH = Input.GetAxisRaw("Horizontal");
        RawInputV = Input.GetAxisRaw("Vertical");
        IsSprinting = Input.GetKey(KeyCode.LeftShift);

        float currentSpeed = IsSprinting ? runSpeed : walkSpeed;

        Vector3 move = transform.right * RawInputH + transform.forward * RawInputV;
        if (move.magnitude > 1f) move.Normalize();

        controller.Move(move * currentSpeed * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && IsGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            IsJumping = true;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    public void SetSpeaking(bool speaking)
    {
        if (floatingNameInstance != null)
            floatingNameInstance.SetSpeaking(speaking);
    }

    void HandleCursorToggle()
    {
        // Esta funcion se ha movido a UIVoice para centralizar el menu de Settings
    }
}
