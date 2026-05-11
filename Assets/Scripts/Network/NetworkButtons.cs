using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Khởi động Netcode multiplayer game với spawn prefab dựa trên nhân vật được chọn.
/// Được gọi khi scene gameplay load (sau khi host/client đã được chọn).
/// </summary>
public class NetworkButtons : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "SampleScene";
    private bool isNetworkStarted = false;

    private static NetworkButtons instance;
    public static NetworkButtons Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<NetworkButtons>();
                if (instance == null)
                {
                    GameObject go = new GameObject("NetworkButtons");
                    instance = go.AddComponent<NetworkButtons>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // Subscribe to scene load
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[NetworkButtons.OnSceneLoaded] Scene '{scene.name}' loaded");

        if (scene.name != gameplaySceneName)
        {
            isNetworkStarted = false;
            return;
        }

        // Check if we're in the gameplay scene
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.GameStarting)
        {
            Debug.Log("[NetworkButtons] Game scene loaded. Waiting for NetworkManager...");
            
            // Wait a frame for NetworkManager to be ready
            StartCoroutine(WaitForNetworkManagerAndStart());
        }
    }

    /// <summary>
    /// Chờ NetworkManager ready rồi mới start network
    /// </summary>
    private System.Collections.IEnumerator WaitForNetworkManagerAndStart()
    {
        int maxWaitFrames = 60; // 1 second at 60fps
        int frameCount = 0;

        while (NetworkManager.Singleton == null && frameCount < maxWaitFrames)
        {
            frameCount++;
            Debug.Log($"[NetworkButtons] Waiting for NetworkManager... ({frameCount}/{maxWaitFrames})");
            yield return null;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkButtons] FATAL: NetworkManager.Singleton not found after 60 frames!");
            Debug.LogError("[NetworkButtons] Please ensure NetworkManager is in the scene!");
            yield break;
        }

        Debug.Log("[NetworkButtons] ✓ NetworkManager found! Starting network...");
        
        // Ensure CharacterPrefabManager is initialized
        var prefabManager = CharacterPrefabManager.Instance;
        if (prefabManager == null)
        {
            Debug.LogError("[NetworkButtons] CharacterPrefabManager not found!");
            yield break;
        }

        // Setup ConnectionApproval to handle character-specific spawning
        SetupConnectionApproval();

        // Single-player should run as host so the local player is spawned.
        // Multiplayer uses the room role to decide host/client.
        if (!GameManager.Instance.IsMultiplayer)
        {
            StartHost();
        }
        else if (GameManager.Instance.IsHost)
        {
            StartHost();
        }
        else
        {
            StartClient();
        }
    }

    /// <summary>
    /// Setup ConnectionApproval callback để xử lý spawn prefab dựa trên nhân vật
    /// </summary>
    private void SetupConnectionApproval()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkButtons] NetworkManager.Singleton is null!");
            return;
        }

        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

        // Set up server-side approval handler
        NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;

        // Set up client-side connect callback
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        Debug.Log("[NetworkButtons] ConnectionApproval setup complete");
    }

    /// <summary>
    /// Server-side: Xác định xem client được phép kết nối hay không
    /// và thiết lập ConnectionData với thông tin nhân vật
    /// </summary>
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        Debug.Log("[NetworkButtons.ApprovalCheck] Processing connection approval");

        // Lấy thông tin nhân vật từ ConnectionData nếu client gửi
        string clientCharacter = ExtractCharacterFromConnectionData(request.Payload);
        
        if (string.IsNullOrEmpty(clientCharacter))
        {
            // Nếu client không gửi, sử dụng character từ GameManager
            clientCharacter = GameManager.Instance.SelectedCharacter;
        }

        Debug.Log($"[NetworkButtons.ApprovalCheck] Client character: {clientCharacter}");

        if (!CharacterPrefabManager.Instance.TryGetPrefabHashForCharacter(clientCharacter, out uint prefabHash))
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = $"No registered prefab found for character '{clientCharacter}'.";
            Debug.LogError($"[NetworkButtons.ApprovalCheck] {response.Reason}");
            return;
        }

        // Luôn approve kết nối
        response.Approved = true;
        
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = prefabHash;
        response.Position = GetSpawnPosition();
        response.Rotation = Quaternion.identity;

        Debug.Log($"[NetworkButtons.ApprovalCheck] Approved with prefab hash {prefabHash} for '{clientCharacter}'");
    }

    /// <summary>
    /// Client-side: Gửi thông tin nhân vật trong ConnectionData
    /// </summary>
    public void StartClient()
    {
        if (isNetworkStarted)
        {
            Debug.LogWarning("[NetworkButtons] Network already started");
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkButtons] NetworkManager.Singleton is null!");
            return;
        }

        Debug.Log("[NetworkButtons] Starting as Client...");

        string selectedCharacter = GameManager.Instance.SelectedCharacter;
        Debug.Log($"[NetworkButtons] Client starting with character: {selectedCharacter}");

        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

        // Chuẩn bị ConnectionData với thông tin nhân vật
        byte[] connectionData = System.Text.Encoding.UTF8.GetBytes(selectedCharacter);

        if (NetworkManager.Singleton.NetworkConfig.ConnectionData != null &&
            NetworkManager.Singleton.NetworkConfig.ConnectionData.Length > 0)
        {
            Debug.LogWarning("[NetworkButtons] ConnectionData already set, will override");
        }

        NetworkManager.Singleton.NetworkConfig.ConnectionData = connectionData;
        isNetworkStarted = true;

        if (!NetworkManager.Singleton.StartClient())
        {
            Debug.LogError("[NetworkButtons] Failed to start client");
            isNetworkStarted = false;
        }
        else
        {
            Debug.Log("[NetworkButtons] ✓ Client started successfully");
            GameManager.Instance.ChangeState(GameManager.GameState.InGame);
        }
    }

    /// <summary>
    /// Host-side: Khởi động host với character của host player
    /// </summary>
    public void StartHost()
    {
        if (isNetworkStarted)
        {
            Debug.LogWarning("[NetworkButtons] Network already started");
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkButtons] NetworkManager.Singleton is null!");
            return;
        }

        Debug.Log("[NetworkButtons] Starting as Host...");

        string selectedCharacter = GameManager.Instance.SelectedCharacter;
        Debug.Log($"[NetworkButtons] Host starting with character: {selectedCharacter}");

        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

        // Host sử dụng GameManager.SelectedCharacter
        isNetworkStarted = true;

        if (!NetworkManager.Singleton.StartHost())
        {
            Debug.LogError("[NetworkButtons] Failed to start host");
            isNetworkStarted = false;
        }
        else
        {
            Debug.Log("[NetworkButtons] ✓ Host started successfully");
            GameManager.Instance.ChangeState(GameManager.GameState.InGame);
        }
    }

    /// <summary>
    /// Lấy vị trí spawn từ spawn point nếu có, nếu không dùng (0,0,0)
    /// </summary>
    private Vector3 GetSpawnPosition()
    {
        Transform spawnPoint = GameObject.Find("SpawnPoint")?.transform;
        if (spawnPoint != null)
        {
            return spawnPoint.position;
        }

        // Default spawn position
        return Vector3.zero;
    }

    /// <summary>
    /// Extract character name từ ConnectionData
    /// </summary>
    private string ExtractCharacterFromConnectionData(byte[] data)
    {
        if (data == null || data.Length == 0)
            return null;

        try
        {
            return System.Text.Encoding.UTF8.GetString(data);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkButtons] Error decoding ConnectionData: {e.Message}");
            return null;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkButtons] Client connected: {clientId}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetworkButtons] Client disconnected: {clientId}");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        isNetworkStarted = false;
    }
}
