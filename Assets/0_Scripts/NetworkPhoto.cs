using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class NetworkPhoto : NetworkBehaviour, IInteractable
{
    [Header("Referencias")]
    public MeshRenderer photoRenderer;
    
    [Header("Variables de Red")]
    public NetworkVariable<ulong> heldByClientId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<bool> isRevealed = new NetworkVariable<bool>(false);
    
    private Texture2D photoTexture;
    private Material photoMaterial;
    private List<string> objectsDetected = new List<string>();
    private Transform targetFollow; 
    private Collider[] colliders;

    private void Awake()
    {
        photoMaterial = photoRenderer.material;
        colliders = GetComponentsInChildren<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        isRevealed.OnValueChanged += OnRevealStateChanged;
        heldByClientId.OnValueChanged += OnHolderChanged;
        
        if (heldByClientId.Value != ulong.MaxValue) OnHolderChanged(ulong.MaxValue, heldByClientId.Value);
        
        UpdateVisuals();
    }

    [ClientRpc]
    public void SetPhotoDataClientRpc(byte[] imageBytes, string delimitedObjects)
    {
        if (!string.IsNullOrEmpty(delimitedObjects))
            objectsDetected = new List<string>(delimitedObjects.Split('|'));
        
        photoTexture = new Texture2D(2, 2);
        photoTexture.LoadImage(imageBytes);
        
        if (photoMaterial.HasProperty("_BaseMap")) photoMaterial.SetTexture("_BaseMap", photoTexture);
        else photoMaterial.SetTexture("_MainTex", photoTexture);
        
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        Color targetColor = isRevealed.Value ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
        if (photoMaterial.HasProperty("_BaseColor")) photoMaterial.SetColor("_BaseColor", targetColor);
        else photoMaterial.SetColor("_Color", targetColor);
    }

    private void OnRevealStateChanged(bool oldVal, bool newVal)
    {
        if (newVal) StartCoroutine(RevealEffect());
    }

    private IEnumerator RevealEffect()
    {
        float t = 0;
        Color startColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        if (photoMaterial.HasProperty("_BaseColor")) startColor = photoMaterial.GetColor("_BaseColor");
        else if (photoMaterial.HasProperty("_Color")) startColor = photoMaterial.GetColor("_Color");

        while (t < 1)
        {
            t += Time.deltaTime * 0.5f; 
            Color lerpColor = Color.Lerp(startColor, Color.white, t);
            if (photoMaterial.HasProperty("_BaseColor")) photoMaterial.SetColor("_BaseColor", lerpColor);
            else photoMaterial.SetColor("_Color", lerpColor);
            yield return null;
        }
    }

    private void OnHolderChanged(ulong oldVal, ulong newVal)
    {
        if (newVal == ulong.MaxValue)
        {
            targetFollow = null;
            // Reactivar colisiones
            foreach (var col in colliders) col.enabled = true;
            
            if (TryGetComponent<Rigidbody>(out Rigidbody rb)) rb.isKinematic = false;
        }
        else
        {
            // Desactivar colisiones para que no empujen al jugador
            foreach (var col in colliders) col.enabled = false;
            
            if (TryGetComponent<Rigidbody>(out Rigidbody rb)) rb.isKinematic = true;
            FindTargetToFollow(newVal);
        }
    }

    private void FindTargetToFollow(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            var npc = client.PlayerObject.GetComponent<NetworkPlayerController>();
            if (npc != null) targetFollow = npc.cameraHolder;
        }
    }

void LateUpdate()
    {
        if (targetFollow != null)
        {
            // Ms lejos (2.0f) para evitar que la camara la "corte" por estar muy cerca
            // Ms abajo (-0.6f) para que se vea mas "en la mano"
            // Ms a la derecha (0.6f)
            Vector3 offset = targetFollow.right * 0.6f + targetFollow.up * -0.6f + targetFollow.forward * 2.0f;
            
            transform.position = targetFollow.position + offset;
            
            // Inclinacion mas suave para evitar que las esquinas choquen con el plano de la camara
            transform.rotation = targetFollow.rotation * Quaternion.Euler(10, -15, 0);
        }
    }

    public string GetInteractText() => (heldByClientId.Value != ulong.MaxValue) ? "" : "Presiona E para recoger foto";
    public bool CanInteract(GameObject player) => heldByClientId.Value == ulong.MaxValue;

    public void Interact(GameObject player)
    {
        ulong clientId = player.GetComponent<NetworkBehaviour>().OwnerClientId;
        RequestGrabServerRpc(clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestGrabServerRpc(ulong clientId)
    {
        if (heldByClientId.Value == ulong.MaxValue)
        {
            heldByClientId.Value = clientId;
            GetComponent<NetworkObject>().ChangeOwnership(clientId);
        }
    }

    [ServerRpc]
    private void RequestDropServerRpc()
    {
        heldByClientId.Value = ulong.MaxValue;
        GetComponent<NetworkObject>().RemoveOwnership();
    }

    void Update()
    {
        if (IsOwner && heldByClientId.Value == OwnerClientId)
        {
            // Soltar con la tecla 'G'
            if (Input.GetKeyDown(KeyCode.G))
            {
                RequestDropServerRpc();
            }

            if (!isRevealed.Value)
            {
                if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0))
                    RequestRevealServerRpc();
            }
        }
    }

    [ServerRpc]
    private void RequestRevealServerRpc() => isRevealed.Value = true;
}
