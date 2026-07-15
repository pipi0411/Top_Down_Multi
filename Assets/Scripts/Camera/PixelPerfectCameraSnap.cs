using UnityEngine;

[DefaultExecutionOrder(10000)]
public class PixelPerfectCameraSnap : MonoBehaviour
{
    [SerializeField] int pixelsPerUnit = 16;
    [SerializeField] bool snapX = true;
    [SerializeField] bool snapY = true;

    float GridSize => pixelsPerUnit <= 0 ? 1f / 16f : 1f / pixelsPerUnit;

    void LateUpdate()
    {
        float grid = GridSize;
        Vector3 position = transform.position;

        if (snapX)
            position.x = Mathf.Round(position.x / grid) * grid;
        if (snapY)
            position.y = Mathf.Round(position.y / grid) * grid;

        transform.position = position;
    }
}
