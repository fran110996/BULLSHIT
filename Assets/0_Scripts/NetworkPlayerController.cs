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
    // El Server escribe (via ServerRpc del owner), todos leen
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
        Debug.Log($"OnNetworkSpawn - IsOwner: {IsOwner}, OwnerClientId: {OwnerClientId}");

        NetworkPlayerName.OnValueChanged += OnNameChanged;

        if (!IsOwner)
        {
            if (cameraHolder != null)
            {
                Camera cam = cameraHolder.GetComponentInChildren<Camera>();
                if (cam != null) cam.gameObject.SetActive(false);
            }

            if (!string.IsNullOrEmpty(NetworkPlayerName.Value.ToString()))
            {
                UpdatePlayerVisuals(NetworkPlayerName.Value.ToString());
            }

            // NO desactivamos enabled, dejamos Update corriendo para leer NetworkVariables
            return;
        }

        // --- SOLO PARA EL OWNER ---
        if (cameraHolder != null)
        {
            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam != null) cam.gameObject.SetActive(true);
        }

        if (bodyMesh != null) bodyMesh.SetActive(false);

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;

        if (VoiceManager.Instance != null)
        {
            VoiceManager.Instance.SetLocalPlayer(this);
            JoinVoiceAsync();
        }

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
        Debug.Log($"Actualizando visuales para: {name}, IsOwner: {IsOwner}");
        
        if (IsOwner) return;

        var fn = GetOrCreateFloatingName();
        if (fn != null)
        {
            fn.Initialize(transform, name);
        }

        VoiceManager.Instance?.RegisterPlayer(name, this);
    }

    private async void JoinVoiceAsync()
    {
        await System.Threading.Tasks.Task.Delay(500);
        if (VoiceManager.Instance != null)
            await VoiceManager.Instance.JoinVoiceChannel("GameRoom");
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
            HandleCursorToggle();

            // Calcular valores de animacion y enviar al server
            float rawH = RawInputH;
            float rawV = RawInputV;
            float targetMoveX, targetMoveZ;

            if (IsSprinting && (Mathf.Abs(rawH) > 0.01f || Mathf.Abs(rawV) > 0.01f))
            {
                targetMoveX = rawH;
                targetMoveZ = Mathf.Clamp(rawV * 2f, -1f, 1f);
            }
            else
            {
                targetMoveX = rawH * 0.5f;
                targetMoveZ = rawV * 0.5f;
            }

            // Enviar al servidor para que todos vean las animaciones
            SyncAnimationServerRpc(targetMoveX, targetMoveZ, IsGrounded, IsJumping);
        }
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(0f, mouseX, 0f);
    }

    void HandleMovement()
    {
        IsGrounded = controller.isGrounded;

        if (IsGrounded && !wasGrounded)
        {
            IsJumping = false;
        }
        wasGrounded = IsGrounded;

        if (IsGrounded && velocity.y < 0f)
            velocity.y = -2f;

        InputH = Input.GetAxis("Horizontal");
        InputV = Input.GetAxis("Vertical");
        RawInputH = Input.GetAxisRaw("Horizontal");
        RawInputV = Input.GetAxisRaw("Vertical");
        IsSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        float currentSpeed = IsSprinting ? runSpeed : walkSpeed;

        Vector3 move = transform.right * InputH + transform.forward * InputV;
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

    void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void SetSpeaking(bool speaking)
    {
        if (floatingNameInstance != null)
            floatingNameInstance.SetSpeaking(speaking);
    }
}
