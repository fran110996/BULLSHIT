using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Controla la interfaz de la lista de misiones/fotografias.
/// Se activa con la tecla Tab.
/// </summary>
public class PhotoMissionUI : MonoBehaviour
{
    public static PhotoMissionUI Instance;

    [Header("Referencias UI")]
    public GameObject panelRoot;
    public Transform listContainer;
    public GameObject itemPrefab;
    public TextMeshProUGUI emptyText;

    [Header("Configuracion")]
    public KeyCode toggleKey = KeyCode.Tab;

    private bool isOpen = false;
    public bool IsUIOpen => isOpen;

    private List<GameObject> activeItems = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        
        // Suscribirse a cambios en la lista global
        if (PhotoMissionManager.Instance != null)
        {
            PhotoMissionManager.Instance.capturedElements.OnListChanged += OnCapturedElementsChanged;
            RefreshList();
        }
        else
        {
            // Reintentar en un momento si el manager no ha spawneado aun
            Invoke(nameof(SubscribeToManager), 0.5f);
        }
    }

    private void SubscribeToManager()
    {
        if (PhotoMissionManager.Instance != null)
        {
            PhotoMissionManager.Instance.capturedElements.OnListChanged += OnCapturedElementsChanged;
            RefreshList();
        }
    }

    private void OnDestroy()
    {
        if (PhotoMissionManager.Instance != null)
        {
            PhotoMissionManager.Instance.capturedElements.OnListChanged -= OnCapturedElementsChanged;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleUI(!isOpen);
        }
    }

    public void ToggleUI(bool open)
    {
        isOpen = open;
        if (panelRoot != null) panelRoot.SetActive(isOpen);
        
        if (isOpen)
        {
            RefreshList();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Solo bloquear si no hay otros menus abiertos (como el de settings)
            if (UIVoice.Instance == null || !UIVoice.Instance.IsSettingsOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void OnCapturedElementsChanged(NetworkListEvent<Unity.Collections.FixedString128Bytes> changeEvent)
    {
        if (isOpen) RefreshList();
    }

    private void RefreshList()
    {
        if (PhotoMissionManager.Instance == null || listContainer == null) return;

        // Limpiar lista actual
        foreach (var item in activeItems)
        {
            if (item != null) Destroy(item);
        }
        activeItems.Clear();

        var elements = PhotoMissionManager.Instance.capturedElements;
        
        if (emptyText != null) 
            emptyText.gameObject.SetActive(elements.Count == 0);

        // Agrupar elementos por nombre y contar ocurrencias
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (var el in elements)
        {
            string name = el.ToString();
            if (counts.ContainsKey(name)) counts[name]++;
            else counts[name] = 1;
        }

        // Crear nuevos items agrupados
        foreach (var entry in counts)
        {
            GameObject newItem = Instantiate(itemPrefab, listContainer);
            activeItems.Add(newItem);
            
            var tmp = newItem.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                // Formato: "xN [Item Name]" (siempre mostramos la cantidad x1, x2, etc.)
                tmp.text = $"x{entry.Value} {entry.Key}";
            }
        }
    }
}
