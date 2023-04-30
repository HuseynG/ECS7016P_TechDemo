using System.Collections.Generic;
using UnityEngine;

public class CustomSpawner : MonoBehaviour
{
    public UnderwaterCaveGenerator underwaterCaveGenerator;
    public GameObject obstaclePrefab;
    // list to store the spawned obstacles
    public List<GameObject> spawnedObstacles { get; private set; } = new List<GameObject>();

    void Start()
    {
        if (underwaterCaveGenerator != null)
        {
            // adding the listener to handle cave generation completion
            underwaterCaveGenerator.OnCaveGenerationCompleted.AddListener(HandleCaveGenerationCompleted);
        }
        else
        {
            Debug.LogError("UnderwaterCaveGenerator reference not set in CustomSpawner.");
        }
    }

    void HandleCaveGenerationCompleted()
    {
        ClearObstacles();
        SpawnObstacles();
    }

    // method to clear existing obstacles
    void ClearObstacles()
    {
        foreach (GameObject obstacle in spawnedObstacles)
        {
            Destroy(obstacle);
        }
        spawnedObstacles.Clear();
    }

    // method to spawn new obstacles
    void SpawnObstacles()
    {
        List<Vector3> soilPositions = underwaterCaveGenerator.GetSoilPositions();
        for (int i = 0; i < soilPositions.Count; i++)
        {
            if (i % 10 == 0) // only spawn obstacles at every 10th position
            {
                Vector3 position = soilPositions[i];
                // instantiating the obstacle prefab at the given position
                GameObject obstacleInstance = Instantiate(obstaclePrefab, position, Quaternion.identity);

                spawnedObstacles.Add(obstacleInstance);
            }
            continue;
        }
    }
}



