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
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private PlayerActions actions;
    private Vector2 moveDirection;
    private Vector2 serverMoveDirection;
    private float currentSpeed;
    private bool isDashing;
    private bool useOfflineControl;
    private PlayerHealth playerHealth;
    private readonly NetworkVariable<bool> networkFacingLeft = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        actions = new PlayerActions();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        playerHealth = GetComponent<PlayerHealth>();
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
        rb.MovePosition(rb.position + serverMoveDirection * (currentSpeed * Time.fixedDeltaTime));
        if ((IsOwner || useOfflineControl) && serverMoveDirection.sqrMagnitude > 0.01f)
            GameAudioManager.Instance?.PlayFootstep();
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
