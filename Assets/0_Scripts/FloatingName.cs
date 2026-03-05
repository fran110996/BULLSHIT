using UnityEngine;
using TMPro;

public class FloatingName : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    private Transform target;
    private Vector3 offset = Vector3.up * 2.5f;

    public void Initialize(Transform playerTransform, string name)
    {
        target = playerTransform;
        nameText.text = name;
        // Posicionar inmediatamente para que no aparezca en otro lado
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

        if (Camera.main != null)
        {
            // Mirar HACIA la camara, no en la misma dirección
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0, 180f, 0); // Dar vuelta para que el texto quede de frente
        }
    }
}