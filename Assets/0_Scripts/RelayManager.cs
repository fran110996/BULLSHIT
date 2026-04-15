using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using System.Threading.Tasks;
using System.Collections;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance;

    private int expectedPlayers = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async Task<string> CreateRelay(int maxConnections, int targetPlayers = -1)
    {
        try
        {
            // Si no se especifica targetPlayers, usamos el maxConnections + 1
            expectedPlayers = targetPlayers > 0 ? targetPlayers : maxConnections + 1;
            Debug.Log($"maxConnections: {maxConnections}, expectedPlayers: {expectedPlayers}");

            Allocation allocation = await RelayService.Instance
                .CreateAllocationAsync(maxConnections);

            string joinCode = await RelayService.Instance
                .GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"Relay creado. Join Code: {joinCode}");

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.ConnectionData,
                allocation.ConnectionData,
                allocation.Key,
                false));

            SteamLobbyManager.Instance.SetRelayCode(joinCode);
            NetworkManager.Singleton.StartHost();

            StartCoroutine(WaitForPlayersAndLoad());

            return joinCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error creando Relay: {e}");
            return null;
        }
    }

    // --- MODOS LOCALES PARA TESTEO RAPIDO ---
    
    public void StartLocalHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        // Resetear a modo IP directo (localhost)
        transport.SetConnectionData("127.0.0.1", 7777);
        
        Debug.Log("Iniciando Host Local (Localhost:7777)...");
        NetworkManager.Singleton.StartHost();
        
        // Cargar escena de juego inmediatamente sin esperar mas gente
        NetworkManager.Singleton.SceneManager.LoadScene("1_GameScene", 
            UnityEngine.SceneManagement.LoadSceneMode.Single);
            
        JoinVoiceAsync();
    }

    public void StartLocalClient()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 7777);
        
        Debug.Log("Uniendose a Host Local (127.0.0.1:7777)...");
        NetworkManager.Singleton.StartClient();
    }

    private IEnumerator WaitForPlayersAndLoad()
    {
        if (expectedPlayers <= 1)
        {
            Debug.Log("✓ Iniciando en solitario. Cargando GameScene...");
            NetworkManager.Singleton.SceneManager.LoadScene("1_GameScene",
                UnityEngine.SceneManagement.LoadSceneMode.Single);

            JoinVoiceAsync();
            yield break;
        }

        Debug.Log($"⌛ Esperando a que se conecten {expectedPlayers} jugadores (incluyendo Host)...");
        float timeout = 10f; // Aumentamos un poco el timeout por si el relay va lento

        while (timeout > 0)
        {
            int connected = NetworkManager.Singleton.ConnectedClientsList.Count;
            Debug.Log($"Sincronización de red: {connected}/{expectedPlayers} jugadores conectados.");

            if (connected >= expectedPlayers)
            {
                Debug.Log("✓ Todos los jugadores conectados. Cargando GameScene para todos...");
                // Pequeña espera extra para asegurar que el apretón de manos de Netcode terminó
                yield return new WaitForSeconds(0.5f);
                
                NetworkManager.Singleton.SceneManager.LoadScene("1_GameScene",
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
                JoinVoiceAsync();
                yield break;
            }
            timeout -= 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        Debug.LogWarning("⚠ El tiempo de espera expiró, pero cargando escena de todos modos con los jugadores que hay.");
        NetworkManager.Singleton.SceneManager.LoadScene("1_GameScene",
            UnityEngine.SceneManagement.LoadSceneMode.Single);
        JoinVoiceAsync();
    }

    private async void JoinVoiceAsync()
    {
        if (VoiceManager.Instance != null)
            await VoiceManager.Instance.JoinVoiceChannel("GameRoom");
    }

    public async void JoinRelay(string joinCode)
    {
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost) return;

        try
        {
            Debug.Log($"Uniendome al Relay con codigo: {joinCode}");

            JoinAllocation joinAllocation = await RelayService.Instance
                .JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData,
                joinAllocation.Key,
                false));

            NetworkManager.Singleton.StartClient();
            Debug.Log("Conectado al host via Relay");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al unirse al Relay: {e}");
        }
    }
}
