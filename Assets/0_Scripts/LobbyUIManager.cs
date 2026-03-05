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

    void OnClickCreate()
    {
        SteamLobbyManager.Instance.CreateLobby();
    }

    void OnClickReady()
    {
        isReady = !isReady;
        SteamLobbyManager.Instance.SetMemberReady(isReady);
        readyButtonText.text = isReady ? "Listo" : "No listo";
        readyButton.image.color = isReady ? new Color(0.2f, 0.8f, 0.2f) : Color.white;
        RefreshPlayerList();
    }

    async void OnClickStart()
    {
        if (!SteamLobbyManager.Instance.IsHost) return;
        if (!SteamLobbyManager.Instance.AllReady()) return;

        startButton.interactable = false;
        lobbyStatusText.text = "Creando sesion...";

        int members = SteamLobbyManager.Instance.GetMembers().Count;
        // Si estas solo members es 1, maxConnections es 1 para evitar el error de 0
        int maxConnections = Mathf.Max(1, members - 1);
        string code = await RelayManager.Instance.CreateRelay(maxConnections);

        if (string.IsNullOrEmpty(code))
        {
            startButton.interactable = true;
            lobbyStatusText.text = "Error al crear sesion, intentá de nuevo";
        }
    }
    public void OnLobbyReady()
    {
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        isReady = false;
        readyButtonText.text = "No listo";
        readyButton.image.color = Color.white;

        // Configurar botones segun rol
        bool soyHost = SteamLobbyManager.Instance.IsHost;
        readyButton.gameObject.SetActive(!soyHost);
        startButton.gameObject.SetActive(soyHost);
        startButton.interactable = false;

        RefreshPlayerList();
        StartCoroutine(PollingLoop());
    }

    private IEnumerator PollingLoop()
    {
        while (lobbyPanel != null && lobbyPanel.activeSelf)
        {
            RefreshPlayerList();
            yield return new WaitForSeconds(1f);
        }
    }

    public void RefreshPlayerList()
    {
        // Si el panel no está activo o fue destruido, no hacer nada
        if (playerListContainer == null || !lobbyPanel.activeSelf) return;

        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        var members = SteamLobbyManager.Instance.GetMembers();

        foreach (var member in members)
        {
            var card = Instantiate(playerCardPrefab, playerListContainer);
            var cardUI = card.GetComponent<PlayerCardUI>();

            cardUI.playerNameText.text = SteamFriends.GetFriendPersonaName(member);

            bool ready = SteamLobbyManager.Instance.IsMemberReady(member);
            cardUI.SetReady(ready);

            StartCoroutine(LoadAvatar(member, cardUI));

            bool isOwner = SteamMatchmaking.GetLobbyOwner(
                SteamLobbyManager.Instance.CurrentLobbyID) == member;
            cardUI.hostIcon.SetActive(isOwner);
        }

        bool soyHost = SteamLobbyManager.Instance.IsHost;
        bool todosReady = SteamLobbyManager.Instance.AllReady();
        bool hayJugadores = members.Count > 0;

        readyButton.gameObject.SetActive(!soyHost);
        startButton.gameObject.SetActive(soyHost);
        startButton.interactable = todosReady && hayJugadores;

        int readyCount = 0;
        foreach (var m in members)
            if (SteamLobbyManager.Instance.IsMemberReady(m)) readyCount++;
        lobbyStatusText.text = $"Listos: {readyCount}/{members.Count}";
    }

    private IEnumerator LoadAvatar(CSteamID steamId, PlayerCardUI card)
    {
        // Forzar que Steam cargue la info del amigo
        SteamFriends.RequestUserInformation(steamId, false);

        float timeout = 5f;
        int handle = -1;

        while (timeout > 0)
        {
            handle = SteamFriends.GetMediumFriendAvatar(steamId);
            if (handle > 0) break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (handle <= 0)
        {
            Debug.LogWarning($"No se pudo cargar avatar de {steamId}");
            yield break;
        }

        uint width, height;
        if (!SteamUtils.GetImageSize(handle, out width, out height)) yield break;

        byte[] imageData = new byte[width * height * 4];
        if (!SteamUtils.GetImageRGBA(handle, imageData, imageData.Length)) yield break;

        Texture2D tex = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
        tex.LoadRawTextureData(imageData);
        tex.Apply();
        FlipTextureVertically(tex);

        card.avatarImage.sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
    }
    private void FlipTextureVertically(Texture2D tex)
    {
        var pixels = tex.GetPixels();
        int w = tex.width, h = tex.height;
        var flipped = new Color[pixels.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                flipped[y * w + x] = pixels[(h - 1 - y) * w + x];
        tex.SetPixels(flipped);
        tex.Apply();
    }

    public void ShowLobbyScreen() => OnLobbyReady();
    public void RefreshPlayerList(List<CSteamID> members) => RefreshPlayerList();
}