using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DungeonLibrary" , menuName = "Dungeon / Library")]
public class DungeonLibrary : ScriptableObject
{
    [Header("Levels")]
    public Level[] Levels;
    [Header("Room")]
    public GameObject DoorNS;
    public GameObject DoorWE;
}

[Serializable]
public class Level
{
    public string Name;
    public GameObject[] Dungeons;
    public EnemyWaveData[] WaveDataByDungeon;
    public DungeonWaveSet[] WaveSetsByDungeon;
}

[Serializable]
public class DungeonWaveSet
{
    public string Name;
    public EnemyWaveData[] RoomWaves;
}
