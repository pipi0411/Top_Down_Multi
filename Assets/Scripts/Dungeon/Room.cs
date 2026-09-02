using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public enum RoomType
{
    RoomFree,
    RoomEntrance,
    RoomEnemy,
    RoomBoss
}
public class Room : MonoBehaviour
{
    public static event Action<Room> OnPlayerEnterEvent;
    public static event Action<Room> OnCombatStartedEvent;
    public static event Action<Room> OnCombatEndedEvent;
    [Header("Config")]
    [SerializeField] private bool useDebug;
    [SerializeField] private RoomType roomType;

    [Header("Grid")]
    [SerializeField] private Tilemap extraTilemap;

    [Header("Enemy Waves")]
    [SerializeField] private EnemyWaveData waveData;
    [HideInInspector]
    [SerializeField] private EnemyWaveSpawner waveSpawner;

    [Header("Reward Chest")]
    [SerializeField] private GameObject weaponChestPrefab;
    [SerializeField] private int minRewardChests = 1;
    [SerializeField] private int maxRewardChests = 2;
    [SerializeField] private float chestSpawnTilePadding = 1.5f;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;

    [Header("Boss End Game")]
    [SerializeField] private string endGameSceneName = "EndGame";
    [SerializeField] private float bossEndGameDelay = 5f;

    [Header("Doors")]
    [SerializeField] private Transform[] posDoorNS;
    [SerializeField] private Transform[] posDoorWE;

    public bool RoomCompleted { get; set; }

    // Position (Key) - Free/ Not Free (Value)
    private Dictionary<Vector3, bool> tiles = new Dictionary<Vector3, bool>();
    private List<Door> doorList = new List<Door>();
    private readonly List<BossManager> activeBosses = new List<BossManager>();
    private readonly List<BoxSpawnPoint> pendingBoxPoints = new List<BoxSpawnPoint>();
    private readonly List<WeaponChest> rewardChests = new List<WeaponChest>();
    private NetworkedWorldEntity roomEntity;
    private Coroutine bossEndGameCoroutine;
    private bool encounterStarted;

    public bool CanSpawnBoxes => !NormalRoom();
    public bool HasEnemyWave => IsCombatRoom() && waveData != null;
    public string SaveId => roomEntity != null ? roomEntity.NetworkId : gameObject.name;

    private void Start()
    {
        EnsureRoomEntity();
        GetTiles();
        CreateDoors();
        EnsureWaveSpawner(false);
        GenerateRoomUsingTemplate();

        if (SaveGameManager.IsRoomCompleted(SaveId))
        {
            RoomCompleted = true;
            OpenDoors();
            SpawnRewardChestsIfNeeded();
        }
    }
    
  private void GetTiles()
  {
    if (NormalRoom())
    {
        return;
    }

    tiles.Clear(); // Đảm bảo xóa dữ liệu cũ trước

    foreach (Vector3Int tilePos in extraTilemap.cellBounds.allPositionsWithin)
    {
        // Lấy vị trí world của tile
        if (!extraTilemap.HasTile(tilePos)) continue;

        Vector3 worldPos = extraTilemap.CellToWorld(tilePos);
        
        // Điều chỉnh về tâm của tile (rất quan trọng)
        Vector3 centerPos = worldPos + new Vector3(0.5f, 0.5f, 0f);

        // Thêm vào dictionary với vị trí đã chỉnh tâm
        tiles.Add(centerPos, true);
    }
    }

    private void  GenerateRoomUsingTemplate() 
    {
        if (NormalRoom())
        {
            return;
        }

        int randomIndex = Random.Range(0, LevelManager.Instance.RoomTemplates.Templates.Length);
        Texture2D texture = LevelManager.Instance.RoomTemplates.Templates[randomIndex];
        List<Vector3> positions = new List<Vector3>(tiles.Keys);
        pendingBoxPoints.Clear();

        int totalPositions = Mathf.Min(texture.width * texture.height, positions.Count);
        for (int a = 0; a < totalPositions; a++)
        {
                int x = a % texture.width;
                int y = a / texture.width;
                Color pixelColor = texture.GetPixel(x, y);
                foreach (RoomProp prop in LevelManager.Instance.RoomTemplates.PropsData)
                {
                    if(ColorsMatch(pixelColor, prop.PropColor))
                    {
                        Vector3 spawnPosition = new Vector3(positions[a].x, positions[a].y, 0f);
                        if (IsBoxProp(prop))
                        {
                            pendingBoxPoints.Add(new BoxSpawnPoint(spawnPosition, x, y, prop));
                            continue;
                        }

                        if (prop == null || prop.ProPrefab == null) continue;

                        GameObject propInstance = Instantiate(prop.ProPrefab, extraTilemap.transform);
                        propInstance.transform.position = spawnPosition;
                        AssignNetworkId(propInstance, "Prop");
                        MarkTileOccupied(spawnPosition);
                    }
                }
        }

        EnsureBoxSpawnPoints(positions);
        SpawnBoxesInPattern();
    }

    private bool ColorsMatch(Color a, Color b)
    {
        Color32 colorA = a;
        Color32 colorB = b;
        return colorA.r == colorB.r && colorA.g == colorB.g && colorA.b == colorB.b && colorA.a == colorB.a;
    }

    private bool IsBoxProp(RoomProp prop)
    {
        return prop != null && !string.IsNullOrEmpty(prop.Name) && prop.Name.ToLowerInvariant().Contains("box");
    }

    private void EnsureBoxSpawnPoints(List<Vector3> positions)
    {
        if (pendingBoxPoints.Count > 0) return;
        if (positions == null || positions.Count == 0) return;

        RoomProp boxProp = FindBoxProp();
        if (boxProp == null || boxProp.ProPrefab == null) return;

        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 position = positions[i];
            Vector3Int cell = extraTilemap != null ? extraTilemap.WorldToCell(position) : Vector3Int.RoundToInt(position);
            pendingBoxPoints.Add(new BoxSpawnPoint(new Vector3(position.x, position.y, 0f), cell.x, cell.y, boxProp));
        }
    }

    private RoomProp FindBoxProp()
    {
        if (LevelManager.Instance == null || LevelManager.Instance.RoomTemplates == null) return null;

        foreach (RoomProp prop in LevelManager.Instance.RoomTemplates.PropsData)
        {
            if (IsBoxProp(prop) && prop.ProPrefab != null)
                return prop;
        }

        return null;
    }

    private void SpawnBoxesInPattern()
    {
        int targetCount = LevelManager.Instance != null
            ? LevelManager.Instance.RequestBoxCountForRoom(pendingBoxPoints.Count)
            : Mathf.Min(15, pendingBoxPoints.Count);

        if (targetCount <= 0) return;

        List<BoxSpawnPoint> candidates = new List<BoxSpawnPoint>(pendingBoxPoints);
        BoxSpawnPoint center = candidates[Random.Range(0, candidates.Count)];
        BoxPattern pattern = (BoxPattern)Random.Range(0, System.Enum.GetValues(typeof(BoxPattern)).Length);
        List<BoxSpawnPoint> selected = SelectBoxPattern(candidates, center, pattern, targetCount);

        if (selected.Count < Mathf.Min(targetCount, candidates.Count))
        {
            candidates.Sort((a, b) => Random.value < 0.5f ? -1 : 1);
            foreach (BoxSpawnPoint point in candidates)
            {
                if (selected.Count >= targetCount) break;
                if (selected.Contains(point)) continue;
                if (!HasAdjacentSelectedBox(point, selected)) selected.Add(point);
            }
        }

        foreach (BoxSpawnPoint point in selected)
            SpawnBox(point);
    }

    private List<BoxSpawnPoint> SelectBoxPattern(List<BoxSpawnPoint> candidates, BoxSpawnPoint center, BoxPattern pattern, int targetCount)
    {
        List<BoxSpawnPoint> selected = new List<BoxSpawnPoint>();
        foreach (BoxSpawnPoint point in candidates)
        {
            if (selected.Count >= targetCount) break;
            int dx = point.TemplateX - center.TemplateX;
            int dy = point.TemplateY - center.TemplateY;
            if (MatchesPattern(dx, dy, pattern)) selected.Add(point);
        }

        return selected;
    }

    private bool MatchesPattern(int dx, int dy, BoxPattern pattern)
    {
        int absX = Mathf.Abs(dx);
        int absY = Mathf.Abs(dy);

        switch (pattern)
        {
            case BoxPattern.Ring:
                return absX <= 2 && absY <= 2 && (absX == 2 || absY == 2);
            case BoxPattern.Cross:
                return (absX == 0 && absY <= 3) || (absY == 0 && absX <= 3);
            case BoxPattern.ZigZag:
                return absX <= 4 && absY <= 2 && (dx + dy) % 2 == 0;
            case BoxPattern.DoubleLine:
                return absX <= 3 && (dy == 0 || dy == 1);
            case BoxPattern.Corners:
                return absX <= 3 && absY <= 3 && absX >= 2 && absY >= 2;
            case BoxPattern.LooseCluster:
                return absX <= 3 && absY <= 3 && (absX + absY) % 2 == 0;
            default:
                return false;
        }
    }

    private bool HasAdjacentSelectedBox(BoxSpawnPoint point, List<BoxSpawnPoint> selected)
    {
        foreach (BoxSpawnPoint selectedPoint in selected)
        {
            int dx = Mathf.Abs(point.TemplateX - selectedPoint.TemplateX);
            int dy = Mathf.Abs(point.TemplateY - selectedPoint.TemplateY);
            if (dx <= 1 && dy <= 1) return true;
        }

        return false;
    }

    private void SpawnBox(BoxSpawnPoint point)
    {
        if (point.Prop == null || point.Prop.ProPrefab == null) return;

        bool networkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool prefabIsNetworked = point.Prop.ProPrefab.GetComponent<NetworkObject>() != null;
        if (networkActive && prefabIsNetworked && !NetworkManager.Singleton.IsServer)
            return;

        GameObject boxInstance = Instantiate(point.Prop.ProPrefab, extraTilemap.transform);
        boxInstance.transform.position = point.Position;
        AssignNetworkId(boxInstance, "Box");
        MarkTileOccupied(point.Position);

        NetworkObject networkObject = boxInstance.GetComponent<NetworkObject>();
        if (networkObject != null && networkActive && NetworkManager.Singleton.IsServer && !networkObject.IsSpawned)
            networkObject.Spawn(true);
    }

    private void AssignNetworkId(GameObject instance, string prefix)
    {
        if (instance == null || LevelManager.Instance == null) return;
        NetworkedWorldEntity entity = instance.GetComponent<NetworkedWorldEntity>();
        if (entity == null)
            entity = instance.AddComponent<NetworkedWorldEntity>();
        entity.Initialize(LevelManager.Instance.NextNetworkEntityId(prefix));
    }

    private void MarkTileOccupied(Vector3 position)
    {
        if (tiles.ContainsKey(position))
            tiles[position] = false;
    }

    private enum BoxPattern
    {
        Ring,
        Cross,
        ZigZag,
        DoubleLine,
        Corners,
        LooseCluster
    }

    private struct BoxSpawnPoint
    {
        public readonly Vector3 Position;
        public readonly int TemplateX;
        public readonly int TemplateY;
        public readonly RoomProp Prop;

        public BoxSpawnPoint(Vector3 position, int templateX, int templateY, RoomProp prop)
        {
            Position = position;
            TemplateX = templateX;
            TemplateY = templateY;
            Prop = prop;
        }
    }

    public void CloseDoors()
    {
        for (int i = 0; i < doorList.Count; i++)
        {
            doorList[i].ShowCloseAnimation();
        }
    }

    public void LockDoors()
    {
        for (int i = 0; i < doorList.Count; i++)
        {
            doorList[i].LockClosed();
        }
    }

    public void  OpenDoors()
    {
        for (int i = 0; i < doorList.Count; i++)
        {
            doorList[i].UnlockAndOpen();
        }
    }

    public void BeginEncounter()
    {
        if (RoomCompleted || !IsCombatRoom() || encounterStarted) return;

        encounterStarted = true;
        OnCombatStartedEvent?.Invoke(this);
        if (roomType == RoomType.RoomBoss)
        {
            GameAudioManager.Instance?.PlayFinalBossBgm();

            if (TryBeginBossEncounter())
                return;

            Debug.LogWarning($"[Room] Boss room '{name}' started but no BossManager/bossPrefab was available. Encounter will not auto-complete.");
            encounterStarted = false;
            OpenDoors();
            OnCombatEndedEvent?.Invoke(this);
            return;
        }

        EnsureWaveSpawner(true);
        if (waveSpawner != null)
        {
            waveSpawner.SetWaveData(GetEncounterWaveData(true));
            waveSpawner.StartWaves(this);
        }
        else
        {
            CompleteEncounter();
        }
    }

    public void CompleteEncounter()
    {
        if (RoomCompleted) return;

        RoomCompleted = true;
        OpenDoors();
        OnCombatEndedEvent?.Invoke(this);
        SaveGameManager.RecordRoomCompleted(SaveId);
        SpawnRewardChestsIfNeeded();

        if (roomType == RoomType.RoomBoss)
        {
            GameAudioManager.Instance?.PlayWinSong();
            StartBossEndGameCountdown();
        }
    }

    public void ResetEncounter()
    {
        if (!IsCombatRoom()) return;

        RoomCompleted = false;
        encounterStarted = false;
        EnsureWaveSpawner(false);

        if (waveSpawner != null)
            waveSpawner.ResetWaves();

        OpenDoors();
        OnCombatEndedEvent?.Invoke(this);
    }

    private void EnsureWaveSpawner(bool reserveLevelWave)
    {
        if (!IsCombatRoom()) return;

        EnemyWaveData encounterWaveData = GetEncounterWaveData(reserveLevelWave);

        if (waveSpawner == null)
            waveSpawner = GetComponent<EnemyWaveSpawner>();

        if (waveSpawner == null && (roomType == RoomType.RoomEnemy || encounterWaveData != null))
            waveSpawner = gameObject.AddComponent<EnemyWaveSpawner>();

        if (waveSpawner != null)
            waveSpawner.SetWaveData(encounterWaveData);
    }

    private EnemyWaveData GetEncounterWaveData(bool reserveLevelWave)
    {
        if (roomType == RoomType.RoomEnemy && LevelManager.Instance != null)
        {
            EnemyWaveData levelWaveData = reserveLevelWave
                ? LevelManager.Instance.GetWaveDataForRoom(this)
                : LevelManager.Instance.GetActiveDungeonWaveData();
            if (levelWaveData != null)
                return levelWaveData;
        }

        return waveData;
    }

    private void SpawnRewardChestsIfNeeded()
    {
        if (roomType != RoomType.RoomEnemy && roomType != RoomType.RoomBoss) return;
        if (rewardChests.Count > 0) return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
            return;

        int chestCount = GetRewardChestCount();
        List<Vector3> positions = PickRewardChestPositions(chestCount);
        for (int i = 0; i < positions.Count; i++)
        {
            string chestId = $"{SaveId}_RewardChest_{i + 1}";
            SpawnRewardChestLocal(chestId, positions[i]);
            MultiplayerGameplaySync.BroadcastRewardChestSpawned(this, chestId, positions[i]);
        }
    }

    public void ApplyRemoteRewardChestSpawned(string chestId, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(chestId)) return;
        if (NetworkedWorldEntity.TryFind(chestId, out WeaponChest _)) return;

        SpawnRewardChestLocal(chestId, position);
    }

    private void SpawnRewardChestLocal(string chestId, Vector3 position)
    {
        if (weaponChestPrefab == null) return;

        GameObject chestObject = Instantiate(weaponChestPrefab, position, Quaternion.identity, transform);
        NetworkedWorldEntity entity = chestObject.GetComponent<NetworkedWorldEntity>();
        if (entity == null)
            entity = chestObject.AddComponent<NetworkedWorldEntity>();

        entity.Initialize(chestId);

        WeaponChest chest = chestObject.GetComponent<WeaponChest>();
        if (chest != null)
            rewardChests.Add(chest);
    }

    private int GetRewardChestCount()
    {
        int min = Mathf.Clamp(minRewardChests, 0, 2);
        int max = Mathf.Clamp(maxRewardChests, min, 2);
        if (max <= min) return min;

        float twoChestChance = 0.35f;
        if (LevelManager.Instance != null)
            twoChestChance = Mathf.Clamp01(0.25f + LevelManager.Instance.ActiveDungeonIndex * 0.12f);

        return Random.value <= twoChestChance ? max : min;
    }

    private List<Vector3> PickRewardChestPositions(int count)
    {
        List<Vector3> result = new List<Vector3>();
        if (count <= 0) return result;

        List<Vector3> candidates = new List<Vector3>();
        foreach (KeyValuePair<Vector3, bool> tile in tiles)
        {
            if (!tile.Value) continue;
            if (IsTooCloseToDoor(tile.Key)) continue;
            candidates.Add(tile.Key);
        }

        if (candidates.Count == 0)
            candidates.Add(transform.position);

        while (result.Count < count && candidates.Count > 0)
        {
            int index = Random.Range(0, candidates.Count);
            Vector3 position = candidates[index];
            candidates.RemoveAt(index);

            bool tooCloseToOtherChest = false;
            foreach (Vector3 existingPosition in result)
            {
                if (Vector2.Distance(existingPosition, position) < chestSpawnTilePadding)
                {
                    tooCloseToOtherChest = true;
                    break;
                }
            }

            if (tooCloseToOtherChest && candidates.Count > 0)
                continue;

            result.Add(new Vector3(position.x, position.y, -0.2f));
        }

        return result;
    }

    private bool IsTooCloseToDoor(Vector3 position)
    {
        foreach (Door door in doorList)
        {
            if (door == null) continue;
            if (Vector2.Distance(position, door.transform.position) < chestSpawnTilePadding)
                return true;
        }

        return false;
    }

    private bool TryBeginBossEncounter()
    {
        activeBosses.Clear();

        BossManager existingBoss = GetComponentInChildren<BossManager>(true);
        BossManager spawnedBoss = null;
        if (existingBoss == null)
            spawnedBoss = SpawnBossForEncounter();

        RegisterBossForEncounter(spawnedBoss);

        BossManager[] bosses = GetComponentsInChildren<BossManager>(true);
        foreach (BossManager boss in bosses)
        {
            RegisterBossForEncounter(boss);
        }

        return activeBosses.Count > 0;
    }

    private BossManager SpawnBossForEncounter()
    {
        if (bossPrefab == null) return null;

        bool networkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (networkActive && !NetworkManager.Singleton.IsServer)
            return null;

        Vector3 spawnPosition = GetBossSpawnPosition();
        GameObject bossObject = Instantiate(bossPrefab, spawnPosition, Quaternion.identity);
        BossManager boss = bossObject.GetComponentInChildren<BossManager>(true);

        NetworkObject networkObject = bossObject.GetComponent<NetworkObject>();
        if (networkObject != null && networkActive && NetworkManager.Singleton.IsServer && !networkObject.IsSpawned)
        {
            networkObject.Spawn(true);
        }
        else
        {
            bossObject.transform.SetParent(transform, true);
        }

        return boss;
    }

    private void RegisterBossForEncounter(BossManager boss)
    {
        if (boss == null || boss.IsDead || activeBosses.Contains(boss)) return;

        if (!boss.gameObject.activeInHierarchy)
            boss.gameObject.SetActive(true);

        boss.OnDied -= HandleBossDied;
        boss.OnDied += HandleBossDied;
        boss.StartFight();
        activeBosses.Add(boss);
    }

    private Vector3 GetBossSpawnPosition()
    {
        if (bossSpawnPoint != null)
            return bossSpawnPoint.position;

        Collider2D roomCollider = GetComponent<Collider2D>();
        if (roomCollider != null)
            return new Vector3(roomCollider.bounds.center.x, roomCollider.bounds.center.y, transform.position.z);

        if (extraTilemap != null)
            return extraTilemap.localBounds.center + extraTilemap.transform.position;

        return transform.position;
    }

    private void HandleBossDied(BossManager boss)
    {
        if (boss != null)
            boss.OnDied -= HandleBossDied;

        activeBosses.Remove(boss);
        for (int i = activeBosses.Count - 1; i >= 0; i--)
        {
            if (activeBosses[i] == null || activeBosses[i].IsDead)
                activeBosses.RemoveAt(i);
        }

        if (activeBosses.Count == 0)
            CompleteEncounter();
    }

    private void StartBossEndGameCountdown()
    {
        if (bossEndGameCoroutine != null) return;
        bossEndGameCoroutine = StartCoroutine(BossEndGameCountdownRoutine());
    }

    private IEnumerator BossEndGameCountdownRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, bossEndGameDelay));

        if (string.IsNullOrWhiteSpace(endGameSceneName))
            yield break;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!NetworkManager.Singleton.IsServer)
                yield break;

            MultiplayerGameplaySync.BroadcastLoadScene(endGameSceneName);
        }

        if (SceneManager.GetActiveScene().name != endGameSceneName)
            SceneManager.LoadScene(endGameSceneName);
    }

    private void EnsureRoomEntity()
    {
        if (roomEntity == null)
            roomEntity = GetComponent<NetworkedWorldEntity>();

        if (roomEntity == null)
            roomEntity = gameObject.AddComponent<NetworkedWorldEntity>();
    }
    
    private void CreateDoors() 
    {
        if (posDoorNS.Length > 0)
        {
            for (int i = 0; i < posDoorNS.Length; i++)
            {
                RegisterDoor(LevelManager.Instance.DungeonLibrary.DoorNS, posDoorNS[i]);
            }
        }

        if (posDoorWE.Length > 0)
        {
            for (int i = 0; i < posDoorWE.Length; i++)
            {
                RegisterDoor(LevelManager.Instance.DungeonLibrary.DoorWE, posDoorWE[i]);
            }
        }
    }

    private void RegisterDoor(GameObject doorPrefab, Transform objTransform)
    {
       if (doorPrefab == null || objTransform == null) return;

       GameObject doorGO = Instantiate(doorPrefab, objTransform);
       doorGO.transform.localPosition = Vector3.zero;
       doorGO.transform.localRotation = Quaternion.identity;
       AssignNetworkId(doorGO, "Door");

       Door door = doorGO.GetComponent<Door>();
       if (door == null)
       {
           door = doorGO.AddComponent<Door>();
       }
       if (door != null)
       {
           doorList.Add(door);
       }
    }

    private bool NormalRoom()
    {
        return roomType == RoomType.RoomFree || roomType == RoomType.RoomEntrance;
    }

    private bool IsCombatRoom()
    {
        return roomType == RoomType.RoomEnemy || roomType == RoomType.RoomBoss;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (NormalRoom())
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            if (OnPlayerEnterEvent != null)
            {
                OnPlayerEnterEvent.Invoke(this);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (useDebug == false)
        {
            return;
        }
        
        if (tiles.Count > 0)
        {
            foreach (KeyValuePair<Vector3, bool> tile in tiles)
            {
                if (tile.Value) //True
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(tile.Key, Vector3.one *0.8f);
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(tile.Key, 0.3f);
                }
            }
        }
    }
}
