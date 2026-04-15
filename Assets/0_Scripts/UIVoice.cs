using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Vivox;
using System.Collections.Generic;
using System.Threading.Tasks;

public class UIVoice : MonoBehaviour
{
    public static UIVoice Instance;

    [Header("Iconos de mute")]
    public GameObject muteIcon;
    public GameObject unmuteIcon;

    [Header("Settings (abre con ESC)")]
    public GameObject settingsPanel;
    public TMP_Dropdown microphoneDropdown;
    public Slider inputVolumeSlider; // Deslizador para m microfono
    public Slider outputVolumeSlider; // Deslizador para los demas

    private bool isMuted = false;
    private bool settingsOpen = false;

    public bool IsSettingsOpen => settingsOpen;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    async void Start()
    {
        settingsPanel.SetActive(false);
        SetMuteIcon(false);
        
        // Configurar Sliders
        if (inputVolumeSlider != null)
        {
            inputVolumeSlider.minValue = 0;
            inputVolumeSlider.maxValue = 100;
            inputVolumeSlider.value = 80; // Valor inicial razonable
            inputVolumeSlider.onValueChanged.AddListener(OnInputVolumeChanged);
        }

        if (outputVolumeSlider != null)
        {
            outputVolumeSlider.minValue = 0;
            outputVolumeSlider.maxValue = 100;
            outputVolumeSlider.value = 80;
            outputVolumeSlider.onValueChanged.AddListener(OnOutputVolumeChanged);
        }

        await PopulateMicrophoneList();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            ToggleSettings();
    }

    public void SetMuteIcon(bool muted)
    {
        isMuted = muted;
        if (muteIcon != null) muteIcon.SetActive(muted);
        if (unmuteIcon != null) unmuteIcon.SetActive(!muted);
    }

    void ToggleSettings()
    {
        settingsOpen = !settingsOpen;
        settingsPanel.SetActive(settingsOpen);

        if (settingsOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Bloquear y esconder el cursor al cerrar
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnInputVolumeChanged(float value)
    {
        VoiceManager.Instance?.SetInputVolume((int)value);
    }

    private void OnOutputVolumeChanged(float value)
    {
        VoiceManager.Instance?.SetOutputVolume((int)value);
    }

    private async Task PopulateMicrophoneList()
    {
        if (microphoneDropdown == null) return;

        microphoneDropdown.ClearOptions();
        var options = new List<string>();

        foreach (var mic in Microphone.devices)
            options.Add(mic);

        if (options.Count == 0)
            options.Add("Microfono por defecto");

        microphoneDropdown.AddOptions(options);
        microphoneDropdown.onValueChanged.AddListener(OnMicrophoneChanged);

        await Task.CompletedTask;
    }

    private void OnMicrophoneChanged(int index)
    {
        if (index < Microphone.devices.Length)
            Debug.Log($"Microfono seleccionado: {Microphone.devices[index]}");
    }
}