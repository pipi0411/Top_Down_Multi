using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField] private bool openWhenPlayerNear = true;
    [SerializeField] private float openDistance = 1.25f;
    [SerializeField] private float closeDistance = 2.1f;
    [SerializeField] private float closeDelayAfterPlayerLeaves = 0.25f;
    [SerializeField] private float minOpenDuration = 0.35f;
    [SerializeField] private float checkInterval = 0.02f;
    [SerializeField] private float animationFadeDuration = 0f;

    private Animator animator;
    private Collider2D[] doorColliders;
    private Bounds detectionBounds;
    private bool hasDetectionBounds;
    private bool isLocked;
    private bool isOpen;
    private float nextCheckTime;
    private float lastPlayerNearTime = float.NegativeInfinity;
    private float lastStateChangeTime = float.NegativeInfinity;
    private int openedStateHash;
    private int closedStateHash;

    public bool BlocksProjectiles => !isOpen;
    public bool IsLocked => isLocked;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        ResolveAnimationStates();
        CacheDoorColliders();
    }

    private void OnValidate()
    {
        closeDistance = Mathf.Max(closeDistance, openDistance + 0.25f);
        closeDelayAfterPlayerLeaves = Mathf.Max(0f, closeDelayAfterPlayerLeaves);
        minOpenDuration = Mathf.Max(0f, minOpenDuration);
        checkInterval = Mathf.Max(0.01f, checkInterval);
    }

    private void Start()
    {
        PlayClosedInstantly();
        CacheDetectionBounds();
    }

    private void Update()
    {
        if (MultiplayerGameplaySync.IsNetworkActive && !MultiplayerGameplaySync.IsServer)
            return;

        if (isLocked || !openWhenPlayerNear || Time.time < nextCheckTime) return;

        nextCheckTime = Time.time + checkInterval;
        bool shouldOpen = HasPlayerNear(isOpen ? closeDistance : openDistance);

        if (shouldOpen)
        {
            lastPlayerNearTime = Time.time;
            ShowOpenAnimation();
        }
        else if (isOpen
                 && Time.time - lastPlayerNearTime >= closeDelayAfterPlayerLeaves
                 && Time.time - lastStateChangeTime >= minOpenDuration)
        {
            ShowCloseAnimation();
        }
    }

    private void CacheDoorColliders()
    {
        doorColliders = GetComponentsInChildren<Collider2D>(true);
        SetDoorPassable(false);
    }

    private void CacheDetectionBounds()
    {
        hasDetectionBounds = false;

        if (doorColliders == null) return;

        foreach (Collider2D doorCollider in doorColliders)
        {
            if (doorCollider == null || !doorCollider.enabled) continue;

            if (!hasDetectionBounds)
            {
                detectionBounds = doorCollider.bounds;
                hasDetectionBounds = true;
            }
            else
            {
                detectionBounds.Encapsulate(doorCollider.bounds);
            }
        }
    }

    private bool HasPlayerNear(float distance)
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        float sqrDistance = distance * distance;

        foreach (PlayerHealth player in players)
        {
            if (player == null || player.CurrentHealth <= 0f) continue;

            if (GetSqrDistanceToPlayer(player) <= sqrDistance)
                return true;
        }

        return false;
    }

    private float GetSqrDistanceToPlayer(PlayerHealth player)
    {
        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>();
        float nearestSqrDistance = float.PositiveInfinity;

        if (playerColliders != null && playerColliders.Length > 0)
        {
            foreach (Collider2D playerCollider in playerColliders)
            {
                if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger) continue;

                float sqrDistance = GetSqrDistanceToDoor(playerCollider);
                if (sqrDistance < nearestSqrDistance)
                    nearestSqrDistance = sqrDistance;
            }
        }

        if (!float.IsPositiveInfinity(nearestSqrDistance))
            return nearestSqrDistance;

        return GetSqrDistanceToDoor(player.transform.position);
    }

    private float GetSqrDistanceToDoor(Collider2D playerCollider)
    {
        return GetSqrDistanceToDoor(playerCollider.bounds);
    }

    private float GetSqrDistanceToDoor(Bounds playerBounds)
    {
        if (hasDetectionBounds)
            return GetSqrDistanceBetweenBounds(detectionBounds, playerBounds);

        return ((Vector2)transform.position - (Vector2)playerBounds.center).sqrMagnitude;
    }

    private float GetSqrDistanceToDoor(Vector2 point)
    {
        if (hasDetectionBounds)
            return detectionBounds.SqrDistance(point);

        return ((Vector2)transform.position - point).sqrMagnitude;
    }

    private float GetSqrDistanceBetweenBounds(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
        return dx * dx + dy * dy;
    }

    public void ShowCloseAnimation()
    {
        if (!isOpen) return;

        if (HasPlayerNear(closeDistance))
        {
            lastPlayerNearTime = Time.time;
            return;
        }

        if (animator != null)
            animator.CrossFade(closedStateHash, animationFadeDuration, 0, 0f);

        SetDoorPassable(false);
        isOpen = false;
        lastStateChangeTime = Time.time;
        MultiplayerGameplaySync.BroadcastDoorState(this, isLocked, isOpen);
    }

    public void ShowOpenAnimation()
    {
        if (isLocked) return;
        if (isOpen) return;

        if (animator != null)
            animator.CrossFade(openedStateHash, animationFadeDuration, 0, 0f);

        SetDoorPassable(true);
        isOpen = true;
        lastStateChangeTime = Time.time;
        MultiplayerGameplaySync.BroadcastDoorState(this, isLocked, isOpen);
    }

    public void LockClosed()
    {
        isLocked = true;

        if (animator != null)
            animator.CrossFade(closedStateHash, animationFadeDuration, 0, 0f);

        SetDoorPassable(false);
        isOpen = false;
        lastStateChangeTime = Time.time;
        MultiplayerGameplaySync.BroadcastDoorState(this, isLocked, isOpen);
    }

    public void UnlockAndOpen()
    {
        isLocked = false;
        ShowOpenAnimation();
    }

    public void ApplyRemoteState(bool locked, bool open)
    {
        isLocked = locked;

        if (animator != null)
            animator.CrossFade(open ? openedStateHash : closedStateHash, animationFadeDuration, 0, 0f);

        SetDoorPassable(open);
        isOpen = open;
        lastStateChangeTime = Time.time;
    }

    private void ResolveAnimationStates()
    {
        string sourceName = gameObject.name;
        bool usesOverrideController = false;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            sourceName = animator.runtimeAnimatorController.name;
            usesOverrideController = animator.runtimeAnimatorController is AnimatorOverrideController;
        }

        bool westEastDoor = !usesOverrideController && sourceName.Contains("WE");
        string prefix = westEastDoor ? "Door_WE" : "Door_NS";
        openedStateHash = Animator.StringToHash(prefix + "_Opened");
        closedStateHash = Animator.StringToHash(prefix + "_Closed");
    }

    private void PlayClosedInstantly()
    {
        if (animator != null)
            animator.Play(closedStateHash, 0, 1f);

        SetDoorPassable(false);
        isOpen = false;
        lastStateChangeTime = Time.time;
    }

    private void SetDoorPassable(bool passable)
    {
        if (doorColliders == null) return;

        foreach (Collider2D doorCollider in doorColliders)
        {
            if (doorCollider != null)
                doorCollider.isTrigger = passable;
        }
    }
}
