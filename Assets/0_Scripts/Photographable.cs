using UnityEngine;

/// <summary>
/// Componente que marca a un objeto como "fotografiable".
/// Permite al sistema de fotos identificar que hay en el encuadre.
/// </summary>
public class Photographable : MonoBehaviour
{
    [Header("Identificacion")]
    public string photoID = "Objeto Desconocido";
    
    [Header("Deteccion Especial")]
    public bool isPlayer = false;
    public Animator playerAnimator;

    public string GetStatus()
    {
        if (isPlayer && playerAnimator != null)
        {
            // Ejemplo: Detectar si el jugador esta bailando
            if (playerAnimator.GetBool("IsDancing"))
            {
                return "Bailando";
            }
        }
        return "Normal";
    }

    private void Start()
    {
        // Si es jugador y no tiene referencia al animator, buscarlo
        if (isPlayer && playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }
    }

    // Para visualizar el area de deteccion en el editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null)
        {
            Gizmos.DrawWireCube(r.bounds.center, r.bounds.size);
        }
    }
}
