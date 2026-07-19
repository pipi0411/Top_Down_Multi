using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField] private bool openWhenPlayerNear = true;
    [SerializeField] private float openDistance = 1.25f;
    [SerializeField] private float closeDistance = 1.3f;
    [SerializeField] private float closeDelayAfterPlayerLeaves = 0.05f;
    [SerializeField] private float checkInterval = 0.02f;
    [SerializeField] private float animationFadeDuration = 0f;

    private Animator animator;
    private Collider2D[] doorColliders;
    private bool isOpen;
    private float nextCheckTime;
    private float lastPlayerNearTime = float.NegativeInfinity;
    private int openedStateHash;
    private int closedStateHash;

    public bool BlocksProjectiles => !isOpen;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        ResolveAnimationStates();
        CacheDoorColliders();
    }

    private void Start()
    {
        PlayClosedInstantly();
    }

    private void Update()
    {
        if (!openWhenPlayerNear || Time.time < nextCheckTime) return;

        nextCheckTime = Time.time + checkInterval;
        bool shouldOpen = HasPlayerNear(isOpen ? closeDistance : openDistance);

        if (shouldOpen)
        {
            lastPlayerNearTime = Time.time;
            ShowOpenAnimation();
        }
        else if (isOpen && Time.time - lastPlayerNearTime >= closeDelayAfterPlayerLeaves)
        {
            ShowCloseAnimation();
        }
    }

    private void CacheDoorColliders()
    {
        doorColliders = GetComponentsInChildren<Collider2D>(true);
        SetDoorPassable(false);
    }

    private bool HasPlayerNear(float distance)
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        float sqrDistance = distance * distance;

        foreach (PlayerHealth player in players)
        {
            if (player == null || player.CurrentHealth <= 0f) continue;

            Vector2 playerPosition = player.transform.position;
            if (((Vector2)transform.position - playerPosition).sqrMagnitude <= sqrDistance)
                return true;
        }

        return false;
    }

    public void ShowCloseAnimation()
    {
        if (!isOpen) return;

        if (animator != null)
            animator.CrossFade(closedStateHash, animationFadeDuration, 0, 0f);

        SetDoorPassable(false);
        isOpen = false;
    }

    public void ShowOpenAnimation()
    {
        if (isOpen) return;

        if (animator != null)
            animator.CrossFade(openedStateHash, animationFadeDuration, 0, 0f);

        SetDoorPassable(true);
        isOpen = true;
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
