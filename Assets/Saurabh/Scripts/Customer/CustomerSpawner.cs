using UnityEngine;
using System.Collections;

public class CustomerSpawner : MonoBehaviour
{
    public GameObject customerPrefab;
    public Transform spawnPoint;
    private bool firstSpawnOfDay;
    
    [Header("Spawn Settings")]
    public float minSpawnTime = 10f;
    public float maxSpawnTime = 25f;
    public int minimumBuyPriceThreshold = 50; // Minimum GP needed to afford a cheap cat

    private void Start()
    {
        // StartCoroutine(SpawnRoutine());
    }
    

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            if (CustomerQueueManager.Instance.IsQueueFull())
            {
                yield return null; // Skip spawning until queue opens up
                continue;
            }

            float wait = firstSpawnOfDay ? Random.Range(3f, 6f) : Random.Range(minSpawnTime, maxSpawnTime);
            firstSpawnOfDay = false;
            yield return new WaitForSeconds(wait);
            
            if (CustomerQueueManager.Instance.IsQueueFull()) continue;

            // 1. Audit store conditions
            bool hasSpace = CageManager.Instance.HasEmptyCage();
            bool hasMoney = CoreEconomySystem.Instance.GetGP("Spawner") >= minimumBuyPriceThreshold;

            // 2. Determine Customer Type
            CustomerAI.CustomerType typeToSpawn = CustomerAI.CustomerType.Taker;

            if (hasSpace && hasMoney)
            {
                // 50/50 chance to spawn a Giver if conditions are met
                typeToSpawn = Random.value > 0.5f ? CustomerAI.CustomerType.Giver : CustomerAI.CustomerType.Taker;
            }

            // 3. Spawn and Initialize
            GameObject newCustomer = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);
            CustomerAI ai = newCustomer.GetComponent<CustomerAI>();
            
            ai.InitializeCustomer(typeToSpawn);
            // AudioManager.Play(Sfx.DoorChime);
        }
    }

    public void StopSpawning()
    {
        StopAllCoroutines();
    }

    public void StartSpawning()
    {
        firstSpawnOfDay = true;
        StopAllCoroutines();
        StartCoroutine(SpawnRoutine());
    }
}