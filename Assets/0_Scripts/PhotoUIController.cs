using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Controla los elementos visuales del visor de la camara (REC, Flash, Brackets).
/// </summary>
public class PhotoUIController : MonoBehaviour
{
    [Header("Elementos REC")]
    public GameObject recGroup;
    public Image recDot;
    public float blinkInterval = 0.5f;

    [Header("Efecto Flash")]
    public CanvasGroup flashGroup;
    public float flashDuration = 0.2f;

    [Header("UI General")]
    public GameObject mainVisuals; // Los brackets, letterbox, etc.

    private bool isBlinking = false;

    private void Start()
    {
        if (flashGroup != null) flashGroup.alpha = 0f;
        // Ocultar por defecto al inicio
        if (mainVisuals != null) mainVisuals.SetActive(false);
    }

    public void SetHUDActive(bool active)
    {
        if (mainVisuals != null)
        {
            if (active)
            {
                mainVisuals.SetActive(true);
                // Efecto "Cute": Pequeño rebote al abrir
                mainVisuals.transform.localScale = Vector3.one * 0.8f;
                StopCoroutine("ScaleInOutRoutine");
                StartCoroutine(ScaleInOutRoutine(true));
            }
            else
            {
                // Efecto de salida antes de desactivar
                StopCoroutine("ScaleInOutRoutine");
                StartCoroutine(ScaleInOutRoutine(false));
            }
        }
        
        if (active)
        {
            if (!isBlinking) StartCoroutine(BlinkRECRoutine());
        }
        else
        {
            isBlinking = false;
        }
    }

    private IEnumerator ScaleInOutRoutine(bool appearing)
    {
        float t = 0;
        Vector3 startScale = mainVisuals.transform.localScale;
        Vector3 endScale = appearing ? Vector3.one : Vector3.one * 0.8f;

        if (!appearing)
        {
            // Si est cerrando, esperamos un poco para que se vea la animacin
            while (t < 1)
            {
                t += Time.deltaTime * 8f;
                mainVisuals.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }
            mainVisuals.SetActive(false);
        }
        else
        {
            while (t < 1)
            {
                t += Time.deltaTime * 5f;
                mainVisuals.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }
        }
        
        mainVisuals.transform.localScale = endScale;
    }

    public void TriggerFlash()
    {
        StopCoroutine("FlashRoutine");
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        flashGroup.alpha = 1f;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            flashGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / flashDuration);
            yield return null;
        }
        flashGroup.alpha = 0f;
    }

    private IEnumerator BlinkRECRoutine()
    {
        isBlinking = true;
        while (true)
        {
            if (recDot != null) recDot.enabled = !recDot.enabled;
            yield return new WaitForSeconds(blinkInterval);
        }
    }
}
