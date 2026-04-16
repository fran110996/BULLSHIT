using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

/// <summary>
/// Manager central que sincroniza todos los elementos fotografiados por todos los jugadores.
/// </summary>
public class PhotoMissionManager : NetworkBehaviour
{
    public static PhotoMissionManager Instance;

    // Lista sincronizada de elementos capturados. 
    // Usamos FixedString128Bytes para soportar nombres de objetos y posibles estados.
    public NetworkList<FixedString128Bytes> capturedElements;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Inicializar la lista. Nota: NetworkList debe inicializarse en Awake.
        capturedElements = new NetworkList<FixedString128Bytes>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log("[PhotoMissionManager] Sistema de misiones activo.");
    }

    /// <summary>
    /// Registra una lista de elementos en el log global.
    /// Llamado por cualquier cliente cuando toma una foto.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RegisterCapturedElementsServerRpc(string delimitedElements)
    {
        string[] elements = delimitedElements.Split('|');
        foreach (string element in elements)
        {
            if (string.IsNullOrEmpty(element)) continue;
            
            // Segun peticion del usuario: si hay 3 palmeras, aparecen 3 palmeras.
            // Asi que simplemente añadimos todo lo detectado.
            capturedElements.Add(element);
            Debug.Log($"[Server] Elemento registrado: {element}");
        }
    }
}
