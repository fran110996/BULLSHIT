using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Sistema que maneja el modo camara, la captura de la foto y la deteccion de objetos.
/// </summary>
public class PhotoCameraSystem : NetworkBehaviour
{
    [Header("Configuracion")]
    public KeyCode toggleKey = KeyCode.C;
    public KeyCode shutterKey = KeyCode.Mouse0;
    public float captureResolution = 512f;
    
    [Header("Referencias")]
    public Camera playerCamera;
    public PhotoUIController uiController;
    public AudioSource shutterSound;
    public GameObject photoPrefab;

    private bool isCameraMode = false;
    private bool isCapturing = false;

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleCameraMode();
        }

        if (isCameraMode && Input.GetKeyDown(shutterKey) && !isCapturing)
        {
            StartCoroutine(CapturePhotoRoutine());
        }
    }

    private void ToggleCameraMode()
    {
        isCameraMode = !isCameraMode;
        if (uiController != null) uiController.SetHUDActive(isCameraMode);
        
        Debug.Log("Modo Camara: " + isCameraMode);
    }

    private IEnumerator CapturePhotoRoutine()
    {
        isCapturing = true;

        if (uiController != null)
        {
            uiController.TriggerFlash();
        }
        else
        {
            Debug.LogWarning("[PhotoCameraSystem] No se puede disparar el flash: uiController es nulo.");
        }

        if (shutterSound != null) shutterSound.Play();

        yield return new WaitForEndOfFrame();

        RenderTexture rt = new RenderTexture((int)captureResolution, (int)captureResolution, 24);
        playerCamera.targetTexture = rt;
        Texture2D screenShot = new Texture2D((int)captureResolution, (int)captureResolution, TextureFormat.RGB24, false);
        
        playerCamera.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, captureResolution, captureResolution), 0, 0);
        playerCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        byte[] jpegBytes = screenShot.EncodeToJPG(75);
        Destroy(screenShot);

        List<string> detectedObjects = DetectObjectsInView();
        string delimitedObjects = string.Join("|", detectedObjects);

        // Reportar hallazgos al manager global si existe
        if (PhotoMissionManager.Instance != null && detectedObjects.Count > 0)
        {
            PhotoMissionManager.Instance.RegisterCapturedElementsServerRpc(delimitedObjects);
        }

        SpawnPhotoServerRpc(jpegBytes, delimitedObjects, transform.position + transform.forward * 2f);

        isCapturing = false;
    }

    private List<string> DetectObjectsInView()
    {
        List<string> detected = new List<string>();
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
        Photographable[] allTargets = GameObject.FindObjectsOfType<Photographable>();

        foreach (var target in allTargets)
        {
            // Obtener todos los renderers del objeto y sus hijos
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            bool isVisible = false;

            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;

                // 1. Comprobacion primaria: ¿Estan sus bounds en el frustum?
                if (GeometryUtility.TestPlanesAABB(planes, r.bounds))
                {
                    // 2. Comprobacion de oclusion con multiples rayos
                    // Probamos varios puntos para objetos grandes como arboles
                    Vector3[] checkPoints = new Vector3[] {
                        r.bounds.center,
                        r.bounds.center + Vector3.up * (r.bounds.extents.y * 0.5f),
                        r.bounds.center + Vector3.down * (r.bounds.extents.y * 0.5f),
                        r.bounds.center + r.transform.right * (r.bounds.extents.x * 0.5f),
                        r.bounds.center - r.transform.right * (r.bounds.extents.x * 0.5f)
                    };

                    foreach (Vector3 point in checkPoints)
                    {
                        Vector3 direction = point - playerCamera.transform.position;
                        float dist = direction.magnitude;
                        
                        // Lanzamos rayo ignorando Triggers
                        if (Physics.Raycast(playerCamera.transform.position, direction, out RaycastHit hit, dist + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        {
                            // Si golpeamos el objeto o alguno de sus hijos, es visible
                            if (hit.transform.IsChildOf(target.transform) || hit.transform == target.transform)
                            {
                                isVisible = true;
                                break;
                            }
                        }
                        else
                        {
                            // Si no golpeamos nada hasta el punto, asumimos que es visible 
                            // (esto ayuda con objetos sin colliders pero con renderers)
                            isVisible = true;
                            break;
                        }
                    }
                }
                if (isVisible) break;
            }

            if (isVisible)
            {
                string entry = target.photoID;
                string status = target.GetStatus();
                if (status != "Normal") entry += " (" + status + ")";
                
                detected.Add(entry);
            }
        }

        return detected;
    }

    [ServerRpc]
    private void SpawnPhotoServerRpc(byte[] imageBytes, string delimitedObjects, Vector3 position)
    {
        if (photoPrefab == null) return;

        Vector3 spawnPos = position;
        if (Physics.Raycast(position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 5f))
        {
            // El quad de Unity tiene el pivot en el centro, subimos 0.5f para que la base toque el suelo.
            spawnPos = hit.point + Vector3.up * 0.5f; 
        }

        GameObject photoObj = Instantiate(photoPrefab, spawnPos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
        photoObj.transform.localScale = Vector3.one * 1.5f; // Escala aumentada un 50%
        
        NetworkObject netObj = photoObj.GetComponent<NetworkObject>();
        netObj.Spawn();

        NetworkPhoto netPhoto = photoObj.GetComponent<NetworkPhoto>();
        if (netPhoto != null)
        {
            netPhoto.SetPhotoDataClientRpc(imageBytes, delimitedObjects);
        }
    }
}
