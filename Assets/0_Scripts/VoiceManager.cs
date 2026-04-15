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
    private string currentChannelName = "";
    
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
            // Solo inicializar si no esta inicializado
            await VivoxService.Instance.InitializeAsync();
            Debug.Log("Vivox inicializado correctamente");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Vivox inicializacion: {e.Message}");
        }
    }

    public void SetLocalPlayer(NetworkPlayerController player)
    {
        localPlayer = player;
        Debug.Log("VoiceManager: Jugador local registrado.");
    }

    public async Task JoinVoiceChannel(string roomName)
    {
        if (isInChannel && currentChannelName == roomName)
        {
            Debug.Log("Ya estamos en este canal, ignorando.");
            return;
        }

        if (isJoining) return;
        isJoining = true;

        try
        {
            // 1. Limpiar eventos previos por seguridad
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;

            // 2. Login si es necesario (NGO suele usar ID anonimo de Unity Services)
            // VivoxService maneja el login internamente o mediante LoginAsync
            // En versiones modernas de Vivox SDK para Unity, Join dispara el login si hace falta.

            // 3. Salir de canales previos si hay alguno
            if (isInChannel)
            {
                await LeaveVoiceChannel();
            }

            // 4. Configurar canal 3D
            var properties = new Channel3DProperties(
                32, // Distancia maxima razonable
                1,  // Distancia de audicion completa
                1f, // Roll-off
                AudioFadeModel.LinearByDistance);

            currentChannelName = roomName;
            
            await VivoxService.Instance.JoinPositionalChannelAsync(
                roomName,
                ChatCapability.AudioOnly,
                properties);

            isInChannel = true;
            
            // 5. Suscribir eventos
            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
            
            Debug.Log($"Conectado al canal de voz: {roomName}");
        }
        catch (System.Exception e)
        {
            isInChannel = false;
            currentChannelName = "";
            Debug.LogError($"Error al unirse al canal de voz: {e.Message}");
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
            isInChannel = false;
            currentChannelName = "";
            Debug.Log("Salimos del canal de voz.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Vivox Leave error: {e.Message}");
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
        // Solo actualizamos posicion si estamos en canal y tenemos la referencia al localPlayer
        if (!isInChannel || localPlayer == null || string.IsNullOrEmpty(currentChannelName)) return;

        // Vivox necesita saber donde estamos para el audio 3D
        VivoxService.Instance.Set3DPosition(
            localPlayer.transform.position,    // Posicion del hablante
            localPlayer.transform.position,    // Posicion del oyente (usamos la misma porque es FPS)
            localPlayer.transform.forward,     // Hacia donde miramos
            localPlayer.transform.up,          // Arriba
            currentChannelName);
    }

    void ToggleMute()
    {
        isMuted = !isMuted;

        if (isMuted)
            VivoxService.Instance.MuteInputDevice();
        else
            VivoxService.Instance.UnmuteInputDevice();

        Debug.Log(isMuted ? "Microfono Muteado" : "Microfono Activo");
        
        // Actualizar UI si existe
        UIVoice.Instance?.SetMuteIcon(isMuted);
    }

    public void SetInputVolume(int volume)
    {
        // Vivox usa un rango de -50 a 50 o 0 a 100 dependiendo de la version.
        // En Unity.Services.Vivox suele ser 0-100.
        VivoxService.Instance.SetInputDeviceVolume(volume);
        Debug.Log($"Voz: Volumen de entrada ajustado a {volume}");
    }

    public void SetOutputVolume(int volume)
    {
        VivoxService.Instance.SetOutputDeviceVolume(volume);
        Debug.Log($"Voz: Volumen de salida ajustado a {volume}");
    }

    public void RegisterPlayer(string playerName, NetworkPlayerController controller)
    {
        if (!playersByName.ContainsKey(playerName))
        {
            playersByName[playerName] = controller;
            Debug.Log($"Voz: Jugador {playerName} mapeado a su controlador.");
        }
    }

    private void OnParticipantAdded(VivoxParticipant participant)
    {
        participant.ParticipantSpeechDetected += () => OnSpeechChanged(participant);
        Debug.Log($"Voz: Entro participante {participant.DisplayName}");
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
        
        // Si tenemos al jugador mapeado, avisamos al controlador para que anime el nombre/icono
        if (playersByName.TryGetValue(participant.DisplayName, out var controller))
        {
            controller.SetSpeaking(speaking);
        }
    }
}
