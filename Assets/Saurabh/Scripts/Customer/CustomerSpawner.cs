using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CustomerSpawner : MonoBehaviour
{
    // Every kind of visitor the shop can get. Add new kinds here later (e.g. red-flag adopters).
    public enum CustomerKind { Taker, Giver, Stray }

    // One row in the customer pool, edited in the Inspector
    [System.Serializable]
    public class PoolEntry
    {
        public string label = "Customer";       // just a name so the Inspector list is readable
        public CustomerKind kind;
        [Min(0)] public float weight = 1f;       // how often this customer comes (bigger = more often)
        [Min(0)] public float emptyShopBonus;    // extra weight added when the shop has no cats at all
        [Min(0)] public float weightPerCatShown; // extra weight for each cat waiting for adoption (more choice = more adopters)
        public bool needsCatToAdopt;             // only comes if a cat is waiting for adoption
    }

    public GameObject customerPrefab;
    public Transform spawnPoint;
    private bool firstSpawnOfDay;

    [Header("Spawn Settings")]
    public float minSpawnTime = 10f;
    public float maxSpawnTime = 25f;

    [Header("Customer Pool")]
    public List<PoolEntry> pool = new List<PoolEntry>
    {
        new PoolEntry { label = "Adopter (Taker)",       kind = CustomerKind.Taker, weight = 0f, weightPerCatShown = 2f, needsCatToAdopt = true },
        new PoolEntry { label = "Owner giving up a cat", kind = CustomerKind.Giver, weight = 4f },
        new PoolEntry { label = "Stray finder (free)",   kind = CustomerKind.Stray, weight = 0.5f, emptyShopBonus = 4f },
    };

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

            // 1. Pick who walks in from the pool
            PoolEntry picked = PickFromPool();
            if (picked == null) continue;   // nobody can visit right now, try again next time

            // 2. Spawn them
            SpawnCustomer(picked.kind);
        }
    }

    // Sends one customer of this kind right now. Returns false if the queue is full.
    // Used by the normal spawn loop and by the developer console ("customer giver").
    public bool SpawnCustomer(CustomerKind kind)
    {
        if (CustomerQueueManager.Instance.IsQueueFull()) return false;

        // Turn the pool kind into the customer's type
        CustomerAI.CustomerType type = kind == CustomerKind.Taker
            ? CustomerAI.CustomerType.Taker
            : CustomerAI.CustomerType.Giver;   // a Stray finder is a Giver who asks for nothing
        bool isStray = kind == CustomerKind.Stray;

        // Spawn and initialize
        GameObject newCustomer = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);
        CustomerAI ai = newCustomer.GetComponent<CustomerAI>();
        ai.InitializeCustomer(type, isStray);
        return true;
    }

    // Weighted random pick: a row with weight 4 is picked twice as often as a row with weight 2
    private PoolEntry PickFromPool()
    {
        bool shopEmpty = CageManager.Instance.CatCount == 0;
        int catsShown = CageManager.Instance.CatsForAdoption;

        // Add up the weights of everyone who is allowed to come right now
        float total = 0f;
        foreach (PoolEntry entry in pool) total += WeightOf(entry, shopEmpty, catsShown);
        if (total <= 0f) return null;

        // Roll a number between 0 and the total, then walk the list until the roll lands in a row
        float roll = Random.Range(0f, total);
        PoolEntry lastValid = null;

        foreach (PoolEntry entry in pool)
        {
            float w = WeightOf(entry, shopEmpty, catsShown);
            if (w <= 0f) continue;

            lastValid = entry;
            if (roll < w) return entry;
            roll -= w;
        }

        return lastValid;   // only reached because of tiny rounding errors
    }

    // This entry's chance right now (0 = cannot come)
    // Example: a Taker with weight 0 and 2 per cat shown has weight 6 when 3 cats are waiting
    private float WeightOf(PoolEntry entry, bool shopEmpty, int catsShown)
    {
        if (entry.needsCatToAdopt && catsShown == 0) return 0f;
        return entry.weight
             + entry.weightPerCatShown * catsShown
             + (shopEmpty ? entry.emptyShopBonus : 0f);
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