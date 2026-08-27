using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EnemyWaveSpawner : MonoBehaviour
{
    [Header("Wave Config")]
    [SerializeField] private EnemyWaveData waveData;
    [SerializeField] private bool waitForWaveClear = true;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform enemyParent;

    private readonly List<EnemyHealth> aliveEnemies = new List<EnemyHealth>();
    private Coroutine waveRoutine;
    private Room ownerRoom;
    private bool completed;
    private int spawnPointCursor;

    public bool IsRunning => waveRoutine != null;
    public bool IsCompleted => completed;

    private void Awake()
    {
        AutoFindSpawnPoints();
        if (enemyParent == null) enemyParent = transform;
    }

    public void SetWaveData(EnemyWaveData data)
    {
        waveData = data;
    }

    public void StartWaves(Room room)
    {
        if (completed || waveRoutine != null) return;

        ownerRoom = room;

        if (waveData == null || !waveData.HasWaves)
        {
            CompleteRoom();
            return;
        }

        waveRoutine = StartCoroutine(RunWaves());
    }

    public void ResetWaves()
    {
        if (waveRoutine != null)
        {
            StopCoroutine(waveRoutine);
            waveRoutine = null;
        }

        ClearAliveEnemies();
        completed = false;
    }

    private IEnumerator RunWaves()
    {
        for (int i = 0; i < waveData.Waves.Count; i++)
        {
            EnemyWave wave = waveData.Waves[i];
            if (wave == null) continue;

            yield return Wait(wave.StartDelay);
            EnemyWaveAnnouncementUI.Show(string.IsNullOrWhiteSpace(wave.WaveName) ? $"Wave {i + 1}" : wave.WaveName);

            for (int entryIndex = 0; entryIndex < wave.Enemies.Count; entryIndex++)
            {
                EnemySpawnEntry entry = wave.Enemies[entryIndex];
                if (entry == null || entry.EnemyPrefab == null || entry.Count <= 0) continue;

                for (int count = 0; count < entry.Count; count++)
                {
                    SpawnEnemy(entry.EnemyPrefab);
                    yield return Wait(wave.SpawnInterval);
                }
            }

            if (waitForWaveClear)
                yield return new WaitUntil(() => aliveEnemies.Count == 0);

            if (i < waveData.Waves.Count - 1)
                yield return Wait(wave.NextWaveDelay);
        }

        waveRoutine = null;
        CompleteRoom();
    }

    private void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null) return;

        bool networkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool prefabIsNetworked = prefab.GetComponent<NetworkObject>() != null;
        if (networkActive && prefabIsNetworked && !NetworkManager.Singleton.IsServer)
            return;

        Transform spawnPoint = GetRandomSpawnPoint();
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject enemy = Instantiate(prefab, spawnPosition, spawnRotation, enemyParent);
        AssignNetworkId(enemy, "Enemy");

        NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
        if (networkObject != null && networkActive && NetworkManager.Singleton.IsServer && !networkObject.IsSpawned)
            networkObject.Spawn(true);

        EnemyHealth health = enemy.GetComponentInChildren<EnemyHealth>();
        if (health != null)
        {
            aliveEnemies.Add(health);
            health.OnDied += () => HandleEnemyDied(health);
        }
    }

    private void AssignNetworkId(GameObject instance, string prefix)
    {
        if (instance == null || LevelManager.Instance == null) return;
        NetworkedWorldEntity entity = instance.GetComponent<NetworkedWorldEntity>();
        if (entity == null)
            entity = instance.AddComponent<NetworkedWorldEntity>();
        entity.Initialize(LevelManager.Instance.NextNetworkEntityId(prefix));
    }

    private void HandleEnemyDied(EnemyHealth health)
    {
        if (health == null) return;
        health.OnDied -= () => HandleEnemyDied(health);
        aliveEnemies.Remove(health);
    }

    private void ClearAliveEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = aliveEnemies[i];
            if (enemy == null) continue;

            NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
                networkObject.Despawn(true);
            else
                Destroy(enemy.gameObject);
        }

        aliveEnemies.Clear();
    }

    private Transform GetRandomSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform point = spawnPoints[spawnPointCursor % spawnPoints.Length];
            spawnPointCursor++;
            if (point != null) return point;
        }

        return null;
    }

    private void CompleteRoom()
    {
        completed = true;
        EnemyWaveAnnouncementUI.Show("Unlock Door");
        if (ownerRoom != null)
        {
            ownerRoom.CompleteEncounter();
        }
    }

    private IEnumerator Wait(float seconds)
    {
        if (seconds <= 0f) yield break;
        yield return new WaitForSeconds(seconds);
    }

    private void AutoFindSpawnPoints()
    {
        if (spawnPoints != null && spawnPoints.Length > 0) return;

        Transform group = transform.Find("SpawnPoints");
        if (group == null) group = transform.Find("Spawn Points");

        if (group != null && group.childCount > 0)
        {
            spawnPoints = new Transform[group.childCount];
            for (int i = 0; i < group.childCount; i++)
                spawnPoints[i] = group.GetChild(i);
        }
        else if (group != null)
        {
            spawnPoints = new[] { group };
        }
    }
}
