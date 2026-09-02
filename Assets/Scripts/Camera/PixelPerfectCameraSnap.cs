using UnityEngine;

[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(Camera))]
public class PixelPerfectCameraSnap : MonoBehaviour
{
    [Header("Pixel Grid")]
    [SerializeField] int pixelsPerUnit = 16;
    [SerializeField] bool snapPosition = false;
    [SerializeField] bool snapX = true;
    [SerializeField] bool snapY = true;

    [Header("Camera Scale")]
    [Tooltip("Keeps the camera zoom on a whole-number pixel scale. This prevents thin seams between pixel-art tiles at resolutions like 1920x1080.")]
    [SerializeField] bool forceIntegerPixelScale = true;
    [SerializeField] float targetOrthographicSize = 5f;
    [SerializeField] int minPixelScale = 1;
    [SerializeField] bool preferZoomIn = false;

    Camera cam;
    float GridSize => pixelsPerUnit <= 0 ? 1f / 16f : 1f / pixelsPerUnit;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
            targetOrthographicSize = cam.orthographicSize;
    }

    void LateUpdate()
    {
        ApplyPixelPerfectCamera();
    }

    void ApplyPixelPerfectCamera()
    {
        ApplyIntegerPixelScale();
        SnapToPixelGrid();
    }

    void ApplyIntegerPixelScale()
    {
        if (!forceIntegerPixelScale || cam == null || !cam.orthographic || pixelsPerUnit <= 0)
            return;

        float wantedScale = Screen.height / (targetOrthographicSize * 2f * pixelsPerUnit);
        int pixelScale = preferZoomIn
            ? Mathf.CeilToInt(wantedScale)
            : Mathf.FloorToInt(wantedScale);
        pixelScale = Mathf.Max(minPixelScale, pixelScale);

        cam.orthographicSize = Screen.height / (2f * pixelsPerUnit * pixelScale);
    }

    void SnapToPixelGrid()
    {
        if (!snapPosition)
            return;

        float grid = GridSize;
        Vector3 position = transform.position;

        if (snapX)
            position.x = Mathf.Round(position.x / grid) * grid;
        if (snapY)
            position.y = Mathf.Round(position.y / grid) * grid;

        transform.position = position;
    }
}
