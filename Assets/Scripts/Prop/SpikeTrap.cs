using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class SpikeTrap : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float damageDelay = 0.2f;

    [Header("Timing")]
    [SerializeField] private float showDuration = 0.6f;
    [SerializeField] private float cooldown = 5f;

    private Animator anim;
    private bool isOnCooldown;
    private bool hasDamaged;
    private Collider2D targetPlayer;
    private PlayerHealth triggeredPlayer;
    private Vector2 triggeredBoundsCenter;
    private Vector2 triggeredBoundsSize;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    private void OnValidate()
    {
        damageDelay = Mathf.Max(0f, damageDelay);
        showDuration = Mathf.Max(0.05f, showDuration);
        cooldown = Mathf.Max(0f, cooldown);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (isOnCooldown) return;

        targetPlayer = other;
        triggeredPlayer = other.GetComponent<PlayerHealth>();
        if (triggeredPlayer == null)
            triggeredPlayer = other.GetComponentInParent<PlayerHealth>();

        Collider2D damageArea = GetComponent<Collider2D>();
        if (damageArea != null)
        {
            triggeredBoundsCenter = damageArea.bounds.center;
            triggeredBoundsSize = damageArea.bounds.size + Vector3.one * 0.18f;
        }

        hasDamaged = false;
        SetShown(true);

        StartCoroutine(SpikeSequence());
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (targetPlayer == null && other.CompareTag("Player"))
            targetPlayer = other;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (targetPlayer == other)
            targetPlayer = null;
    }

    private IEnumerator SpikeSequence()
    {
        if (damageDelay > 0f)
            yield return new WaitForSeconds(damageDelay);

        DealDamage();

        yield return new WaitForSeconds(showDuration);

        SetShown(false);

        isOnCooldown = true;
        targetPlayer = null;
        triggeredPlayer = null;
        yield return new WaitForSeconds(cooldown);
        isOnCooldown = false;
    }

    private void DealDamage()
    {
        if (hasDamaged) return;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsServer)
            return;

        PlayerHealth health = triggeredPlayer;
        if (health == null && targetPlayer != null)
        {
            health = targetPlayer.GetComponent<PlayerHealth>();
            if (health == null)
                health = targetPlayer.GetComponentInParent<PlayerHealth>();
        }

        if (health == null)
            return;

        if (!IsPlayerStillInDamageArea(health))
            return;

        health.TakeDamage(damage);
        hasDamaged = true;
    }

    private bool IsPlayerStillInDamageArea(PlayerHealth health)
    {
        if (health == null)
            return false;

        Collider2D[] playerColliders = health.GetComponentsInChildren<Collider2D>();
        if (playerColliders == null || playerColliders.Length == 0)
            return Vector2.Distance(health.transform.position, triggeredBoundsCenter) <= Mathf.Max(triggeredBoundsSize.x, triggeredBoundsSize.y);

        Bounds damageBounds = new Bounds(triggeredBoundsCenter, triggeredBoundsSize);
        foreach (Collider2D playerCollider in playerColliders)
        {
            if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger)
                continue;

            if (damageBounds.Intersects(playerCollider.bounds))
                return true;
        }

        return false;
    }

    private void SetShown(bool shown)
    {
        if (anim == null)
            return;

        anim.SetBool("IsShown", shown);
        anim.Play(shown ? "Spike_Show" : "Spike_Hide", 0, 0f);
    }
}
