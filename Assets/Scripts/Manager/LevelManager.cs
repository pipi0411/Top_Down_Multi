using System;
using System.Collections.Generic;
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
    private GameObject currentDungeonGO;
    
    private void Awake()
    {
        Instance = this;
        boxBudgetInitialized = false;
    }

    private void  Start()
    {
        CreateDungeon();
    }

    private void CreateDungeon()
    {
      currentDungeonGO = Instantiate(dungeonLibrary.Levels[currentLevelIndex].Dungeons[currentDungeonIndex], transform);  
      currentDungeonIndex++;
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
