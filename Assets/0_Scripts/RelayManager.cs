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

    public async Task<string> CreateRelay(int maxConnections)
    {
        try
        {

            expectedPlayers = maxConnections + 1;
            Debug.Log($"maxConnections: {maxConnections}, expectedPlayers: {expectedPlayers}");

            // Si estas solo, expectedPlayers = 1
            if (expectedPlayers < 1) expectedPlayers = 1;

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

            // Esperar a que todos los clientes se conecten antes de cargar la escena
            StartCoroutine(WaitForPlayersAndLoad());

            return joinCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error creando Relay: {e}");
            return null;
        }
    }

    private IEnumerator WaitForPlayersAndLoad()
    {
        if (expectedPlayers <= 1)
        {
            Debug.Log("Solo un jugador, cargando directo...");
            NetworkManager.Singleton.SceneManager.LoadScene("1_GameScene",
                UnityEngine.SceneManagement.LoadSceneMode.Single);

            // Unirse al canal de voz sin await (fire and forget)
            JoinVoiceAsync();
            yield break;
        }

        Debug.Log($"Esperando {expectedPlayers} jugadores...");
        float timeout = 5f;

        while (timeout > 0)
        {
            int connected = NetworkManager.Singleton.ConnectedClientsList.Count;
            if (connected >= expectedPlayers)
            {
                NetworkManager.Singleton.SceneManager.LoadScene("1_GameScene",
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
                JoinVoiceAsync();
                yield break;
            }
            timeout -= 1f;
            yield return new WaitForSeconds(1f);
        }

        NetworkManager.Singleton.SceneManager.LoadScene("1_GameScene",
            UnityEngine.SceneManagement.LoadSceneMode.Single);
        JoinVoiceAsync();
    }

    private async void JoinVoiceAsync()
    {
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