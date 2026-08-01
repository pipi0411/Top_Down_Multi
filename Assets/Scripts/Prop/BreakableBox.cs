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

        currentHealth -= Mathf.Max(0f, amount);
        if (currentHealth <= 0f)
            Break();
    }

    void Break()
    {
        if (isBroken) return;
        isBroken = true;

        TryDropLoot();
        Destroy(gameObject);
    }

    void TryDropLoot()
    {
        if (Random.value > dropChance) return;

        GameObject lootPrefab = PickLootPrefab();
        if (lootPrefab == null) return;

        Vector2 randomOffset = Random.insideUnitCircle * spawnSpreadRadius;
        Vector3 dropPosition = transform.position + new Vector3(randomOffset.x, randomOffset.y, -0.5f);
        Transform parent = transform.parent;
        GameObject loot = Instantiate(lootPrefab, dropPosition, Quaternion.identity, parent);

        Collider2D lootCollider = loot.GetComponent<Collider2D>();
        if (lootCollider != null)
            lootCollider.isTrigger = true;
    }

    GameObject PickLootPrefab()
    {
        float totalWeight = Mathf.Max(0f, healthWeight)
            + Mathf.Max(0f, manaWeight)
            + Mathf.Max(0f, shieldWeight)
            + Mathf.Max(0f, coinWeight);

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);

        roll -= Mathf.Max(0f, coinWeight);
        if (roll <= 0f) return coinPrefab;

        roll -= Mathf.Max(0f, healthWeight);
        if (roll <= 0f) return healthBottlePrefab;

        roll -= Mathf.Max(0f, manaWeight);
        if (roll <= 0f) return manaBottlePrefab;

        return shieldBottlePrefab;
    }
}
