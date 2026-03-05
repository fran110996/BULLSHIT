using UnityEngine;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(false);

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        gameStarted.OnValueChanged += OnGameStarted;
    }

    public void SetPlayerReady(bool ready)
    {
        SteamLobbyManager.Instance.SetMemberReady(ready);
        LobbyUIManager.Instance?.RefreshPlayerList();
    }

    public bool AllPlayersReady()
    {
        return SteamLobbyManager.Instance.AllReady();
    }

    public void StartGame()
    {
        if (!IsHost) return;
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene",
            UnityEngine.SceneManagement.LoadSceneMode.Single);
        gameStarted.Value = true;
    }

    private void OnGameStarted(bool prev, bool current)
    {
        if (current)
            Debug.Log("Juego iniciado");
    }
}