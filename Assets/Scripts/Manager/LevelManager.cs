using System;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;
    [Header("Config")]
    [SerializeField] private RoomTemplate roomTemplates;
    [SerializeField] private DungeonLibrary dungeonLibrary;
    public RoomTemplate RoomTemplates => roomTemplates;
    public DungeonLibrary DungeonLibrary => dungeonLibrary;
    
    private void Awake()
    {
        Instance = this;
    }
}
