using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class WeaponChest : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] Sprite closedSprite;
    [SerializeField] Sprite[] openingFrames;
    [SerializeField] Sprite openSprite;

    [Header("Interaction")]
    [SerializeField] Key interactKey = Key.E;
    [SerializeField] float interactRadius = 1.15f;
    [SerializeField] float openFrameInterval = 0.12f;
    [SerializeField] TextMeshPro interactPrompt;
    [SerializeField] string interactPromptText = "Ấn E để mở";
    [SerializeField] Vector3 interactPromptOffset = new Vector3(0f, 0.85f, -0.1f);
    [SerializeField] Color interactPromptColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] Color interactPromptOutlineColor = new Color(0.17f, 0.02f, 0.32f, 1f);

    [Header("Drop")]
    [SerializeField] GameObject[] weaponPrefabs;
    [SerializeField] Vector3 dropOffset = new Vector3(0f, -0.85f, -0.5f);
    [SerializeField] float droppedWeaponScale = 1.2f;
    [SerializeField] float pickupColliderRadius = 0.45f;
    [SerializeField] float pickupDelay = 0.45f;
    [SerializeField] string weaponSortingLayer = "Weapon";
    [SerializeField] int weaponSortingOrder = 8;

    SpriteRenderer spriteRenderer;
    Collider2D chestCollider;
    bool opened;
    bool opening;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        chestCollider = GetComponent<Collider2D>();
        chestCollider.isTrigger = false;
        SetupPrompt();
        ShowClosed();
    }

    void Update()
    {
        PlayerHealth player = opened || opening ? null : FindLocalPlayerInRange();
        SetPromptVisible(player != null);

        if (opened || opening) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[interactKey].wasPressedThisFrame) return;

        if (player == null) return;

        RequestOpen(player);
    }

    void RequestOpen(PlayerHealth player)
    {
        if (MultiplayerGameplaySync.IsNetworkActive)
        {
            if (MultiplayerGameplaySync.IsServer)
                TryOpenAuthoritative(player);
            else
                MultiplayerGameplaySync.RequestWeaponChestOpen(this, player);
            return;
        }

        OpenLocal(PickRandomWeaponIndex(), GetDropPosition());
    }

    public void TryOpenAuthoritative(PlayerHealth player)
    {
        if (opened || opening || player == null || !IsPlayerInRange(player)) return;

        int weaponIndex = PickRandomWeaponIndex();
        Vector3 dropPosition = GetDropPosition();
        OpenLocal(weaponIndex, dropPosition);
        MultiplayerGameplaySync.BroadcastWeaponChestOpened(this, weaponIndex, dropPosition);
    }

    public void ApplyRemoteOpened(int weaponIndex, Vector3 dropPosition)
    {
        if (opened || opening) return;
        OpenLocal(weaponIndex, dropPosition);
    }

    void OpenLocal(int weaponIndex, Vector3 dropPosition)
    {
        opened = true;
        opening = true;
        StartCoroutine(OpenRoutine(weaponIndex, dropPosition));
    }

    IEnumerator OpenRoutine(int weaponIndex, Vector3 dropPosition)
    {
        SetPromptVisible(false);

        if (openingFrames != null)
        {
            foreach (Sprite frame in openingFrames)
            {
                if (frame != null && spriteRenderer != null)
                    spriteRenderer.sprite = frame;
                yield return new WaitForSeconds(openFrameInterval);
            }
        }

        ShowOpen();
        SpawnDroppedWeapon(weaponIndex, dropPosition);
        opening = false;
    }

    void SpawnDroppedWeapon(int weaponIndex, Vector3 dropPosition)
    {
        if (weaponPrefabs == null || weaponPrefabs.Length == 0) return;
        if (weaponIndex < 0 || weaponIndex >= weaponPrefabs.Length) return;

        GameObject prefab = weaponPrefabs[weaponIndex];
        if (prefab == null) return;

        GameObject weaponObject = Instantiate(prefab, dropPosition, Quaternion.identity, transform.parent);
        weaponObject.transform.localScale = Vector3.one * droppedWeaponScale;

        Weapon weapon = weaponObject.GetComponent<Weapon>();
        if (weapon != null)
            weapon.SetDroppedState();

        CircleCollider2D pickupCollider = weaponObject.GetComponent<CircleCollider2D>();
        if (pickupCollider == null)
            pickupCollider = weaponObject.AddComponent<CircleCollider2D>();
        pickupCollider.isTrigger = true;
        pickupCollider.radius = pickupColliderRadius;

        Rigidbody2D rb = weaponObject.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = weaponObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        WeaponPickup pickup = weaponObject.GetComponent<WeaponPickup>();
        if (pickup == null)
            pickup = weaponObject.AddComponent<WeaponPickup>();
        pickup.Initialize(weapon, pickupDelay);

        NetworkedWorldEntity chestEntity = GetComponent<NetworkedWorldEntity>();
        if (chestEntity != null)
        {
            NetworkedWorldEntity weaponEntity = weaponObject.GetComponent<NetworkedWorldEntity>();
            if (weaponEntity == null)
                weaponEntity = weaponObject.AddComponent<NetworkedWorldEntity>();
            weaponEntity.Initialize($"{chestEntity.NetworkId}_Weapon_{weaponIndex}");
        }

        int sortingLayerId = SortingLayer.NameToID(weaponSortingLayer);
        foreach (SpriteRenderer renderer in weaponObject.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = weaponSortingOrder;
            renderer.flipY = false;
        }
    }

    int PickRandomWeaponIndex()
    {
        if (weaponPrefabs == null || weaponPrefabs.Length == 0) return -1;

        for (int attempts = 0; attempts < 12; attempts++)
        {
            int index = Random.Range(0, weaponPrefabs.Length);
            if (weaponPrefabs[index] != null)
                return index;
        }

        for (int i = 0; i < weaponPrefabs.Length; i++)
            if (weaponPrefabs[i] != null)
                return i;

        return -1;
    }

    Vector3 GetDropPosition()
    {
        return transform.position + dropOffset;
    }

    PlayerHealth FindLocalPlayerInRange()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        foreach (PlayerHealth player in players)
        {
            if (player == null || player.IsDead) continue;
            if (player.IsSpawned && !player.IsOwner) continue;
            if (IsPlayerInRange(player))
                return player;
        }

        return null;
    }

    bool IsPlayerInRange(PlayerHealth player)
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.transform.position) <= interactRadius;
    }

    void ShowClosed()
    {
        if (spriteRenderer != null && closedSprite != null)
            spriteRenderer.sprite = closedSprite;
    }

    void ShowOpen()
    {
        if (spriteRenderer != null)
            spriteRenderer.sprite = openSprite != null ? openSprite : closedSprite;
    }

    void SetupPrompt()
    {
        if (interactPrompt == null)
            interactPrompt = GetComponentInChildren<TextMeshPro>(true);

        if (interactPrompt == null)
        {
            GameObject promptObject = new GameObject("InteractPrompt");
            promptObject.transform.SetParent(transform, false);
            interactPrompt = promptObject.AddComponent<TextMeshPro>();
        }

        interactPrompt.text = interactPromptText;
        interactPrompt.alignment = TextAlignmentOptions.Center;
        interactPrompt.fontSize = interactPrompt.fontSize > 0.1f ? interactPrompt.fontSize : 2.8f;
        interactPrompt.color = interactPromptColor;
        interactPrompt.outlineColor = interactPromptOutlineColor;
        interactPrompt.outlineWidth = 0.34f;
        interactPrompt.sortingLayerID = SortingLayer.NameToID("UI");
        interactPrompt.sortingOrder = Mathf.Max(interactPrompt.sortingOrder, 50);
        interactPrompt.textWrappingMode = TextWrappingModes.NoWrap;
        interactPrompt.transform.localPosition = interactPromptOffset;
        interactPrompt.gameObject.SetActive(false);
    }

    void SetPromptVisible(bool visible)
    {
        if (interactPrompt == null) return;
        if (interactPrompt.gameObject.activeSelf != visible)
            interactPrompt.gameObject.SetActive(visible);
    }
}
