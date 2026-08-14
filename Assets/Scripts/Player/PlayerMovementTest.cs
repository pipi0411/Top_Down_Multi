using UnityEngine;
using Unity.Netcode;
using UnityEngine.PlayerLoop;
using System.Collections;
using Cinemachine;

public class PlayerMovementTest : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed;
    [SerializeField] private bool allowOfflineTest = true;
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashTime = 0.2f;
    [SerializeField] private float transperency = 0.5f;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private PlayerActions actions;
    private Vector2 moveDirection;
    private Vector2 serverMoveDirection;
    private float currentSpeed;
    private bool isDashing;
    private bool useOfflineControl;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        actions = new PlayerActions();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        // Only the server simulates physics for this object.
        rb.simulated = IsServer;
        useOfflineControl = false;

        if (IsOwner)
        {
            actions.Enable();
            SetupCameraFollow();
        }
    }

    public override void OnNetworkDespawn()
    {
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

        if (IsServer)
        {
            if (IsOwner)
            {
                serverMoveDirection = moveDirection;
            }

            MovePlayer();
            return;
        }

        if (IsOwner)
        {
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

    if (serverMoveDirection.sqrMagnitude < 0.01f)
    {
        rb.linearVelocity = Vector2.zero;
        return;
    }

    // Tính vận tốc mong muốn
    Vector2 desiredVelocity = serverMoveDirection * currentSpeed;

    // Dùng velocity thay vì MovePosition → Collider hoạt động đáng tin cậy hơn nhiều
    rb.linearVelocity = desiredVelocity;

    // Giới hạn vận tốc (phòng lúc dash)
    if (rb.linearVelocity.magnitude > currentSpeed * 1.05f)
    {
        rb.linearVelocity = rb.linearVelocity.normalized * currentSpeed;
    }
}    private void Dash()
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
            spriteRenderer.flipX = false;
        }
        else if (moveDirection.x < 0f)
        {
            spriteRenderer.flipX = true;
        }
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
