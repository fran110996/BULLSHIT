using UnityEngine;
using Steamworks;
using System.Collections.Generic;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance;

    public CSteamID CurrentLobbyID { get; private set; }
    public bool IsHost => SteamMatchmaking.GetLobbyOwner(CurrentLobbyID) == SteamUser.GetSteamID();

    private Callback<LobbyCreated_t> lobbyCreated;
    private Callback<GameLobbyJoinRequested_t> joinRequested;
    private Callback<LobbyEnter_t> lobbyEntered;
    private Callback<LobbyChatUpdate_t> lobbyChatUpdate;
    private Callback<LobbyDataUpdate_t> lobbyDataUpdate;
    private Callback<PersonaStateChange_t> personaStateChange;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        personaStateChange = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
    }

    private void OnPersonaStateChange(PersonaStateChange_t cb)
    {
        // Alguien en la lista de amigos cambio de estado (desconexion, etc)
        LobbyUIManager.Instance?.RefreshPlayerList();
    }

    void Update()
    {
        SteamAPI.RunCallbacks();
    }

    public void CreateLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
    }

    private void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (cb.m_eResult != EResult.k_EResultOK) { Debug.LogError("Error al crear lobby"); return; }

        CurrentLobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(CurrentLobbyID, "game", "MiJuegoCoop");
        SteamMatchmaking.SetLobbyData(CurrentLobbyID, "relay_code", "");

        // El host se marca ready automaticamente
        SetMemberReady(true);

        Debug.Log($"Lobby creado: {CurrentLobbyID}");
        LobbyUIManager.Instance?.OnLobbyReady();
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t cb)
    {
        SteamMatchmaking.JoinLobby(cb.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t cb)
    {
        CurrentLobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        Debug.Log($"✓ Entré al lobby. Soy host: {IsHost}");
        
        if (IsHost)
        {
            SetMemberReady(true);
        }

        LobbyUIManager.Instance?.OnLobbyReady();

        if (!IsHost)
        {
            string code = SteamMatchmaking.GetLobbyData(CurrentLobbyID, "relay_code");
            if (!string.IsNullOrEmpty(code))
                RelayManager.Instance.JoinRelay(code);
        }
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t cb)
    {
        LobbyUIManager.Instance?.RefreshPlayerList();
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
    {
        if (!IsHost)
        {
            string code = SteamMatchmaking.GetLobbyData(CurrentLobbyID, "relay_code");
            if (!string.IsNullOrEmpty(code))
                RelayManager.Instance.JoinRelay(code);
        }
        LobbyUIManager.Instance?.RefreshPlayerList();
    }

    public void SetRelayCode(string code)
    {
        SteamMatchmaking.SetLobbyData(CurrentLobbyID, "relay_code", code);
    }

    public void SetMemberReady(bool ready)
    {
        string val = ready ? "1" : "0";
        SteamMatchmaking.SetLobbyMemberData(CurrentLobbyID, "ready", val);
    }

    public bool IsMemberReady(CSteamID memberId)
    {
        string val = SteamMatchmaking.GetLobbyMemberData(CurrentLobbyID, memberId, "ready");
        return val == "1";
    }

    public List<CSteamID> GetMembers()
    {
        var list = new List<CSteamID>();
        int count = SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyID);
        for (int i = 0; i < count; i++)
            list.Add(SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyID, i));
        return list;
    }

    public bool AllReady()
    {
        var members = GetMembers();
        if (members.Count == 0) return false;

        foreach (var m in members)
        {
            // El host no necesita estar ready
            if (m == SteamMatchmaking.GetLobbyOwner(CurrentLobbyID)) continue;
            if (!IsMemberReady(m)) return false;
        }

        return true;
    }

    public void LeaveLobby()
    {
        if (CurrentLobbyID.IsValid())
            SteamMatchmaking.LeaveLobby(CurrentLobbyID);
    }

    public static Texture2D GetSteamAvatar(CSteamID id)
    {
        int avatarInt = SteamFriends.GetMediumFriendAvatar(id);
        if (avatarInt == -1) return null;

        SteamUtils.GetImageSize(avatarInt, out uint width, out uint height);

        if (width > 0 && height > 0)
        {
            byte[] avatarStream = new byte[4 * (int)width * (int)height];
            SteamUtils.GetImageRGBA(avatarInt, avatarStream, (int)(4 * width * height));

            Texture2D texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
            texture.LoadRawTextureData(avatarStream);
            texture.Apply();

            // Los avatares de Steam vienen invertidos verticalmente
            return FlipTexture(texture);
        }

        return null;
    }

    private static Texture2D FlipTexture(Texture2D original)
    {
        Texture2D flipped = new Texture2D(original.width, original.height);
        int xN = original.width;
        int yN = original.height;

        for (int i = 0; i < xN; i++)
        {
            for (int j = 0; j < yN; j++)
            {
                flipped.SetPixel(i, yN - j - 1, original.GetPixel(i, j));
            }
        }
        flipped.Apply();
        return flipped;
    }

    void OnApplicationPause(bool paused)
    {
        // Por si acaso en mobile o alt+tab forzado
        if (paused) LeaveLobby();
    }
}