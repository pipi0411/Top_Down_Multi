using UnityEngine;
using Unity.Netcode;
using UnityEngine.PlayerLoop;
using System.Collections;
using Cinemachine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed;
    [SerializeField] private bool allowOfflineTest = true;
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashTime = 0.2f;
    [SerializeField] private float transperency = 0.5f;
    [Header("Collision")]
    [SerializeField] private LayerMask movementBlockMask = (1 << 0) | (1 << 6) | (1 << 7);
    [SerializeField] private float movementSkin = 0.03f;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private PlayerActions actions;
    private Vector2 moveDirection;
    private Vector2 serverMoveDirection;
    private float currentSpeed;
    private bool isDashing;
    private bool useOfflineControl;
    private PlayerHealth playerHealth;
    private readonly Collider2D[] overlapHits = new Collider2D[12];
    private readonly NetworkVariable<bool> networkFacingLeft = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        actions = new PlayerActions();
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        playerHealth = GetComponent<PlayerHealth>();
        NormalizeMovementBlockMask();
    }

    public override void OnNetworkSpawn()
    {
        // Owner simulates its own player for responsive WebGL/client movement.
        // NetworkTransform then syncs the transform to the server/other clients.
        rb.simulated = IsServer || IsOwner;
        useOfflineControl = false;

        if (IsOwner)
        {
            actions.Enable();
            SetupCameraFollow();
        }
        else
        {
            ApplyFacing(networkFacingLeft.Value);
        }

        networkFacingLeft.OnValueChanged += HandleFacingChanged;
    }

    public override void OnNetworkDespawn()
    {
        networkFacingLeft.OnValueChanged -= HandleFacingChanged;

        if (!useOfflineControl)
        {
            actions.Disable();
        }
    }

    private void Start()
    {
        currentSpeed = speed;
        actions.Movement.Dash.performed += ctx => Dash();

        if (allowOfflineTest && !IsSpawned && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening))
        {
            EnableOfflineControl();
            SetupCameraFollow();
        }
    }

    private void Update()
    {
        if (!IsOwner && !useOfflineControl)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            moveDirection = Vector2.zero;
            return;
        }

        CaptureInput();
        RotatePlayer();
    }

    private void FixedUpdate()
    {
        if (useOfflineControl)
        {
            serverMoveDirection = moveDirection;
            MovePlayer();
            return;
        }

        if (IsOwner)
        {
            serverMoveDirection = moveDirection;
            MovePlayer();
            SubmitMoveInputServerRpc(moveDirection);
        }
    }
    private void MovePlayer()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        MoveWithCollision(serverMoveDirection * (currentSpeed * Time.fixedDeltaTime));
        if ((IsOwner || useOfflineControl) && serverMoveDirection.sqrMagnitude > 0.01f)
            GameAudioManager.Instance?.PlayFootstep();
    }

    private void MoveWithCollision(Vector2 movement)
    {
        if (rb == null || movement.sqrMagnitude <= 0.000001f)
            return;

        ResolveBlockingOverlaps();

        Vector2 position = rb.position;
        Vector2 nextPosition = position;

        Vector2 horizontal = new Vector2(movement.x, 0f);
        if (Mathf.Abs(horizontal.x) > 0.000001f && CanOccupyPosition(position + horizontal))
            nextPosition += horizontal;

        Vector2 vertical = new Vector2(0f, movement.y);
        if (Mathf.Abs(vertical.y) > 0.000001f && CanOccupyPosition(nextPosition + vertical))
            nextPosition += vertical;

        if ((nextPosition - position).sqrMagnitude > 0.000001f)
            rb.MovePosition(nextPosition);

        ResolveBlockingOverlaps();
    }

    private bool CanOccupyPosition(Vector2 targetBodyPosition)
    {
        if (bodyCollider == null)
            return true;

        Bounds bounds = bodyCollider.bounds;
        Vector2 currentBodyPosition = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 offset = targetBodyPosition - currentBodyPosition;
        Vector2 center = (Vector2)bounds.center + offset;
        Vector2 size = new Vector2(
            Mathf.Max(0.01f, bounds.size.x - movementSkin * 2f),
            Mathf.Max(0.01f, bounds.size.y - movementSkin * 2f));

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(movementBlockMask);
        filter.useTriggers = false;

        int hitCount = Physics2D.OverlapBox(center, size, 0f, filter, overlapHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = overlapHits[i];
            overlapHits[i] = null;

            if (hit == null || hit == bodyCollider || hit.isTrigger || hit.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    private void ResolveBlockingOverlaps()
    {
        if (rb == null || bodyCollider == null)
            return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(movementBlockMask);
        filter.useTriggers = false;

        int overlapCount = bodyCollider.Overlap(filter, overlapHits);
        Vector2 correction = Vector2.zero;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D other = overlapHits[i];
            overlapHits[i] = null;

            if (other == null || other == bodyCollider || other.isTrigger)
                continue;

            ColliderDistance2D distance = bodyCollider.Distance(other);
            if (!distance.isOverlapped)
                continue;

            correction += distance.normal * (distance.distance - movementSkin);
        }

        if (correction.sqrMagnitude > 0.000001f)
            rb.position += correction;
    }

    private void NormalizeMovementBlockMask()
    {
        int mask = movementBlockMask.value;
        mask |= 1 << LayerMask.NameToLayer("Default");
        mask |= 1 << LayerMask.NameToLayer("Room");
        mask |= 1 << LayerMask.NameToLayer("Walls");
        movementBlockMask = mask;
    }
    private void Dash()
    {
        if (playerHealth != null && playerHealth.IsDead) return;
        if (isDashing)
        {
            return;
        }

        isDashing = true;
        StartCoroutine(IEDash());
    }
    private IEnumerator IEDash()
    {
        currentSpeed = dashSpeed;
        ModifySpriteRenderer(transperency);
        yield return new WaitForSeconds(dashTime);
        currentSpeed = speed;
        ModifySpriteRenderer(1f);
        isDashing = false;
    }
    private void ModifySpriteRenderer(float alpha)
    {
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color = new Color(color.r, color.g, color.b, alpha);
            spriteRenderer.color = color;
        }
    }
    private void RotatePlayer()
    {
        if (moveDirection.x >= 0.1f)
        {
            SetFacing(false);
        }
        else if (moveDirection.x < 0f)
        {
            SetFacing(true);
        }
    }

    private void SetFacing(bool facingLeft)
    {
        ApplyFacing(facingLeft);

        if (IsSpawned && IsOwner && networkFacingLeft.Value != facingLeft)
            networkFacingLeft.Value = facingLeft;
    }

    private void ApplyFacing(bool facingLeft)
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = facingLeft;
    }

    private void HandleFacingChanged(bool previousValue, bool newValue)
    {
        if (IsOwner) return;
        ApplyFacing(newValue);
    }

    private void CaptureInput()
    {
        moveDirection = actions.Movement.Move.ReadValue<Vector2>().normalized;
    }

    [ServerRpc]
    private void SubmitMoveInputServerRpc(Vector2 inputDirection)
    {
        serverMoveDirection = inputDirection;
    }

    private void EnableOfflineControl()
    {
        useOfflineControl = true;
        rb.simulated = true;
        actions.Enable();
    }

    private void SetupCameraFollow()
    {
        FollowWithCamera(transform);
    }

    public static void FollowWithCamera(Transform target)
    {
        if (target == null) return;

        CinemachineVirtualCamera vcam = FindAnyObjectByType<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            vcam.Follow = target;
            vcam.LookAt = target;
            Debug.Log($"[PlayerMovement] Cinemachine Camera is now following: {target.name}");
        }
        else
        {
            Debug.LogWarning("[PlayerMovement] No CinemachineVirtualCamera found in the scene!");
        }
    }
}
