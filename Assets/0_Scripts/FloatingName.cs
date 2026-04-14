using UnityEngine;
using TMPro;

public class FloatingName : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    private Transform target;
    private Vector3 offset = Vector3.up * 6f;

    public void Initialize(Transform playerTransform, string name)
    {
        target = playerTransform;
        nameText.text = name;
        nameText.alignment = TextAlignmentOptions.Center;
        transform.position = playerTransform.position + offset;
    }

    public void SetSpeaking(bool speaking)
    {
        nameText.color = speaking ? Color.green : Color.white;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = target.position + offset;

        // Intentamos obtener la camara de forma mas robusta
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = FindFirstObjectByType<Camera>();
        }

        if (cam != null)
        {
            // Alineamos el frente del objeto con el frente de la camara
            transform.forward = cam.transform.forward;
        }
    }
}
