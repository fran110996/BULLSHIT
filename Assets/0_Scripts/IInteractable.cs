using UnityEngine;

/// <summary>
/// Interfaz para objetos con los que el jugador puede interactuar (fotos, botones, etc.)
/// </summary>
public interface IInteractable
{
    string GetInteractText();
    void Interact(GameObject player);
    bool CanInteract(GameObject player);
}
