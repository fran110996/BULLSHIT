using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Steamworks;
using System.Threading.Tasks;
using Unity.Netcode;

public class GameInitializer : MonoBehaviour
{
    public static GameInitializer Instance;

    public bool IsSteamAvailable { get; private set; }

    async void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Intentar inicializar Steam
        try 
        {
            if (SteamAPI.Init())
            {
                IsSteamAvailable = true;
                Debug.Log("Steam inicializado correctamente.");
            }
            else
            {
                IsSteamAvailable = false;
                Debug.LogWarning("Steam no esta corriendo. El juego funcionara en modo LOCAL/LAN.");
            }
        }
        catch (System.Exception e)
        {
            IsSteamAvailable = false;
            Debug.LogWarning($"Error al inicializar Steam: {e.Message}. Entrando en modo LOCAL/LAN.");
        }

        // Inicializar Servicios de Unity (Relay, Vivox, etc)
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        await VoiceManager.Instance.InitializeVoice();

        Debug.Log("Servicios de Unity inicializados");
    }

    void Update()
    {
        if (IsSteamAvailable)
        {
            SteamAPI.RunCallbacks();
        }
    }

    void OnApplicationQuit()
    {
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
            NetworkManager.Singleton.Shutdown();

        if (VoiceManager.Instance != null)
            _ = VoiceManager.Instance.LeaveVoiceChannel();

        if (IsSteamAvailable)
        {
            SteamLobbyManager.Instance?.LeaveLobby();
            SteamAPI.Shutdown();
        }
    }
}
