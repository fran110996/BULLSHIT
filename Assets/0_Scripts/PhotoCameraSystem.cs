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
            Renderer r = target.GetComponentInChildren<Renderer>();
            if (r == null) continue;

            if (GeometryUtility.TestPlanesAABB(planes, r.bounds))
            {
                Vector3 direction = r.bounds.center - playerCamera.transform.position;
                if (Physics.Raycast(playerCamera.transform.position, direction, out RaycastHit hit, 100f))
                {
                    if (hit.transform.IsChildOf(target.transform) || hit.transform == target.transform)
                    {
                        string entry = target.photoID;
                        string status = target.GetStatus();
                        if (status != "Normal") entry += " (" + status + ")";
                        
                        if (!detected.Contains(entry))
                            detected.Add(entry);
                    }
                }
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
