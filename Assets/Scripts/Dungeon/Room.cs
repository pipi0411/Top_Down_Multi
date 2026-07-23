using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System;

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
    [Header("Config")]
    [SerializeField] private bool useDebug;
    [SerializeField] private RoomType roomType;

    [Header("Grid")]
    [SerializeField] private Tilemap extraTilemap;

    [Header("Enemy Waves")]
    [SerializeField] private EnemyWaveData waveData;
    [HideInInspector]
    [SerializeField] private EnemyWaveSpawner waveSpawner;

    [Header("Doors")]
    [SerializeField] private Transform[] posDoorNS;
    [SerializeField] private Transform[] posDoorWE;

    public bool RoomCompleted { get; set; }

    // Position (Key) - Free/ Not Free (Value)
    private Dictionary<Vector3, bool> tiles = new Dictionary<Vector3, bool>();
    private List<Door> doorList = new List<Door>();
    private readonly List<BoxSpawnPoint> pendingBoxPoints = new List<BoxSpawnPoint>();

    public bool CanSpawnBoxes => !NormalRoom();
    public bool HasEnemyWave => IsCombatRoom() && waveData != null;

    private void Start()
    {
        GetTiles();
        CreateDoors();
        EnsureWaveSpawner();
        GenerateRoomUsingTemplate();
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
        GameObject boxInstance = Instantiate(point.Prop.ProPrefab, extraTilemap.transform);
        boxInstance.transform.position = point.Position;
        MarkTileOccupied(point.Position);
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
        if (RoomCompleted || !IsCombatRoom()) return;

        EnsureWaveSpawner();
        if (waveSpawner != null)
        {
            waveSpawner.SetWaveData(waveData);
            waveSpawner.StartWaves(this);
        }
        else
        {
            RoomCompleted = true;
            OpenDoors();
        }
    }

    private void EnsureWaveSpawner()
    {
        if (!IsCombatRoom()) return;

        if (waveSpawner == null)
            waveSpawner = GetComponent<EnemyWaveSpawner>();

        if (waveSpawner == null && waveData != null)
            waveSpawner = gameObject.AddComponent<EnemyWaveSpawner>();

        if (waveSpawner != null)
            waveSpawner.SetWaveData(waveData);
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
