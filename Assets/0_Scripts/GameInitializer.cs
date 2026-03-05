using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Steamworks;
using System.Threading.Tasks;
using Unity.Netcode;

public class GameInitializer : MonoBehaviour
{
    public static GameInitializer Instance;

    async void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!SteamAPI.Init())
        {
            Debug.LogError("Steam no está corriendo.");
            return;
        }

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        await VoiceManager.Instance.InitializeVoice();

        Debug.Log("Steam y Unity Services inicializados");
    }

    void OnApplicationQuit()
    {
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
            NetworkManager.Singleton.Shutdown();

        if (VoiceManager.Instance != null)
            _ = VoiceManager.Instance.LeaveVoiceChannel();

        SteamLobbyManager.Instance?.LeaveLobby();
        SteamAPI.Shutdown();
    }
}