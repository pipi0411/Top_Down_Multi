using System.Collections;
using UnityEngine;

public class BossDashTrail : MonoBehaviour
{
    [SerializeField] SpriteRenderer sourceRenderer;
    [SerializeField] float spawnInterval = 0.045f;
    [SerializeField] float ghostLifetime = 0.22f;
    [SerializeField] int sortingOrderOffset = -1;
    [SerializeField] Color ghostColor = new Color(0.6f, 0.9f, 1f, 0.42f);

    float nextSpawnTime;
    bool emitting;

    void Awake()
    {
        if (sourceRenderer == null)
            sourceRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (!emitting || sourceRenderer == null) return;
        if (Time.time < nextSpawnTime) return;

        nextSpawnTime = Time.time + Mathf.Max(0.01f, spawnInterval);
        SpawnGhost();
    }

    public void Begin()
    {
        emitting = true;
        nextSpawnTime = 0f;
    }

    public void End()
    {
        emitting = false;
    }

    void SpawnGhost()
    {
        GameObject ghost = new GameObject("BossDashGhost");
        ghost.transform.position = sourceRenderer.transform.position;
        ghost.transform.rotation = sourceRenderer.transform.rotation;
        ghost.transform.localScale = sourceRenderer.transform.lossyScale;

        SpriteRenderer ghostRenderer = ghost.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = sourceRenderer.sprite;
        ghostRenderer.flipX = sourceRenderer.flipX;
        ghostRenderer.flipY = sourceRenderer.flipY;
        ghostRenderer.color = ghostColor;
        ghostRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        ghostRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;

        StartCoroutine(FadeAndDestroy(ghostRenderer, ghost));
    }

    IEnumerator FadeAndDestroy(SpriteRenderer ghostRenderer, GameObject ghost)
    {
        float duration = Mathf.Max(0.01f, ghostLifetime);
        float elapsed = 0f;
        Color startColor = ghostRenderer.color;

        while (elapsed < duration && ghostRenderer != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            ghostRenderer.color = color;
            yield return null;
        }

        if (ghost != null)
            Destroy(ghost);
    }
}
