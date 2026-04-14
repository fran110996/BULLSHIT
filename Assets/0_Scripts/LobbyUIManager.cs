using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using System.Collections;
using System.Collections.Generic;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance;

    [Header("Pantallas")]
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;

    [Header("Main Menu")]
    public Button createLobbyBtn;

    [Header("Lobby")]
    public Transform playerListContainer;
    public GameObject playerCardPrefab;
    public Button readyButton;
    public TextMeshProUGUI readyButtonText;
    public Button startButton;
    public TextMeshProUGUI lobbyStatusText;

    private bool isReady = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);

        createLobbyBtn.onClick.AddListener(OnClickCreate);
        readyButton.onClick.AddListener(OnClickReady);
        startButton.onClick.AddListener(OnClickStart);
        startButton.gameObject.SetActive(false);
    }

    void Update()
    {
        // ATAJOS DE DEBUG PARA TESTEO RAPIDO (LAN)
        // Mantener Shift y presionar H para Host local o J para Join local
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                RelayManager.Instance.StartLocalHost();
                OnLobbyReady(); // Ocultar UI de menu
            }
            else if (Input.GetKeyDown(KeyCode.J))
            {
                RelayManager.Instance.StartLocalClient();
                OnLobbyReady(); // Ocultar UI de menu
            }
        }
    }

    void OnClickCreate()
    {
        if (GameInitializer.Instance != null && GameInitializer.Instance.IsSteamAvailable)
        {
            SteamLobbyManager.Instance.CreateLobby();
        }
        else
        {
            Debug.LogWarning("Steam no disponible. Usa Shift+H para iniciar modo local.");
        }
    }

    void OnClickReady()
    {
        isReady = !isReady;
        readyButtonText.text = isReady ? "¡LISTO!" : "LISTO?";
        SteamLobbyManager.Instance.SetMemberReady(isReady);
    }

    void OnClickStart()
    {
        // En modo Steam, el host decide cuando empezar
        RelayManager.Instance.CreateRelay(4);
    }

    public void OnLobbyReady()
    {
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        RefreshPlayerList();
    }

    public void RefreshPlayerList()
    {
        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        // Si estamos en modo Steam, listar miembros de Steam
        if (GameInitializer.Instance != null && GameInitializer.Instance.IsSteamAvailable)
        {
            var members = SteamLobbyManager.Instance.GetMembers();
            CSteamID hostId = SteamMatchmaking.GetLobbyOwner(SteamLobbyManager.Instance.CurrentLobbyID);
            
            foreach (var m in members)
            {
                GameObject card = Instantiate(playerCardPrefab, playerListContainer);
                var cardScript = card.GetComponent<PlayerCardUI>();
                
                string name = m == SteamUser.GetSteamID() ? "Yo" : SteamFriends.GetFriendPersonaName(m);
                bool ready = SteamLobbyManager.Instance.IsMemberReady(m);
                bool isHost = m == hostId;
                
                if (cardScript != null)
                {
                    cardScript.Configure(name, ready, isHost);
                }
            }

            if (SteamLobbyManager.Instance.IsHost)
            {
                startButton.gameObject.SetActive(true);
                startButton.interactable = SteamLobbyManager.Instance.AllReady();
            }
        }
        else
        {
            // En modo local esto se ignora mayormente porque pasamos directo a la escena de juego
            if (lobbyStatusText != null) lobbyStatusText.text = "Modo Local / LAN";
        }
    }
}
