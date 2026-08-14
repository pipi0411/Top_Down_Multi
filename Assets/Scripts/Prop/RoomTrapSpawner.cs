using UnityEngine;
using UnityEngine.Tilemaps;

public class RoomTrapSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap trapMarkerTilemap;   // Tilemap đánh dấu trong room này
    [SerializeField] private TileBase trapMarkerTile;     // Tile dùng để tô
    [SerializeField] private GameObject spikePrefab;

    [Header("Settings")]
    [SerializeField] private bool clearMarkerAfterSpawn = true;
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    private void Start()
    {
        SpawnTrapsInThisRoom();
    }

    public void SpawnTrapsInThisRoom()
    {
        if (trapMarkerTilemap == null || spikePrefab == null || trapMarkerTile == null)
        {
            Debug.LogWarning($"Room {name} thiếu reference TrapSpawner");
            return;
        }

        BoundsInt bounds = trapMarkerTilemap.cellBounds;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (trapMarkerTilemap.GetTile(pos) == trapMarkerTile)
            {
                Vector3 worldPos = trapMarkerTilemap.GetCellCenterWorld(pos) + spawnOffset;
                Instantiate(spikePrefab, worldPos, Quaternion.identity, transform); // spawn làm con của room

                if (clearMarkerAfterSpawn)
                    trapMarkerTilemap.SetTile(pos, null);
            }
        }
    }
}