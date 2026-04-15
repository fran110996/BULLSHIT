using UnityEngine;
using Unity.Netcode;
using TMPro;

/// <summary>
/// Maneja la interaccion del jugador con objetos IInteractable mediante un Raycast.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [Header("Configuracion")]
    public float interactionDistance = 10f; // Aumentado a 10 para evitar el "llegar muy justo"
    public LayerMask interactionLayer;
    public KeyCode interactionKey = KeyCode.E;

    [Header("Referencias")]
    public Transform cameraTransform;

    private IInteractable currentInteractable;

    void Update()
    {
        if (!IsOwner) return;

        CheckForInteractable();

        if (Input.GetKeyDown(interactionKey) && currentInteractable != null)
        {
            if (currentInteractable.CanInteract(gameObject))
            {
                currentInteractable.Interact(gameObject);
            }
        }
    }

    private void CheckForInteractable()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hit;

        // Visualizar el rayo en la ventana de Scene para debuggear
        Debug.DrawRay(ray.origin, ray.direction * interactionDistance, Color.yellow);

        if (Physics.Raycast(ray, out hit, interactionDistance, interactionLayer))
        {
            // Buscamos el componente en el objeto golpeado o en sus padres
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            
            if (interactable != null)
            {
                if (interactable != currentInteractable)
                {
                    Debug.Log("<color=green>Interaccin Detectada:</color> " + interactable.GetInteractText());
                }
                currentInteractable = interactable;
                return;
            }
        }

        currentInteractable = null;
    }
}
