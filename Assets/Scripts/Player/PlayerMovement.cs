using UnityEngine;
using Unity.Netcode;
using UnityEngine.PlayerLoop;
using System.Collections;

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

    private void Awake()
    {
        actions = new PlayerActions();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public override void OnNetworkSpawn()
    {
        // Only the server simulates physics for this object.
        rb.simulated = IsServer;
        useOfflineControl = false;

        if (IsOwner)
        {
            actions.Enable();
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
        }
    }

    private void Update()
    {
        if (!IsOwner && !useOfflineControl)
        {
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
        rb.MovePosition(rb.position + serverMoveDirection * (currentSpeed * Time.fixedDeltaTime));
    }
    private void Dash()
    {
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
}
