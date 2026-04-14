using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerSpawner : NetworkBehaviour
{
    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    private Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Escuchar cuando termina de cargar una escena (para el Host y clientes que ya estaban)
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        
        // Escuchar cuando se conecta un nuevo cliente (para Late Joiners / Modo Local)
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnSceneLoaded(string sceneName, 
        UnityEngine.SceneManagement.LoadSceneMode mode, 
        List<ulong> clientsCompleted, 
        List<ulong> clientsTimedOut)
    {
        if (sceneName != "1_GameScene") return;

        // Al cargar la escena, intentamos spawnear a todos los que ya esten conectados
        foreach (var clientId in clientsCompleted)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Si un cliente se une y ya estamos en la escena de juego, spawnearlo inmediatamente
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "1_GameScene")
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (!IsServer) return;

        // Evitar spawnear dos veces para el mismo cliente
        if (spawnedPlayers.ContainsKey(clientId) && spawnedPlayers[clientId] != null) return;

        Debug.Log($"Spawneando jugador para el cliente: {clientId}");

        Vector3 pos = spawnPoints[spawnedPlayers.Count % spawnPoints.Length].position;
        GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);
        
        var netObj = player.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);

        spawnedPlayers[clientId] = player;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}
