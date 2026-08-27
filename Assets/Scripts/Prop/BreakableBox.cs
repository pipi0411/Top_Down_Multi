using UnityEngine;

public class BreakableBox : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] float maxHealth = 1f;

    [Header("Drop Chance")]
    [SerializeField, Range(0f, 1f)] float dropChance = 0.7f;
    [SerializeField] float spawnSpreadRadius = 0.18f;

    [Header("Loot Prefabs")]
    [SerializeField] GameObject healthBottlePrefab;
    [SerializeField] GameObject manaBottlePrefab;
    [SerializeField] GameObject shieldBottlePrefab;
    [SerializeField] GameObject coinPrefab;

    GameObject[] LootPrefabs => new[] { coinPrefab, healthBottlePrefab, manaBottlePrefab, shieldBottlePrefab };

    [Header("Loot Weights")]
    [SerializeField] float healthWeight = 20f;
    [SerializeField] float manaWeight = 20f;
    [SerializeField] float shieldWeight = 10f;
    [SerializeField] float coinWeight = 50f;

    float currentHealth;
    bool isBroken;

    void Awake()
    {
        currentHealth = Mathf.Max(1f, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isBroken) return;
        if (MultiplayerGameplaySync.IsNetworkActive && !MultiplayerGameplaySync.IsServer)
        {
            MultiplayerGameplaySync.RequestBoxDamage(this, amount);
            return;
        }

        ApplyDamageAuthoritative(amount, true);
    }

    public void ApplyDamageAuthoritative(float amount, bool broadcast)
    {
        if (isBroken) return;
        currentHealth -= Mathf.Max(0f, amount);
        if (currentHealth <= 0f)
            Break(broadcast);
    }

    void Break(bool broadcast)
    {
        if (isBroken) return;
        isBroken = true;

        int lootIndex = -1;
        Vector3 dropPosition = transform.position;
        GameObject lootPrefab = TryPickLoot(out lootIndex, out dropPosition);
        if (broadcast)
            MultiplayerGameplaySync.BroadcastBoxBroken(this, lootIndex, dropPosition);
        SpawnLoot(lootPrefab, lootIndex, dropPosition);
        Destroy(gameObject);
    }

    GameObject TryPickLoot(out int lootIndex, out Vector3 dropPosition)
    {
        lootIndex = -1;
        dropPosition = transform.position;
        if (Random.value > dropChance) return null;

        GameObject lootPrefab = PickLootPrefab(out lootIndex);
        if (lootPrefab == null) return null;

        Vector2 randomOffset = Random.insideUnitCircle * spawnSpreadRadius;
        dropPosition = transform.position + new Vector3(randomOffset.x, randomOffset.y, -0.5f);
        return lootPrefab;
    }

    void SpawnLoot(GameObject lootPrefab, int lootIndex, Vector3 dropPosition)
    {
        if (lootPrefab == null || lootIndex < 0) return;
        Transform parent = transform.parent;
        GameObject loot = Instantiate(lootPrefab, dropPosition, Quaternion.identity, parent);
        NetworkedWorldEntity entity = loot.GetComponent<NetworkedWorldEntity>();
        if (entity == null)
            entity = loot.AddComponent<NetworkedWorldEntity>();
        string boxId = GetComponent<NetworkedWorldEntity>()?.NetworkId ?? gameObject.name;
        entity.Initialize($"{boxId}_Loot");

        Collider2D lootCollider = loot.GetComponent<Collider2D>();
        if (lootCollider != null)
            lootCollider.isTrigger = true;
    }

    public void BreakRemote(int lootIndex, Vector3 dropPosition)
    {
        if (isBroken) return;
        isBroken = true;
        GameObject[] prefabs = LootPrefabs;
        GameObject lootPrefab = lootIndex >= 0 && lootIndex < prefabs.Length ? prefabs[lootIndex] : null;
        SpawnLoot(lootPrefab, lootIndex, dropPosition);
        Destroy(gameObject);
    }

    GameObject PickLootPrefab(out int lootIndex)
    {
        lootIndex = -1;
        float totalWeight = Mathf.Max(0f, healthWeight)
            + Mathf.Max(0f, manaWeight)
            + Mathf.Max(0f, shieldWeight)
            + Mathf.Max(0f, coinWeight);

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);

        roll -= Mathf.Max(0f, coinWeight);
        if (roll <= 0f) { lootIndex = 0; return coinPrefab; }

        roll -= Mathf.Max(0f, healthWeight);
        if (roll <= 0f) { lootIndex = 1; return healthBottlePrefab; }

        roll -= Mathf.Max(0f, manaWeight);
        if (roll <= 0f) { lootIndex = 2; return manaBottlePrefab; }

        lootIndex = 3;
        return shieldBottlePrefab;
    }
}
