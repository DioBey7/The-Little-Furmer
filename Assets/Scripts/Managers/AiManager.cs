using System.Collections.Generic;
using UnityEngine;

public class AiManager : MonoBehaviour
{
    public static AiManager Instance { get; private set; }

    public GameObject aiWorkerPrefab;
    [HideInInspector] public Transform spawnPoint;

    [HideInInspector] public List<AiWorker> activeAiWorkers = new List<AiWorker>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterAi(AiWorker ai)
    {
        if (!activeAiWorkers.Contains(ai))
        {
            activeAiWorkers.Add(ai);
        }
    }

    public void SpawnWorker()
    {
        if (aiWorkerPrefab != null && spawnPoint != null)
        {
            Instantiate(aiWorkerPrefab, spawnPoint.position, spawnPoint.rotation);
        }
    }
}
