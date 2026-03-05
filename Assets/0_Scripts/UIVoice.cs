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

    private bool isMuted = false;
    private bool settingsOpen = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    async void Start()
    {
        settingsPanel.SetActive(false);
        SetMuteIcon(false);
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

        // Cuando abre settings, liberar cursor
        // Cuando cierra, volver a bloquear si estamos en juego
        if (settingsOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = false;
        }
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
        Debug.Log($"Microfono seleccionado: {Microphone.devices[index]}");
    }
}