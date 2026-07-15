using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
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
    private float nextGameplayHeartbeat;

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
    private string GetCharacterForNetworkStartup()
    {
        if (GameManager.Instance == null)
            return null;

        if (GameManager.Instance.IsMultiplayer)
            return GameManager.Instance.RoomSelectedCharacter;

        return GameManager.Instance.SelectedCharacter;
    }

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
            // Nếu client không gửi, sử dụng character đã lưu cho room hiện tại
            clientCharacter = GetCharacterForNetworkStartup();
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
        response.Position = GetSpawnPosition(request.ClientNetworkId);
        response.Rotation = Quaternion.identity;

        Debug.Log($"[NetworkButtons.ApprovalCheck] Approved with prefab hash {prefabHash} for '{clientCharacter}'");
    }

    /// <summary>
    /// Client-side: Gửi thông tin nhân vật trong ConnectionData
    /// </summary>
    public async void StartClient()
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

        if (GameManager.Instance.IsMultiplayer && !await ConfigureRelayClient())
            return;

        Debug.Log("[NetworkButtons] Starting as Client...");

        string selectedCharacter = GetCharacterForNetworkStartup();
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
    public async void StartHost()
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

        if (GameManager.Instance.IsMultiplayer && !await ConfigureRelayHost())
            return;

        Debug.Log("[NetworkButtons] Starting as Host...");

        string selectedCharacter = GetCharacterForNetworkStartup();
        Debug.Log($"[NetworkButtons] Host starting with character: {selectedCharacter}");

        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

        // Host uses SelectedCharacter (single-player) or RoomSelectedCharacter (multiplayer).
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
    private async Task<bool> ConfigureRelayHost()
    {
        try
        {
            await EnsureUnityServicesReady();
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(7);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null) throw new InvalidOperationException("UnityTransport is missing from NetworkManager.");
            transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
            GameManager.Instance.SetRelayJoinCode(joinCode);
            bool published = await PublishRelayCode(joinCode);
            if (!published) throw new InvalidOperationException("Could not publish Relay join code to room.");
            Debug.Log($"[Relay] Host allocation ready. Join code: {joinCode}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Relay] Host setup failed: {exception.Message}");
            isNetworkStarted = false;
            return false;
        }
    }

    private void Update()
    {
        if (!isNetworkStarted || GameManager.Instance == null || !GameManager.Instance.IsMultiplayer ||
            !GameManager.Instance.IsHost || Time.unscaledTime < nextGameplayHeartbeat) return;
        if (RoomClient.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
            RoomClient.Instance.SendHeartbeat(GameManager.Instance.CurrentRoomCode);
        nextGameplayHeartbeat = Time.unscaledTime + 10f;
    }

    private async Task<bool> ConfigureRelayClient()
    {
        try
        {
            await EnsureUnityServicesReady();
            string joinCode = await WaitForRelayCode();
            if (string.IsNullOrEmpty(joinCode)) throw new InvalidOperationException("Relay join code was not published by host.");
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null) throw new InvalidOperationException("UnityTransport is missing from NetworkManager.");
            transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
            Debug.Log($"[Relay] Client joined allocation: {joinCode}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Relay] Client setup failed: {exception.Message}");
            isNetworkStarted = false;
            return false;
        }
    }

    private async Task EnsureUnityServicesReady()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private async Task<bool> PublishRelayCode(string joinCode)
    {
        if (RoomClient.Instance == null || string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode)) return false;
        var completion = new TaskCompletionSource<bool>();
        void Handler(RoomClient.RoomResult result) => completion.TrySetResult(result.success);
        RoomClient.Instance.OnSetRelayCodeComplete += Handler;
        RoomClient.Instance.SetRelayJoinCode(GameManager.Instance.CurrentRoomCode, joinCode);
        Task finished = await Task.WhenAny(completion.Task, Task.Delay(10000));
        RoomClient.Instance.OnSetRelayCodeComplete -= Handler;
        return finished == completion.Task && completion.Task.Result;
    }

    private async Task<string> WaitForRelayCode()
    {
        if (!string.IsNullOrEmpty(GameManager.Instance.CurrentRelayJoinCode))
            return GameManager.Instance.CurrentRelayJoinCode;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var completion = new TaskCompletionSource<RoomClient.RoomDetailsResult>();
            void Handler(RoomClient.RoomDetailsResult result) => completion.TrySetResult(result);
            RoomClient.Instance.OnGetRoomDetailsComplete += Handler;
            RoomClient.Instance.GetRoomDetails(GameManager.Instance.CurrentRoomCode);
            await Task.WhenAny(completion.Task, Task.Delay(1000));
            RoomClient.Instance.OnGetRoomDetailsComplete -= Handler;
            if (completion.Task.IsCompleted && completion.Task.Result.success && completion.Task.Result.room != null)
            {
                string code = completion.Task.Result.room.relayJoinCode;
                if (!string.IsNullOrEmpty(code))
                {
                    GameManager.Instance.SetRelayJoinCode(code);
                    return code;
                }
            }
            await Task.Delay(500);
        }
        return null;
    }

    private Vector3 GetSpawnPosition(ulong clientId = 0)
    {
        Transform spawnPoint = GameObject.Find("SpawnPoint")?.transform;
        if (spawnPoint != null)
        {
            return spawnPoint.position + Vector3.right * ((clientId % 4) * 1.5f);
        }

        // Default spawn position
        return Vector3.right * ((clientId % 4) * 1.5f);
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
