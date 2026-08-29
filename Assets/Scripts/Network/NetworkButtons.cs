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
    [SerializeField] private float localPlayerSpawnTimeout = 15f;
    private bool isNetworkStarted = false;
    private bool relayHostConfigured = false;
    private float nextGameplayHeartbeat;
    private Coroutine enterGameWhenPlayerReadyCoroutine;
    private readonly Dictionary<ulong, string> approvedClientCharacters = new Dictionary<ulong, string>();

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

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            isNetworkStarted = false;

        // Check if we're in the gameplay scene
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.GameStarting)
        {
            if (!GameManager.Instance.IsMultiplayer)
            {
                Debug.Log("[NetworkButtons] Single-player scene loaded. Spawning offline player.");
                StartCoroutine(StartOfflineSinglePlayer());
                return;
            }

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
        {
            if (!string.IsNullOrEmpty(GameManager.Instance.RoomSelectedCharacter))
                return GameManager.Instance.RoomSelectedCharacter;

            if (!string.IsNullOrEmpty(GameManager.Instance.SelectedCharacter))
                return GameManager.Instance.SelectedCharacter;

            Debug.LogWarning("[NetworkButtons] RoomSelectedCharacter is empty. Connection approval may fail.");
            return null;
        }

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
        approvedClientCharacters[request.ClientNetworkId] = clientCharacter;

        GameObject characterPrefab = CharacterPrefabManager.Instance.GetPrefabForCharacter(clientCharacter);
        if (characterPrefab == null || characterPrefab.GetComponent<NetworkObject>() == null)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = $"No valid NetworkObject prefab found for character '{clientCharacter}'.";
            Debug.LogError($"[NetworkButtons.ApprovalCheck] {response.Reason}");
            return;
        }

        // Luôn approve kết nối
        response.Approved = true;
        
        response.CreatePlayerObject = false;
        response.Position = GetSpawnPosition(request.ClientNetworkId);
        response.Rotation = Quaternion.identity;

        Debug.Log($"[NetworkButtons.ApprovalCheck] Approved '{clientCharacter}'. Server will spawn prefab '{characterPrefab.name}'.");
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

        if (GameManager.Instance.IsMultiplayer)
            Debug.Log($"[NetworkButtons] Client preparing Relay for room '{GameManager.Instance.CurrentRoomCode}', cachedRelayRoom='{GameManager.Instance.CurrentRelayRoomCode}', cachedJoinCode='{GameManager.Instance.CurrentRelayJoinCode}'");

        if (GameManager.Instance.IsMultiplayer && !await ConfigureRelayClient())
            return;

        Debug.Log("[NetworkButtons] Starting as Client...");

        string selectedCharacter = GetCharacterForNetworkStartup();
        Debug.Log($"[NetworkButtons] Client starting with character: {selectedCharacter}");
        if (string.IsNullOrEmpty(selectedCharacter))
        {
            Debug.LogError("[NetworkButtons] Cannot start client because selected character is empty.");
            isNetworkStarted = false;
            return;
        }

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

        if (GameManager.Instance.IsMultiplayer && !relayHostConfigured && !await ConfigureRelayHost())
            return;

        Debug.Log("[NetworkButtons] Starting as Host...");

        string selectedCharacter = GetCharacterForNetworkStartup();
        Debug.Log($"[NetworkButtons] Host starting with character: {selectedCharacter}");
        if (string.IsNullOrEmpty(selectedCharacter))
        {
            Debug.LogError("[NetworkButtons] Cannot start host because selected character is empty.");
            isNetworkStarted = false;
            return;
        }

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
            StartEnterGameWhenPlayerReady("Host");
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
            string connectionType = GetRelayConnectionType();
            ConfigureTransportForRelay(transport, connectionType);
            transport.SetRelayServerData(allocation.ToRelayServerData(connectionType));
            GameManager.Instance.SetRelayJoinCode(joinCode, GameManager.Instance.CurrentRoomCode);
            bool published = await PublishRelayCode(joinCode);
            if (!published) throw new InvalidOperationException("Could not publish Relay join code to room.");
            relayHostConfigured = true;
            Debug.Log($"[Relay] Host allocation ready. Join code: {joinCode}, connection: {connectionType}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Relay] Host setup failed: {exception.Message}");
            isNetworkStarted = false;
            relayHostConfigured = false;
            return false;
        }
    }

    public async Task<bool> PrepareRelayHostBeforeGameplay()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsMultiplayer || !GameManager.Instance.IsHost)
            return true;

        if (relayHostConfigured &&
            !string.IsNullOrEmpty(GameManager.Instance.CurrentRelayJoinCode) &&
            string.Equals(GameManager.Instance.CurrentRelayRoomCode, GameManager.Instance.CurrentRoomCode, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[Relay] Reusing prepared host Relay join code: {GameManager.Instance.CurrentRelayJoinCode}");
            return true;
        }

        Debug.Log($"[Relay] Preparing host Relay before starting room '{GameManager.Instance.CurrentRoomCode}'.");
        return await ConfigureRelayHost();
    }

    private void Update()
    {
        if (!isNetworkStarted || GameManager.Instance == null || !GameManager.Instance.IsMultiplayer ||
            !GameManager.Instance.IsHost || Time.unscaledTime < nextGameplayHeartbeat) return;
        if (RoomClient.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
            RoomClient.Instance.SendHeartbeat(GameManager.Instance.CurrentRoomCode);
        nextGameplayHeartbeat = Time.unscaledTime + 10f;
    }

    public void ResetNetworkStartupState()
    {
        isNetworkStarted = false;
        relayHostConfigured = false;
        nextGameplayHeartbeat = 0f;
        approvedClientCharacters.Clear();
        StopEnterGameWhenPlayerReady();
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
            string connectionType = GetRelayConnectionType();
            ConfigureTransportForRelay(transport, connectionType);
            transport.SetRelayServerData(allocation.ToRelayServerData(connectionType));
            Debug.Log($"[Relay] Client joined allocation: {joinCode}, connection: {connectionType}");
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

    private string GetRelayConnectionType()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return "wss";
#else
        return "dtls";
#endif
    }

    private void ConfigureTransportForRelay(UnityTransport transport, string connectionType)
    {
        if (transport == null) return;

        bool useWebSockets = string.Equals(connectionType, "wss", StringComparison.OrdinalIgnoreCase)
            || string.Equals(connectionType, "ws", StringComparison.OrdinalIgnoreCase);
        transport.UseWebSockets = useWebSockets;
        Debug.Log($"[Relay] UnityTransport UseWebSockets={transport.UseWebSockets}");
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
        if (!string.IsNullOrEmpty(GameManager.Instance.CurrentRelayJoinCode) &&
            string.Equals(GameManager.Instance.CurrentRelayRoomCode, GameManager.Instance.CurrentRoomCode, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[Relay] Using cached room join code: {GameManager.Instance.CurrentRelayJoinCode}");
            return GameManager.Instance.CurrentRelayJoinCode;
        }

        Debug.Log($"[Relay] Waiting for relay code from room '{GameManager.Instance.CurrentRoomCode}'.");
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
                    GameManager.Instance.SetRelayJoinCode(code, GameManager.Instance.CurrentRoomCode);
                    Debug.Log($"[Relay] Received room join code on attempt {attempt + 1}: {code}");
                    return code;
                }
            }
            else if (completion.Task.IsCompleted && !completion.Task.Result.success)
            {
                Debug.LogWarning($"[Relay] Room details failed while waiting for relay code. Room='{GameManager.Instance.CurrentRoomCode}', Error='{completion.Task.Result.error}'");
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
    private System.Collections.IEnumerator StartOfflineSinglePlayer()
    {
        isNetworkStarted = false;
        relayHostConfigured = false;
        StopEnterGameWhenPlayerReady();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            yield return null;
        }

        yield return null;

        PlayerHealth existingPlayer = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
        if (existingPlayer != null)
        {
            Debug.Log($"[NetworkButtons] Offline single-player already has player '{existingPlayer.name}'. Entering InGame.");
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.GameStarting)
                GameManager.Instance.ChangeState(GameManager.GameState.InGame);
            yield break;
        }

        string selectedCharacter = GetCharacterForNetworkStartup();
        if (string.IsNullOrEmpty(selectedCharacter))
        {
            Debug.LogError("[NetworkButtons] Cannot start offline single-player because selected character is empty.");
            yield break;
        }

        CharacterPrefabManager prefabManager = CharacterPrefabManager.Instance;
        if (prefabManager == null)
        {
            Debug.LogError("[NetworkButtons] CharacterPrefabManager not found. Cannot spawn offline single-player.");
            yield break;
        }

        GameObject prefab = prefabManager.GetPrefabForCharacter(selectedCharacter);
        if (prefab == null)
        {
            Debug.LogError($"[NetworkButtons] Cannot spawn offline single-player. No prefab for character '{selectedCharacter}'.");
            yield break;
        }

        Vector3 position = GetSpawnPosition(0);
        GameObject playerInstance = Instantiate(prefab, position, Quaternion.identity);
        playerInstance.name = $"Player_{selectedCharacter}";

        Debug.Log($"[NetworkButtons] Offline single-player spawned '{selectedCharacter}' using prefab '{prefab.name}'.");

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.GameStarting)
            GameManager.Instance.ChangeState(GameManager.GameState.InGame);
    }

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

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(EnsureServerPlayerObjectSpawned(clientId));
        }

        if (NetworkManager.Singleton != null
            && clientId == NetworkManager.Singleton.LocalClientId
            && GameManager.Instance != null
            && GameManager.Instance.CurrentState == GameManager.GameState.GameStarting)
        {
            Debug.Log("[NetworkButtons] Local client connected. Waiting for local PlayerObject before entering InGame.");
            StartEnterGameWhenPlayerReady("Client");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetworkButtons] Client disconnected: {clientId}");

        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            StopEnterGameWhenPlayerReady();
            isNetworkStarted = false;
            Debug.LogWarning("[NetworkButtons] Local client disconnected before/while in game. Check Relay, approval reason, and selected character.");
        }

        approvedClientCharacters.Remove(clientId);
    }

    private System.Collections.IEnumerator EnsureServerPlayerObjectSpawned(ulong clientId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer)
            yield break;

        yield return null;

        if (!TryGetConnectedClient(manager, clientId, out NetworkClient connectedClient) || connectedClient.PlayerObject != null)
            yield break;

        string character = approvedClientCharacters.TryGetValue(clientId, out string approvedCharacter)
            ? approvedCharacter
            : GetCharacterForNetworkStartup();

        GameObject prefab = CharacterPrefabManager.Instance.GetPrefabForCharacter(character);
        if (prefab == null)
        {
            Debug.LogError($"[NetworkButtons] Fallback spawn failed: no prefab for client {clientId}, character '{character}'.");
            yield break;
        }

        NetworkObject prefabNetworkObject = prefab.GetComponent<NetworkObject>();
        if (prefabNetworkObject == null)
        {
            Debug.LogError($"[NetworkButtons] Fallback spawn failed: prefab '{prefab.name}' has no NetworkObject.");
            yield break;
        }

        Vector3 position = GetSpawnPosition(clientId);
        GameObject playerInstance = Instantiate(prefab, position, Quaternion.identity);
        NetworkObject playerNetworkObject = playerInstance.GetComponent<NetworkObject>();
        playerNetworkObject.SpawnAsPlayerObject(clientId, false);
        Debug.LogWarning($"[NetworkButtons] Fallback spawned player for client {clientId} as '{character}' using prefab '{prefab.name}'.");
    }

    private bool TryGetConnectedClient(NetworkManager manager, ulong clientId, out NetworkClient client)
    {
        client = null;
        return manager != null && manager.ConnectedClients.TryGetValue(clientId, out client);
    }

    private void StartEnterGameWhenPlayerReady(string roleLabel)
    {
        StopEnterGameWhenPlayerReady();
        enterGameWhenPlayerReadyCoroutine = StartCoroutine(EnterGameWhenLocalPlayerReady(roleLabel));
    }

    private void StopEnterGameWhenPlayerReady()
    {
        if (enterGameWhenPlayerReadyCoroutine == null)
            return;

        StopCoroutine(enterGameWhenPlayerReadyCoroutine);
        enterGameWhenPlayerReadyCoroutine = null;
    }

    private System.Collections.IEnumerator EnterGameWhenLocalPlayerReady(string roleLabel)
    {
        float deadline = Time.unscaledTime + Mathf.Max(1f, localPlayerSpawnTimeout);

        while (Time.unscaledTime < deadline)
        {
            NetworkObject localPlayerObject = GetLocalPlayerObject();
            if (localPlayerObject != null)
            {
                Debug.Log($"[NetworkButtons] {roleLabel} local PlayerObject ready: {localPlayerObject.name}. Entering InGame.");
                enterGameWhenPlayerReadyCoroutine = null;

                if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.GameStarting)
                    GameManager.Instance.ChangeState(GameManager.GameState.InGame);

                yield break;
            }

            yield return null;
        }

        enterGameWhenPlayerReadyCoroutine = null;
        isNetworkStarted = false;
        Debug.LogError($"[NetworkButtons] Timed out waiting for local PlayerObject as {roleLabel}. The client likely connected but player spawn was denied or prefab/hash is not registered in the WebGL build.");
    }

    private NetworkObject GetLocalPlayerObject()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
            return null;

        if (manager.LocalClient != null && manager.LocalClient.PlayerObject != null)
            return manager.LocalClient.PlayerObject;

        if (manager.IsServer && manager.ConnectedClients.TryGetValue(manager.LocalClientId, out NetworkClient client))
            return client.PlayerObject;

        return null;
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
