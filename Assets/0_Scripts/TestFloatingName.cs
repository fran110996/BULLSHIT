using UnityEngine;

public class TestFloatingName : MonoBehaviour
{
    public GameObject floatingNamePrefab;
    private FloatingName fn;
    private bool initialized = false;

    void Update()
    {
        if (initialized) return;

        // Buscar el Player(Clone) automaticamente
        var player = FindObjectOfType<NetworkPlayerController>();
        if (player == null) return;

        // Usar el primer jugador que no sea el owner (jugador remoto)
        GameObject nameObj = Instantiate(floatingNamePrefab);
        fn = nameObj.GetComponent<FloatingName>();
        fn.Initialize(player.transform, "TestPlayer");
        initialized = true;
    }
}