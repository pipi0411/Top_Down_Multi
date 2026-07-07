using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;   // ← Thêm dòng này

public class DummyMovement : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private CinemachineVirtualCamera vcam;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        FindAndFollowCamera();   // Tìm camera
    }

    private void Update()
    {
        // Điều khiển Dummy
        Vector2 input = Vector2.zero;
        
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1;
        }

        moveInput = input.normalized;
    }

    private void FixedUpdate()
    {
        if (moveInput != Vector2.zero)
        {
            rb.linearVelocity = moveInput * speed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    // ==================== CAMERA ====================
    private void FindAndFollowCamera()
    {
        vcam = FindAnyObjectByType<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            vcam.Follow = this.transform;
            vcam.LookAt = this.transform;
            Debug.Log("[DummyMovement] Camera is now following Dummy_Test");
        }
        else
        {
            Debug.LogWarning("[DummyMovement] Không tìm thấy CinemachineVirtualCamera!");
        }
    }

    // Nếu muốn chuyển camera về Player thì gọi hàm này
    public void SwitchCameraToPlayer(Transform playerTransform)
    {
        if (vcam != null && playerTransform != null)
        {
            vcam.Follow = playerTransform;
            vcam.LookAt = playerTransform;
            Debug.Log("[DummyMovement] Camera switched back to Player");
        }
    }
}