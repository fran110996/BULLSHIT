using UnityEngine;
using Unity.Services.Vivox;
using System.Threading.Tasks;
using System.Collections.Generic;

public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance;

    [Header("Configuracion")]
    public KeyCode muteKey = KeyCode.F5;

    private bool isMuted = false;
    private bool isInChannel = false;
    private bool isJoining = false;
    private Dictionary<string, NetworkPlayerController> playersByName = new Dictionary<string, NetworkPlayerController>();
    private NetworkPlayerController localPlayer;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async Task InitializeVoice()
    {
        try
        {
            await VivoxService.Instance.InitializeAsync();
            Debug.Log("Vivox inicializado");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Vivox no disponible: {e.Message}");
        }
    }

    public async Task JoinVoiceChannel(string roomName)
    {
        if (isInChannel || isJoining)
        {
            Debug.Log("Ya estamos en el canal o uniendose, ignorando");
            return;
        }

        isJoining = true;

        try
        {
            try
            {
                VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
                VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
            }
            catch { }

            try
            {
                await VivoxService.Instance.LeaveAllChannelsAsync();
                await Task.Delay(1000);
            }
            catch { }

            try
            {
                await VivoxService.Instance.LogoutAsync();
                await Task.Delay(500);
            }
            catch { }

            await VivoxService.Instance.InitializeAsync();
            await Task.Delay(500);

            var properties = new Channel3DProperties(
                32,
                1,
                1f,
                AudioFadeModel.LinearByDistance);

            await VivoxService.Instance.JoinPositionalChannelAsync(
                roomName,
                ChatCapability.AudioOnly,
                properties);

            isInChannel = true;
            Debug.Log($"Unido al canal de voz: {roomName}");

            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
        }
        catch (System.Exception e)
        {
            isInChannel = false;
            Debug.LogWarning($"Error uniendose al canal: {e.Message}");
        }
        finally
        {
            isJoining = false;
        }
    }

    public async Task LeaveVoiceChannel()
    {
        try
        {
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
            await VivoxService.Instance.LeaveAllChannelsAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error saliendo del canal: {e.Message}");
        }
        finally
        {
            isInChannel = false;
            isJoining = false;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(muteKey))
            ToggleMute();

        UpdateVivoxPosition();
    }

    void UpdateVivoxPosition()
    {
        if (!isInChannel) return;

        // Buscar solo una vez
        if (localPlayer == null)
        {
            var players = FindObjectsOfType<NetworkPlayerController>();
            foreach (var p in players)
            {
                if (p.IsOwner) { localPlayer = p; break; }
            }
        }

        if (localPlayer == null) return;

        VivoxService.Instance.Set3DPosition(
            localPlayer.transform.position,
            localPlayer.transform.position,
            localPlayer.transform.forward,
            localPlayer.transform.up,
            "GameRoom");
    }

    void ToggleMute()
    {
        if (!isInChannel) return;

        isMuted = !isMuted;

        if (isMuted)
            VivoxService.Instance.MuteInputDevice();
        else
            VivoxService.Instance.UnmuteInputDevice();

        Debug.Log(isMuted ? "Microfono muteado" : "Microfono activado");
        UIVoice.Instance?.SetMuteIcon(isMuted);
    }

    public void RegisterPlayer(string playerName, NetworkPlayerController controller)
    {
        if (!playersByName.ContainsKey(playerName))
            playersByName[playerName] = controller;
    }

    private void OnParticipantAdded(VivoxParticipant participant)
    {
        participant.ParticipantSpeechDetected += () => OnSpeechChanged(participant);
        Debug.Log($"Participante en canal: {participant.DisplayName}");
    }

    private void OnParticipantRemoved(VivoxParticipant participant)
    {
        if (playersByName.ContainsKey(participant.DisplayName))
        {
            playersByName[participant.DisplayName].SetSpeaking(false);
            playersByName.Remove(participant.DisplayName);
        }
    }

    private void OnSpeechChanged(VivoxParticipant participant)
    {
        bool speaking = participant.SpeechDetected && !participant.IsMuted;
        if (playersByName.TryGetValue(participant.DisplayName, out var controller))
            controller.SetSpeaking(speaking);
    }
}