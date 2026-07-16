using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System.IO.Compression;

public enum RoomType
{
    RoomFree,
    RoomEntrance,
    RoomEnemy,
    RoomBoss
}
public class Room : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private bool useDebug;
    [SerializeField] private RoomType roomType;

    [Header("Grid")]
    [SerializeField] private Tilemap extraTilemap;
    
    // Position (Key) - Free/ Not Free (Value)
    private Dictionary<Vector3, bool> tiles = new Dictionary<Vector3, bool>();
    private void Start()
    {
        GetTiles();
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
        for (int y = 0, a = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++, a++)
            {
                Color pixelColor = texture.GetPixel(x, y);
                foreach (RoomProp prop in LevelManager.Instance.RoomTemplates.PropsData)
                {
                    if(pixelColor == prop.PropColor)
                    {
                       GameObject propInstance =
                        Instantiate(prop.ProPrefab, extraTilemap.transform);
                        propInstance.transform.position = new Vector3(positions[a].x, positions[a].y, 0f);
                        if (tiles.ContainsKey(positions[a]))
                        {
                            tiles[positions[a]] = false;
                        }
                    }
                }
            }
        }
    }

    private bool NormalRoom()
    {
        return roomType == RoomType.RoomFree || roomType == RoomType.RoomEntrance;
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
