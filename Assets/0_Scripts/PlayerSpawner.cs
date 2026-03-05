using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerSpawner : NetworkBehaviour
{
    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
    }

    private void OnSceneLoaded(string sceneName,
        UnityEngine.SceneManagement.LoadSceneMode mode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (sceneName != "1_GameScene") return;
        SpawnPlayers();
    }

    private void SpawnPlayers()
    {
        int index = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            Vector3 pos = spawnPoints[index % spawnPoints.Length].position;
            GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(client.ClientId);
            index++;
        }
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
    }
}