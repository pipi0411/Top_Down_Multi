using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WeaponPickup : MonoBehaviour
{
    [SerializeField] Weapon weapon;
    [SerializeField] float pickupDelay = 0.35f;
    [SerializeField] float popDuration = 0.28f;
    [SerializeField] float popHeight = 0.18f;
    [SerializeField] float popScale = 1.2f;
    [SerializeField] float bobHeight = 0.04f;
    [SerializeField] float bobSpeed = 3.5f;

    float canPickupTime;
    Vector3 basePosition;
    Vector3 baseScale;
    Quaternion baseRotation;
    float spawnTime;
    float randomPhase;

    public Weapon Weapon => weapon;

    public void Initialize(Weapon droppedWeapon, float delay = 0.35f)
    {
        weapon = droppedWeapon != null ? droppedWeapon : GetComponent<Weapon>();
        pickupDelay = Mathf.Max(0f, delay);
        canPickupTime = Time.time + pickupDelay;
        basePosition = transform.position;
        baseScale = transform.localScale;
        baseRotation = transform.rotation;
        spawnTime = Time.time;
        randomPhase = Random.Range(0f, Mathf.PI * 2f);

        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    void Awake()
    {
        if (weapon == null)
            weapon = GetComponent<Weapon>();

        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;

        basePosition = transform.position;
        baseScale = transform.localScale;
        baseRotation = transform.rotation;
        spawnTime = Time.time;
        randomPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        float age = Time.time - spawnTime;
        transform.rotation = baseRotation;

        if (age < popDuration)
        {
            float t = Mathf.Clamp01(age / Mathf.Max(0.01f, popDuration));
            float arc = Mathf.Sin(t * Mathf.PI) * popHeight;
            float scalePulse = Mathf.Sin(t * Mathf.PI) * (popScale - 1f);
            transform.position = basePosition + Vector3.up * arc;
            transform.localScale = baseScale * (1f + scalePulse);
            return;
        }

        float bob = Mathf.Sin((age - popDuration) * bobSpeed + randomPhase) * bobHeight;
        transform.position = basePosition + Vector3.up * bob;
        transform.localScale = baseScale;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryPickup(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryPickup(other);
    }

    void TryPickup(Collider2D other)
    {
        if (Time.time < canPickupTime) return;
        if (weapon == null) return;

        PlayerWeaponController controller = other.GetComponentInParent<PlayerWeaponController>();
        if (controller == null) return;
        if (controller.IsSpawned && !controller.IsOwner) return;

        controller.PickupDroppedWeapon(this);
    }
}
