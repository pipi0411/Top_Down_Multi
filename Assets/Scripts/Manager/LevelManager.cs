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

        if (currentDungeonGO != null)
            Destroy(currentDungeonGO);

        currentRoom = null;
        closedRooms.Clear();
        boxBudgetInitialized = false;
        networkEntitySequence = 0;

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

        if (currentDungeonGO != null)
            Destroy(currentDungeonGO);

        currentRoom = null;
        closedRooms.Clear();
        boxBudgetInitialized = false;
        networkEntitySequence = 0;
        currentLevelIndex = targetLevel;
        currentDungeonIndex = targetDungeon;

        return TryInstantiateCurrentDungeon();
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
