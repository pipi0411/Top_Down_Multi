using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class TeleportGate : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite[] frames;

    [Header("Animation")]
    [SerializeField] private float frameInterval = 0.18f;
    [SerializeField] private float teleportDelayAfterOpen = 0.12f;
    [SerializeField] private bool playReverseOnArrival = true;

    [Header("Teleport")]
    [SerializeField] private Vector3 playerArrivalOffset = Vector3.down * 1.25f;
    [SerializeField] private float reuseCooldown = 1.5f;
    [SerializeField] private bool requireAllPlayersInsideInMultiplayer = true;

    [Header("Loading Screen")]
    [SerializeField] private bool showLoadingScreen = true;
    [SerializeField] private float loadingBeforeMapSwitch = 0.85f;
    [SerializeField] private float loadingAfterMapSwitch = 0.35f;

    private SpriteRenderer spriteRenderer;
    private Collider2D triggerCollider;
    private Coroutine animationRoutine;
    private bool playerInside;
    private bool isBusy;
    private bool hasTeleported;
    private float nextAllowedUseTime;
    private int currentFrame;

    public bool PlayReverseOnArrival => playReverseOnArrival;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

        SetFrame(0);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsValidPlayer(other))
            return;

        playerInside = true;
        if (!isBusy && Time.time >= nextAllowedUseTime)
            StartAnimation(OpenThenTeleport());
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsValidPlayer(other))
            return;

        playerInside = true;
        if (!isBusy && Time.time >= nextAllowedUseTime && CanTeleportNow())
            StartAnimation(OpenThenTeleport());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsValidPlayer(other))
            return;

        playerInside = false;
        if (!isBusy && !hasTeleported)
            StartAnimation(AnimateToFrame(0));
    }

    public void PlayArrivalReverse()
    {
        hasTeleported = false;
        playerInside = false;
        nextAllowedUseTime = Time.time + reuseCooldown;
        StartAnimation(AnimateToFrame(0, startFromLastFrame: true));
    }

    private IEnumerator OpenThenTeleport()
    {
        isBusy = true;
        hasTeleported = false;

        yield return AnimateToFrame(LastFrameIndex);

        if (!playerInside || hasTeleported)
        {
            isBusy = false;
            yield break;
        }

        yield return new WaitForSeconds(teleportDelayAfterOpen);

        if (playerInside && LevelManager.Instance != null)
        {
            if (!CanTeleportNow())
            {
                yield return AnimateToFrame(0);
                isBusy = false;
                yield break;
            }

            PlayerHealth playerHealth = GetPlayerHealthInsideGate();
            if (playerHealth != null && playerHealth.IsSpawned)
            {
                playerHealth.RequestPortalTeleport(
                    playerArrivalOffset,
                    showLoadingScreen,
                    loadingBeforeMapSwitch,
                    loadingAfterMapSwitch);

                nextAllowedUseTime = Time.time + reuseCooldown;
                yield break;
            }

            if (showLoadingScreen)
            {
                PortalMapLoadingUI.Instance.PlayTransition(
                    loadingBeforeMapSwitch,
                    () =>
                    {
                        hasTeleported = LevelManager.Instance != null &&
                                        LevelManager.Instance.LoadNextDungeonFromPortal(playerArrivalOffset);
                        return hasTeleported;
                    },
                    loadingAfterMapSwitch,
                    switched =>
                    {
                        if (!switched)
                        {
                            hasTeleported = false;
                            isBusy = false;
                            Debug.LogWarning("[TeleportGate] Cannot teleport: no next dungeon/map configured.");
                        }
                    });

                nextAllowedUseTime = Time.time + reuseCooldown;
                yield break;
            }
            else
            {
                hasTeleported = LevelManager.Instance.LoadNextDungeonFromPortal(playerArrivalOffset);
            }

            if (!hasTeleported)
            {
                Debug.LogWarning("[TeleportGate] Cannot teleport: no next dungeon/map configured.");
            }
        }

        nextAllowedUseTime = Time.time + reuseCooldown;
        isBusy = false;
    }

    private IEnumerator AnimateToFrame(int targetFrame, bool startFromLastFrame = false)
    {
        if (frames == null || frames.Length == 0)
            yield break;

        if (startFromLastFrame)
            SetFrame(LastFrameIndex);

        int step = targetFrame >= currentFrame ? 1 : -1;
        while (currentFrame != targetFrame)
        {
            SetFrame(currentFrame + step);
            yield return new WaitForSeconds(frameInterval);
        }
    }

    private void StartAnimation(IEnumerator routine)
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine = StartCoroutine(routine);
    }

    private void SetFrame(int index)
    {
        if (frames == null || frames.Length == 0 || spriteRenderer == null)
            return;

        currentFrame = Mathf.Clamp(index, 0, LastFrameIndex);
        spriteRenderer.sprite = frames[currentFrame];
    }

    private int LastFrameIndex => frames == null || frames.Length == 0 ? 0 : frames.Length - 1;

    private bool IsValidPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return false;

        NetworkObject networkObject = playerHealth.GetComponent<NetworkObject>();
        return networkObject == null || networkObject.IsOwner;
    }

    private PlayerHealth GetPlayerHealthInsideGate()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            triggerCollider.bounds.center,
            triggerCollider.bounds.size,
            transform.eulerAngles.z);

        foreach (Collider2D hit in hits)
        {
            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null) continue;

            NetworkObject networkObject = playerHealth.GetComponent<NetworkObject>();
            if (networkObject == null || networkObject.IsOwner)
                return playerHealth;
        }

        return null;
    }

    private bool CanTeleportNow()
    {
        if (!requireAllPlayersInsideInMultiplayer)
            return true;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return true;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        int activePlayerCount = 0;
        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.IsSpawned || player.IsOutOfLives) continue;
            activePlayerCount++;
        }

        if (activePlayerCount <= 1)
            return true;

        HashSet<PlayerHealth> playersInside = GetPlayersInsideGate();
        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.IsSpawned || player.IsOutOfLives) continue;
            if (!playersInside.Contains(player))
                return false;
        }

        return true;
    }

    private HashSet<PlayerHealth> GetPlayersInsideGate()
    {
        HashSet<PlayerHealth> playersInside = new HashSet<PlayerHealth>();
        if (triggerCollider == null)
            return playersInside;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            triggerCollider.bounds.center,
            triggerCollider.bounds.size,
            transform.eulerAngles.z);

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.isTrigger) continue;
            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null || playerHealth.IsOutOfLives) continue;
            playersInside.Add(playerHealth);
        }

        return playersInside;
    }
}
