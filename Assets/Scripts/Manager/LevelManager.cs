using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("Config")]
    [SerializeField] private RoomTemplate roomTemplates;
    [SerializeField] private DungeonLibrary dungeonLibrary;

    [Header("Map Box Limit")]
    [SerializeField] private int minBoxesPerMap = 8;
    [SerializeField] private int maxBoxesPerMap = 15;

    private int remainingBoxBudget;
    private int remainingBoxRooms;
    private bool boxBudgetInitialized;

    public RoomTemplate RoomTemplates => roomTemplates;
    public DungeonLibrary DungeonLibrary => dungeonLibrary;

    private Room currentRoom;
    private readonly HashSet<Room> closedRooms = new HashSet<Room>();
    private int currentLevelIndex;
    private int currentDungeonIndex;
    private int activeLevelIndex;
    private int activeDungeonIndex;
    private GameObject currentDungeonGO;
    private int networkEntitySequence;
    private int activeRoomWaveIndex;
    private readonly Dictionary<Room, EnemyWaveData> roomWaveAssignments = new Dictionary<Room, EnemyWaveData>();

    public int ActiveLevelIndex => activeLevelIndex;
    public int ActiveDungeonIndex => activeDungeonIndex;
    
    private void Awake()
    {
        Instance = this;
        boxBudgetInitialized = false;
        MultiplayerGameplaySync.Ensure();
    }

    private void  Start()
    {
        if (SaveGameManager.IsContinueLoading)
        {
            Debug.Log("[LevelManager] Continue loading is active. Skip default dungeon spawn.");
            return;
        }

        CreateDungeon();
    }

    private void CreateDungeon()
    {
        if (!TryInstantiateCurrentDungeon())
            Debug.LogWarning("[LevelManager] Could not create dungeon. Check DungeonLibrary setup.");
    }

    public bool LoadNextDungeonFromPortal(Vector3 playerArrivalOffset)
    {
        if (dungeonLibrary == null || dungeonLibrary.Levels == null || dungeonLibrary.Levels.Length == 0)
        {
            Debug.LogWarning("[LevelManager] Cannot load next dungeon: DungeonLibrary is missing.");
            return false;
        }

        if (!HasNextDungeon())
        {
            Debug.Log("[LevelManager] No next dungeon available.");
            return false;
        }

        ClearCurrentDungeon();

        currentRoom = null;
        closedRooms.Clear();
        roomWaveAssignments.Clear();
        boxBudgetInitialized = false;
        networkEntitySequence = 0;
        activeRoomWaveIndex = 0;

        if (!TryInstantiateCurrentDungeon())
            return false;

        GameAudioManager.Instance?.PlayRandomMapBgm();

        Vector3 spawnPosition = GetSpawnPointPosition() + playerArrivalOffset;
        TeleportLocalPlayersTo(spawnPosition);

        foreach (TeleportGate gate in FindObjectsByType<TeleportGate>(FindObjectsInactive.Exclude))
        {
            if (gate != null && gate.PlayReverseOnArrival)
            {
                gate.PlayArrivalReverse();
                break;
            }
        }

        return true;
    }

    private bool TryInstantiateCurrentDungeon()
    {
        if (dungeonLibrary == null || dungeonLibrary.Levels == null || dungeonLibrary.Levels.Length == 0)
            return false;

        currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, dungeonLibrary.Levels.Length - 1);
        Level level = dungeonLibrary.Levels[currentLevelIndex];
        if (level == null || level.Dungeons == null || level.Dungeons.Length == 0)
            return false;

        if (currentDungeonIndex >= level.Dungeons.Length)
        {
            if (currentLevelIndex + 1 >= dungeonLibrary.Levels.Length)
                return false;

            currentLevelIndex++;
            currentDungeonIndex = 0;
            level = dungeonLibrary.Levels[currentLevelIndex];
            if (level == null || level.Dungeons == null || level.Dungeons.Length == 0)
                return false;
        }

        GameObject dungeonPrefab = level.Dungeons[currentDungeonIndex];
        if (dungeonPrefab == null)
            return false;

        int mapOrdinal = currentLevelIndex * 1000 + currentDungeonIndex;
        networkEntitySequence = 0;
        activeRoomWaveIndex = 0;
        roomWaveAssignments.Clear();
        UnityEngine.Random.InitState(73001 + mapOrdinal);
        activeLevelIndex = currentLevelIndex;
        activeDungeonIndex = currentDungeonIndex;
        currentDungeonGO = Instantiate(dungeonPrefab, transform);
        currentDungeonIndex++;
        return true;
    }

    public bool LoadSavedDungeon(int levelIndex, int dungeonIndex)
    {
        if (dungeonLibrary == null || dungeonLibrary.Levels == null || dungeonLibrary.Levels.Length == 0)
            return false;

        int targetLevel = Mathf.Clamp(levelIndex, 0, dungeonLibrary.Levels.Length - 1);
        Level level = dungeonLibrary.Levels[targetLevel];
        if (level == null || level.Dungeons == null || level.Dungeons.Length == 0)
            return false;

        int targetDungeon = Mathf.Clamp(dungeonIndex, 0, level.Dungeons.Length - 1);

        ClearCurrentDungeon();

        currentRoom = null;
        closedRooms.Clear();
        roomWaveAssignments.Clear();
        boxBudgetInitialized = false;
        networkEntitySequence = 0;
        activeRoomWaveIndex = 0;
        currentLevelIndex = targetLevel;
        currentDungeonIndex = targetDungeon;

        return TryInstantiateCurrentDungeon();
    }

    private void ClearCurrentDungeon()
    {
        HashSet<GameObject> objectsToClear = new HashSet<GameObject>();

        if (currentDungeonGO != null)
        {
            objectsToClear.Add(currentDungeonGO);
            currentDungeonGO.SetActive(false);
            Destroy(currentDungeonGO);
            currentDungeonGO = null;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            objectsToClear.Add(child.gameObject);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        CollectRuntimeWorldObjects(objectsToClear);
        foreach (GameObject worldObject in objectsToClear)
        {
            if (worldObject == null || worldObject == gameObject)
                continue;

            if (worldObject.GetComponentInParent<PlayerHealth>() != null)
                continue;

            worldObject.SetActive(false);
            Destroy(worldObject);
        }
    }

    private void CollectRuntimeWorldObjects(HashSet<GameObject> objectsToClear)
    {
        if (objectsToClear == null)
            return;

        AddComponentsToClear(FindObjectsByType<Room>(FindObjectsInactive.Include), objectsToClear);
        AddComponentsToClear(FindObjectsByType<Door>(FindObjectsInactive.Include), objectsToClear);
        AddComponentsToClear(FindObjectsByType<TeleportGate>(FindObjectsInactive.Include), objectsToClear);
        AddComponentsToClear(FindObjectsByType<BreakableBox>(FindObjectsInactive.Include), objectsToClear);
        AddComponentsToClear(FindObjectsByType<PickupItem>(FindObjectsInactive.Include), objectsToClear);
        AddComponentsToClear(FindObjectsByType<WeaponPickup>(FindObjectsInactive.Include), objectsToClear);
        AddComponentsToClear(FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include), objectsToClear);
        AddComponentsToClear(FindObjectsByType<EnemyStateMachine>(FindObjectsInactive.Include), objectsToClear);
        AddComponentsToClear(FindObjectsByType<EnemyProjectile>(FindObjectsInactive.Include), objectsToClear);
        AddComponentsToClear(FindObjectsByType<Projectile>(FindObjectsInactive.Include), objectsToClear);
        AddComponentsToClear(FindObjectsByType<RoomTrapSpawner>(FindObjectsInactive.Include), objectsToClear);
    }

    private void AddComponentsToClear<T>(T[] components, HashSet<GameObject> objectsToClear) where T : Component
    {
        if (components == null)
            return;

        foreach (T component in components)
        {
            if (component == null || component.gameObject == gameObject)
                continue;

            if (component.GetComponentInParent<PlayerHealth>() != null)
                continue;

            objectsToClear.Add(component.gameObject);
        }
    }

    public string NextNetworkEntityId(string prefix)
    {
        networkEntitySequence++;
        int levelNumber = Mathf.Max(0, currentLevelIndex);
        int dungeonNumber = Mathf.Max(0, currentDungeonIndex - 1);
        return $"{prefix}_{levelNumber}_{dungeonNumber}_{networkEntitySequence}";
    }

    public bool ShouldRunWorldSimulation()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }

    public EnemyWaveData GetActiveDungeonWaveData()
    {
        if (dungeonLibrary == null || dungeonLibrary.Levels == null)
            return null;

        if (activeLevelIndex < 0 || activeLevelIndex >= dungeonLibrary.Levels.Length)
            return null;

        Level level = dungeonLibrary.Levels[activeLevelIndex];
        if (level == null || level.WaveDataByDungeon == null)
            return null;

        if (activeDungeonIndex < 0 || activeDungeonIndex >= level.WaveDataByDungeon.Length)
            return null;

        return level.WaveDataByDungeon[activeDungeonIndex];
    }

    public EnemyWaveData GetWaveDataForRoom(Room room)
    {
        if (room == null)
            return GetNextActiveDungeonRoomWaveData();

        if (roomWaveAssignments.TryGetValue(room, out EnemyWaveData assignedWaveData))
            return assignedWaveData;

        EnemyWaveData waveData = GetNextActiveDungeonRoomWaveData();
        roomWaveAssignments[room] = waveData;
        return waveData;
    }

    private EnemyWaveData GetNextActiveDungeonRoomWaveData()
    {
        EnemyWaveData fallbackWaveData = GetActiveDungeonWaveData();

        if (dungeonLibrary == null || dungeonLibrary.Levels == null)
            return fallbackWaveData;

        if (activeLevelIndex < 0 || activeLevelIndex >= dungeonLibrary.Levels.Length)
            return fallbackWaveData;

        Level level = dungeonLibrary.Levels[activeLevelIndex];
        if (level == null || level.WaveSetsByDungeon == null)
            return fallbackWaveData;

        if (activeDungeonIndex < 0 || activeDungeonIndex >= level.WaveSetsByDungeon.Length)
            return fallbackWaveData;

        DungeonWaveSet waveSet = level.WaveSetsByDungeon[activeDungeonIndex];
        if (waveSet == null || waveSet.RoomWaves == null || waveSet.RoomWaves.Length == 0)
            return fallbackWaveData;

        int waveIndex = Mathf.Clamp(activeRoomWaveIndex, 0, waveSet.RoomWaves.Length - 1);
        activeRoomWaveIndex++;

        return waveSet.RoomWaves[waveIndex] != null ? waveSet.RoomWaves[waveIndex] : fallbackWaveData;
    }

    private bool HasNextDungeon()
    {
        if (dungeonLibrary == null || dungeonLibrary.Levels == null || dungeonLibrary.Levels.Length == 0)
            return false;

        if (currentLevelIndex < 0 || currentLevelIndex >= dungeonLibrary.Levels.Length)
            return false;

        Level level = dungeonLibrary.Levels[currentLevelIndex];
        if (level != null && level.Dungeons != null && currentDungeonIndex < level.Dungeons.Length)
            return true;

        return currentLevelIndex + 1 < dungeonLibrary.Levels.Length;
    }

    private Vector3 GetSpawnPointPosition()
    {
        Transform spawnPoint = GameObject.Find("SpawnPoint")?.transform;
        return spawnPoint != null ? spawnPoint.position : Vector3.zero;
    }

    private void TeleportLocalPlayersTo(Vector3 position)
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        foreach (PlayerHealth player in players)
        {
            if (player == null)
                continue;

            player.transform.position = position;
        }
    }
    private void  PlayerEnterEventCallback(Room room)
    {
        if (room == null)
        {
            return;
        }

        currentRoom = room;
        if (currentRoom.RoomCompleted == false && closedRooms.Add(currentRoom))
        {
            currentRoom.LockDoors();
            currentRoom.BeginEncounter();
        }
    }

    public void ResetCurrentRoomEncounter()
    {
        if (currentRoom == null || currentRoom.RoomCompleted) return;

        closedRooms.Remove(currentRoom);
        currentRoom.ResetEncounter();
    }
    private void OnEnable()
    {
        Room.OnPlayerEnterEvent += PlayerEnterEventCallback;
    }

    private void OnDisable()
    {
        Room.OnPlayerEnterEvent -= PlayerEnterEventCallback;
    }

    public int RequestBoxCountForRoom(int candidateCount)
    {
        EnsureBoxBudgetInitialized();

        remainingBoxRooms = Mathf.Max(0, remainingBoxRooms - 1);

        if (candidateCount <= 0 || remainingBoxBudget <= 0)
            return 0;

        int roomsLeftIncludingThis = remainingBoxRooms + 1;
        int fairShare = Mathf.CeilToInt(remainingBoxBudget / (float)roomsLeftIncludingThis);
        int spawnCount = Mathf.Clamp(fairShare, 1, Mathf.Min(candidateCount, remainingBoxBudget));

        remainingBoxBudget -= spawnCount;
        return spawnCount;
    }

    private void EnsureBoxBudgetInitialized()
    {
        if (boxBudgetInitialized) return;

        int min = Mathf.Max(0, minBoxesPerMap);
        int max = Mathf.Max(min, maxBoxesPerMap);
        remainingBoxBudget = UnityEngine.Random.Range(min, max + 1);

        remainingBoxRooms = 0;
        Room[] rooms = FindObjectsByType<Room>(FindObjectsInactive.Include);
        foreach (Room room in rooms)
        {
            if (room != null && room.CanSpawnBoxes)
                remainingBoxRooms++;
        }

        remainingBoxRooms = Mathf.Max(1, remainingBoxRooms);
        boxBudgetInitialized = true;
    }
}
