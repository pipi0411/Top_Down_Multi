using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveGameManager : MonoBehaviour
{
    const string SaveFileName = "single_run_save.json";
    const string RegistryResourcePath = "WeaponPrefabRegistry";

    static SaveGameManager instance;

    [SerializeField] WeaponPrefabRegistry weaponRegistry;
    [SerializeField] GameObject[] weaponPrefabs;
    [SerializeField] bool autoSaveSinglePlayer = true;

    SingleRunSaveData loadedSave;
    bool pendingContinue;
    bool applyingSave;
    float nextSaveAllowedTime;

    public static SaveGameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<SaveGameManager>(FindObjectsInactive.Include);
                if (instance == null)
                {
                    GameObject go = new GameObject("SaveGameManager");
                    instance = go.AddComponent<SaveGameManager>();
                }
            }

            return instance;
        }
    }

    public static bool HasSingleSave => File.Exists(SavePath);
    public static bool IsApplyingSave => Instance.applyingSave;
    static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadRegistryIfNeeded();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveSingleRun("Application paused", true);
    }

    void OnApplicationQuit()
    {
        SaveSingleRun("Application quit", true);
    }

    public static void ContinueSinglePlayer()
    {
        Instance.BeginContinueSinglePlayer();
    }

    public static void SaveSingleRunNow(string reason = "Manual save")
    {
        Instance.SaveSingleRun(reason, true);
    }

    public static void AutoSave(string reason)
    {
        if (Instance.autoSaveSinglePlayer)
            Instance.SaveSingleRun(reason, false);
    }

    public static void ClearSingleRunSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

    public static bool IsBoxBroken(string id)
    {
        return Instance.loadedSave != null && Contains(Instance.loadedSave.world.brokenBoxes, id);
    }

    public static bool IsChestOpened(string id)
    {
        return Instance.loadedSave != null && Contains(Instance.loadedSave.world.openedChests, id);
    }

    public static bool IsRoomCompleted(string id)
    {
        return Instance.loadedSave != null && Contains(Instance.loadedSave.world.completedRooms, id);
    }

    public static void RecordBoxBroken(string id)
    {
        Instance.RecordWorldId(Instance.EnsureLoadedSave().world.brokenBoxes, id, "Box broken");
    }

    public static void RecordChestOpened(string id)
    {
        Instance.RecordWorldId(Instance.EnsureLoadedSave().world.openedChests, id, "Chest opened");
    }

    public static void RecordWaveCompleted(string roomId, int waveIndex)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return;
        Instance.RecordWorldId(Instance.EnsureLoadedSave().world.completedWaves, $"{roomId}_Wave_{waveIndex}", "Wave completed");
    }

    public static void RecordRoomCompleted(string roomId)
    {
        Instance.RecordWorldId(Instance.EnsureLoadedSave().world.completedRooms, roomId, "Room completed");
    }

    public GameObject FindWeaponPrefab(string weaponId)
    {
        LoadRegistryIfNeeded();
        GameObject prefab = weaponRegistry != null ? weaponRegistry.FindWeaponPrefab(weaponId) : null;
        if (prefab != null)
            return prefab;

        string normalizedId = NormalizeObjectId(weaponId);
        if (weaponPrefabs != null)
        {
            foreach (GameObject weaponPrefab in weaponPrefabs)
            {
                if (weaponPrefab != null && NormalizeObjectId(weaponPrefab.name) == normalizedId)
                    return weaponPrefab;
            }
        }

        return null;
    }

    public static string NormalizeObjectId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Replace("(Clone)", string.Empty).Trim();
    }

    void BeginContinueSinglePlayer()
    {
        if (!TryLoadSave(out loadedSave))
        {
            Debug.LogWarning("[Save] No single-player save found.");
            return;
        }

        pendingContinue = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMultiplayerMode(false);
            GameManager.Instance.SetSelectedCharacter(loadedSave.selectedCharacter);
            GameManager.Instance.ChangeState(GameManager.GameState.GameStarting);
        }

        GameSceneLoader.LoadGameplayScene("SampleScene");
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pendingContinue || loadedSave == null || scene.name != "SampleScene")
            return;

        StartCoroutine(ApplyContinueWhenReady());
    }

    System.Collections.IEnumerator ApplyContinueWhenReady()
    {
        applyingSave = true;

        float deadline = Time.unscaledTime + 8f;
        while (Time.unscaledTime < deadline && LevelManager.Instance == null)
            yield return null;

        if (LevelManager.Instance != null)
            LevelManager.Instance.LoadSavedDungeon(loadedSave.levelIndex, loadedSave.dungeonIndex);

        yield return null;
        yield return null;

        PlayerHealth player = FindLocalPlayer();
        if (player != null)
        {
            PlayerWeaponController weaponController = player.GetComponent<PlayerWeaponController>();
            if (weaponController != null)
            {
                weaponController.RestoreWeaponsFromSave(
                    loadedSave.player.gunWeaponId,
                    loadedSave.player.meleeWeaponId,
                    loadedSave.player.selectedSlotIndex,
                    FindWeaponPrefab);
            }

            player.RestoreSingleRunState(
                loadedSave.player.health,
                loadedSave.player.armor,
                loadedSave.player.energy,
                loadedSave.player.lives,
                loadedSave.player.ammo);

            if (loadedSave.player.hasPosition)
                player.transform.position = loadedSave.player.position.ToVector3();
        }

        PickupItem.SetCoinsLocal(loadedSave.player.coinInRun);

        pendingContinue = false;
        applyingSave = false;
        AutoSave("Continue loaded");
    }

    void SaveSingleRun(string reason, bool force)
    {
        if (applyingSave || pendingContinue) return;
        if (!force && Time.unscaledTime < nextSaveAllowedTime) return;
        if (!IsSingleGameplay()) return;

        PlayerHealth player = FindLocalPlayer();
        if (player == null) return;

        SingleRunSaveData save = CaptureSave(player);
        loadedSave = save;

        Directory.CreateDirectory(Application.persistentDataPath);
        File.WriteAllText(SavePath, JsonUtility.ToJson(save, true));
        nextSaveAllowedTime = Time.unscaledTime + 0.35f;
        Debug.Log($"[Save] Single run saved: {reason}. Path: {SavePath}");
    }

    SingleRunSaveData CaptureSave(PlayerHealth player)
    {
        SingleRunSaveData save = loadedSave ?? new SingleRunSaveData();
        save.version = 1;
        save.savedAtUtc = DateTime.UtcNow.ToString("O");
        save.selectedCharacter = GameManager.Instance != null ? GameManager.Instance.SelectedCharacter : string.Empty;

        if (LevelManager.Instance != null)
        {
            save.levelIndex = LevelManager.Instance.ActiveLevelIndex;
            save.dungeonIndex = LevelManager.Instance.ActiveDungeonIndex;
        }

        save.player.health = player.CurrentHealth;
        save.player.armor = player.CurrentArmor;
        save.player.energy = player.CurrentEnergy;
        save.player.lives = player.CurrentLives;
        save.player.ammo = player.CurrentAmmo;
        save.player.coinInRun = PickupItem.Coins;
        save.player.position = SerializableVector3.From(player.transform.position);
        save.player.hasPosition = true;

        PlayerWeaponController weaponController = player.GetComponent<PlayerWeaponController>();
        if (weaponController != null)
        {
            save.player.gunWeaponId = weaponController.GetWeaponSaveId(0);
            save.player.meleeWeaponId = weaponController.GetWeaponSaveId(1);
            save.player.selectedSlotIndex = weaponController.SelectedSlotIndex;
        }

        return save;
    }

    SingleRunSaveData EnsureLoadedSave()
    {
        if (loadedSave != null)
            return loadedSave;

        if (!TryLoadSave(out loadedSave))
            loadedSave = new SingleRunSaveData();

        loadedSave.EnsureLists();
        return loadedSave;
    }

    bool TryLoadSave(out SingleRunSaveData save)
    {
        save = null;
        if (!File.Exists(SavePath))
            return false;

        try
        {
            save = JsonUtility.FromJson<SingleRunSaveData>(File.ReadAllText(SavePath));
            if (save == null)
                return false;

            save.EnsureLists();
            loadedSave = save;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Save] Failed to load save: {ex.Message}");
            return false;
        }
    }

    void RecordWorldId(List<string> list, string id, string reason)
    {
        if (!IsSingleGameplay() || string.IsNullOrWhiteSpace(id) || list == null)
            return;

        if (!Contains(list, id))
            list.Add(id);

        AutoSave(reason);
    }

    bool IsSingleGameplay()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsMultiplayer) return false;
        return GameManager.Instance.CurrentState == GameManager.GameState.InGame
               || GameManager.Instance.CurrentState == GameManager.GameState.GameStarting;
    }

    PlayerHealth FindLocalPlayer()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        foreach (PlayerHealth player in players)
        {
            if (player == null) continue;
            if (!player.IsSpawned || player.IsOwner)
                return player;
        }

        return null;
    }

    void LoadRegistryIfNeeded()
    {
        if (weaponRegistry == null)
            weaponRegistry = Resources.Load<WeaponPrefabRegistry>(RegistryResourcePath);
    }

    static bool Contains(List<string> values, string id)
    {
        if (values == null || string.IsNullOrWhiteSpace(id)) return false;
        for (int i = 0; i < values.Count; i++)
            if (string.Equals(values[i], id, StringComparison.Ordinal))
                return true;
        return false;
    }
}

[Serializable]
public class SingleRunSaveData
{
    public int version = 1;
    public string savedAtUtc;
    public string selectedCharacter;
    public int levelIndex;
    public int dungeonIndex;
    public SinglePlayerSaveData player = new SinglePlayerSaveData();
    public SingleWorldSaveData world = new SingleWorldSaveData();

    public void EnsureLists()
    {
        if (player == null) player = new SinglePlayerSaveData();
        if (world == null) world = new SingleWorldSaveData();
        world.EnsureLists();
    }
}

[Serializable]
public class SinglePlayerSaveData
{
    public float health;
    public float armor;
    public float energy;
    public int lives;
    public int ammo;
    public int coinInRun;
    public string gunWeaponId;
    public string meleeWeaponId;
    public int selectedSlotIndex;
    public bool hasPosition;
    public SerializableVector3 position;
}

[Serializable]
public class SingleWorldSaveData
{
    public List<string> openedChests = new List<string>();
    public List<string> brokenBoxes = new List<string>();
    public List<string> completedRooms = new List<string>();
    public List<string> completedWaves = new List<string>();

    public void EnsureLists()
    {
        openedChests ??= new List<string>();
        brokenBoxes ??= new List<string>();
        completedRooms ??= new List<string>();
        completedWaves ??= new List<string>();
    }
}

[Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public static SerializableVector3 From(Vector3 value)
    {
        return new SerializableVector3 { x = value.x, y = value.y, z = value.z };
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
