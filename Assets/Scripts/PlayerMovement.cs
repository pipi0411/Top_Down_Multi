using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float speed;
    private Rigidbody2D rb;
    private PlayerActions actions;
    private Vector2 moveDirection;
    private Vector2 serverMoveDirection;

    private void Awake()
    {
        actions = new PlayerActions();
        rb = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn()
    {
        // Only the server simulates physics for this object.
        rb.simulated = IsServer;

        if (IsOwner)
        {
            actions.Enable();
        }
    }

    public override void OnNetworkDespawn()
    {
        actions.Disable();
    }

    private void MovePlayer()
    {
        rb.MovePosition(rb.position + serverMoveDirection * (speed * Time.fixedDeltaTime));
    }

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        CaptureInput();
    }

    private void FixedUpdate()
    {
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

    private void CaptureInput()
    {
        moveDirection = actions.Movement.Move.ReadValue<Vector2>().normalized;
    }

    [ServerRpc]
    private void SubmitMoveInputServerRpc(Vector2 inputDirection)
    {
        serverMoveDirection = inputDirection;
    }
}
