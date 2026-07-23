using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyWaveData", menuName = "Dungeon/Enemy Wave Data")]
public class EnemyWaveData : ScriptableObject
{
    [SerializeField] private List<EnemyWave> waves = new List<EnemyWave>();

    public IReadOnlyList<EnemyWave> Waves => waves;
    public bool HasWaves => waves != null && waves.Count > 0;
}

[System.Serializable]
public class EnemyWave
{
    [SerializeField] private string waveName = "Wave";
    [SerializeField] private float startDelay = 0.5f;
    [SerializeField] private float spawnInterval = 0.4f;
    [SerializeField] private float nextWaveDelay = 1.5f;
    [SerializeField] private List<EnemySpawnEntry> enemies = new List<EnemySpawnEntry>();

    public string WaveName => waveName;
    public float StartDelay => Mathf.Max(0f, startDelay);
    public float SpawnInterval => Mathf.Max(0f, spawnInterval);
    public float NextWaveDelay => Mathf.Max(0f, nextWaveDelay);
    public IReadOnlyList<EnemySpawnEntry> Enemies => enemies;
}

[System.Serializable]
public class EnemySpawnEntry
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int count = 1;

    public GameObject EnemyPrefab => enemyPrefab;
    public int Count => Mathf.Max(0, count);
}
