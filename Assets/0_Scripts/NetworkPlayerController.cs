using UnityEngine;
using Unity.Netcode;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float jumpForce = 2f;
    public float gravity = -20f;

    [Header("Camara FPS")]
    public Transform cameraHolder;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;

    [Header("Referencias")]
    public GameObject bodyMesh;
    public GameObject floatingNamePrefab;

    public string PlayerName { get; private set; }

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;
    private FloatingName floatingNameInstance;

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

        if (!IsOwner)
        {
            if (cameraHolder != null)
            {
                Camera cam = cameraHolder.GetComponentInChildren<Camera>();
                if (cam != null) cam.gameObject.SetActive(false);
            }
            enabled = false;
            return;
        }

        if (cameraHolder != null)
        {
            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam != null) cam.gameObject.SetActive(true);
        }

        if (bodyMesh != null) bodyMesh.SetActive(false);

        enabled = true;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;

        JoinVoiceAsync();

        string steamName = Steamworks.SteamFriends.GetPersonaName();
        SetNameServerRpc(steamName);
    }

    private async void JoinVoiceAsync()
    {
        await System.Threading.Tasks.Task.Delay(1000);
        await VoiceManager.Instance.JoinVoiceChannel("GameRoom");
    }

    [ServerRpc]
    private void SetNameServerRpc(string name)
    {
        SetNameClientRpc(name);
    }

    [ClientRpc]
    private void SetNameClientRpc(string name)
    {
        Debug.Log($"SetNameClientRpc recibido - nombre: {name}, IsOwner: {IsOwner}");
        PlayerName = name;

        if (IsOwner) return;

        var fn = GetOrCreateFloatingName();
        if (fn != null)
        {
            Debug.Log($"Inicializando FloatingName con: {name}");
            fn.Initialize(transform, name);
        }
        else
        {
            Debug.LogError($"No se pudo crear FloatingName para: {name}, floatingNamePrefab es null");
        }

        VoiceManager.Instance?.RegisterPlayer(name, this);
    }

    void Update()
    {
        if (!IsOwner) return;
        HandleLook();
        HandleMovement();
        HandleCursorToggle();
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
        bool grounded = controller.isGrounded;

        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = transform.right * h + transform.forward * v;
        if (move.magnitude > 1f) move.Normalize();

        controller.Move(move * moveSpeed * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && grounded)
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

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