using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class SpikeTrap : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 1;

    [Header("Timing")]
    [SerializeField] private float showDuration = 0.6f;   // Thời gian gai đâm lên (khớp với animation Show)
    [SerializeField] private float cooldown = 5f;         // Thời gian hồi 5 giây

    private Animator anim;
    private bool isOnCooldown = false;
    private bool hasDamaged = false;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (isOnCooldown) return;          // Đang hồi thì bỏ qua

        // Bắt đầu đâm
        hasDamaged = false;
        anim.SetBool("IsShown", true);

        // Gây sát thương 1 lần
        DealDamage(other);

        // Chạy coroutine: đợi animation Show xong → Hide → bắt đầu cooldown
        StartCoroutine(SpikeSequence());
    }

    private void DealDamage(Collider2D player)
    {
        if (hasDamaged) return;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsServer)
        {
            hasDamaged = true;
            return;
        }

        var health = player.GetComponent<PlayerHealth>();
        if (health == null)
            health = player.GetComponentInParent<PlayerHealth>();

        if (health != null)
        {
            health.TakeDamage(damage);
            hasDamaged = true;
        }
    }

    private IEnumerator SpikeSequence()
    {
        // Chờ animation Show chạy xong
        yield return new WaitForSeconds(showDuration);

        // Thu gai
        anim.SetBool("IsShown", false);

        // Bắt đầu cooldown 60 giây
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldown);
        isOnCooldown = false;
    }
}
