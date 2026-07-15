using UnityEngine;

public class EnemyDamagePopup : MonoBehaviour
{
    const float Lifetime = 1.25f;

    [SerializeField] Vector3 moveSpeed = new Vector3(0f, 1.25f, 0f);
    [SerializeField] float horizontalJitter = 0.25f;
    [SerializeField] static float yOffset = 0.9f;
    [SerializeField] int fontSize = 42;
    [SerializeField] float characterSize = 0.07f;
    [SerializeField] Color textColor = new Color(0.85f, 0.02f, 0.01f, 1f);
    [SerializeField] Color shadowColor = new Color(0.04f, 0f, 0f, 0.95f);
    [SerializeField] int sortingOrder = 1000;

    TextMesh textMesh;
    TextMesh shadowTextMesh;
    MeshRenderer meshRenderer;
    MeshRenderer shadowRenderer;
    float timer;
    Color startColor;
    Vector3 startScale;

    public static void Show(Vector3 worldPosition, float damage)
    {
        if (damage <= 0f) return;

        GameObject popupObject = new GameObject("DamagePopup");
        popupObject.transform.position = new Vector3(
            worldPosition.x + Random.Range(-0.18f, 0.18f),
            worldPosition.y + yOffset,
            -5f);

        EnemyDamagePopup popup = popupObject.AddComponent<EnemyDamagePopup>();
        popup.Setup(damage);
    }

    void Setup(float damage)
    {
        string damageText = FormatDamage(damage);

        GameObject shadowObject = new GameObject("Shadow");
        shadowObject.transform.SetParent(transform, false);
        shadowObject.transform.localPosition = new Vector3(0.035f, -0.035f, 0.01f);
        shadowTextMesh = CreateTextMesh(shadowObject, damageText, shadowColor);
        shadowRenderer = shadowObject.GetComponent<MeshRenderer>();

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = Vector3.zero;
        textMesh = CreateTextMesh(textObject, damageText, textColor);
        meshRenderer = textObject.GetComponent<MeshRenderer>();

        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerID = SortingLayer.NameToID("UI");
            meshRenderer.sortingOrder = sortingOrder;
        }
        if (shadowRenderer != null)
        {
            shadowRenderer.sortingLayerID = SortingLayer.NameToID("UI");
            shadowRenderer.sortingOrder = sortingOrder - 1;
        }

        startColor = textColor;
        moveSpeed.x = Random.Range(-horizontalJitter, horizontalJitter);
        startScale = transform.localScale;
    }

    void Update()
    {
        timer += Time.deltaTime;
        transform.position += moveSpeed * Time.deltaTime;

        float normalizedTime = Mathf.Clamp01(timer / Lifetime);
        float popScale = normalizedTime < 0.18f
            ? Mathf.Lerp(0.65f, 1.2f, normalizedTime / 0.18f)
            : Mathf.Lerp(1.2f, 0.85f, (normalizedTime - 0.18f) / 0.82f);
        transform.localScale = startScale * popScale;

        if (textMesh != null)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01((normalizedTime - 0.25f) / 0.75f));
            textMesh.color = color;
        }
        if (shadowTextMesh != null)
        {
            Color color = shadowColor;
            color.a = Mathf.Lerp(shadowColor.a, 0f, Mathf.Clamp01((normalizedTime - 0.25f) / 0.75f));
            shadowTextMesh.color = color;
        }

        if (timer >= Lifetime)
            Destroy(gameObject);
    }

    string FormatDamage(float damage)
    {
        float rounded = Mathf.Round(damage);
        if (Mathf.Abs(damage - rounded) < 0.01f)
            return $"-{rounded:0}";

        return $"-{damage:0.#}";
    }

    TextMesh CreateTextMesh(GameObject target, string value, Color color)
    {
        TextMesh mesh = target.AddComponent<TextMesh>();
        mesh.text = value;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = fontSize;
        mesh.characterSize = characterSize;
        mesh.color = color;
        return mesh;
    }
}
