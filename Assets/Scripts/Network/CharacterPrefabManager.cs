using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Quản lý ánh xạ giữa tên nhân vật và prefab tương ứng.
/// Sử dụng để spawn prefab đúng khi chọn nhân vật ở CharacterSelect.
/// </summary>
public class CharacterPrefabManager : MonoBehaviour
{
    [System.Serializable]
    public class CharacterPrefabMapping
    {
        public string characterName;
        public string prefabPath; // Đường dẫn trong Resources hoặc Assets
        public GameObject prefabReference; // Optional: direct reference trong inspector
    }

    [SerializeField] private List<CharacterPrefabMapping> characterMappings = new List<CharacterPrefabMapping>();

    private static CharacterPrefabManager instance;
    public static CharacterPrefabManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<CharacterPrefabManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("CharacterPrefabManager");
                    instance = go.AddComponent<CharacterPrefabManager>();
                    instance.InitializeDefaultMappings();
                }
            }
            return instance;
        }
    }

    private Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    private Dictionary<string, uint> prefabHashCache = new Dictionary<string, uint>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (characterMappings == null || characterMappings.Count == 0)
        {
            InitializeDefaultMappings();
        }

        // Cache tất cả các prefab
        CacheAllPrefabs();
    }

    /// <summary>
    /// Tạo ánh xạ mặc định nếu chưa có
    /// </summary>
    private void InitializeDefaultMappings()
    {
        characterMappings = new List<CharacterPrefabMapping>
        {
            new CharacterPrefabMapping { characterName = "Knight", prefabPath = "Prefabs/Player_Shadowblade" },
            new CharacterPrefabMapping { characterName = "Archer", prefabPath = "Prefabs/Player_Novak" },
            new CharacterPrefabMapping { characterName = "Mage", prefabPath = "Prefabs/Player_Ganfald" },
            new CharacterPrefabMapping { characterName = "Rogue", prefabPath = "Prefabs/Player_Ember" },
            new CharacterPrefabMapping { characterName = "Paladin", prefabPath = "Prefabs/Player_Iron" }
        };

        Debug.Log("[CharacterPrefabManager] Initialized with default mappings (5 characters)");
    }

    /// <summary>
    /// Load tất cả prefab vào cache
    /// </summary>
    private void CacheAllPrefabs()
    {
        prefabCache.Clear();
        prefabHashCache.Clear();

        foreach (var mapping in characterMappings)
        {
            if (mapping == null)
            {
                Debug.LogWarning("[CharacterPrefabManager] Null mapping found in characterMappings");
                continue;
            }

            GameObject prefab = null;

            // Ưu tiên lấy từ direct reference nếu có
            if (mapping.prefabReference != null)
            {
                prefab = mapping.prefabReference;
                Debug.Log($"[CharacterPrefabManager] Loaded '{mapping.characterName}' from direct reference: {prefab.name}");
            }
            // Nếu không, try load từ Resources
            else if (!string.IsNullOrEmpty(mapping.prefabPath))
            {
                try
                {
                    prefab = Resources.Load<GameObject>(mapping.prefabPath);
                    if (prefab != null)
                    {
                        Debug.Log($"[CharacterPrefabManager] Loaded '{mapping.characterName}' from Resources: {mapping.prefabPath}");
                    }
                    else
                    {
                        Debug.LogWarning($"[CharacterPrefabManager] Not found in Resources: {mapping.prefabPath}, trying fallback...");
                        // Fallback: Try finding by name using AssetDatabase or Resources
                        prefab = TryFindPrefabByName(mapping.prefabPath.Split('/')[mapping.prefabPath.Split('/').Length - 1]);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[CharacterPrefabManager] Error loading '{mapping.characterName}': {e.Message}");
                    prefab = TryFindPrefabByName(mapping.characterName);
                }
            }
            
            // Last resort: Try to find by character name
            if (prefab == null)
            {
                prefab = TryFindPrefabByName(mapping.characterName);
            }

            if (prefab != null)
            {
                CachePrefab(mapping.characterName, prefab);
            }
            else
            {
                Debug.LogError($"[CharacterPrefabManager] ✗ Failed to load prefab for '{mapping.characterName}'");
            }
        }

        Debug.Log($"[CharacterPrefabManager] Cache complete: {prefabCache.Count}/{characterMappings.Count} prefabs loaded");
    }

    private void CachePrefab(string characterName, GameObject prefab)
    {
        prefabCache[characterName] = prefab;

        NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            prefabHashCache[characterName] = networkObject.PrefabIdHash;
            Debug.Log($"[CharacterPrefabManager] ✓ Cached '{characterName}' → {prefab.name} (hash={networkObject.PrefabIdHash})");
        }
        else
        {
            Debug.LogWarning($"[CharacterPrefabManager] Prefab '{prefab.name}' has no NetworkObject component, hash not cached.");
        }
    }

    /// <summary>
    /// Tìm prefab theo tên từ Resources
    /// </summary>
    private GameObject TryFindPrefabByName(string searchName)
    {
        if (string.IsNullOrEmpty(searchName))
            return null;

#if UNITY_EDITOR
        try
        {
            // Try find in Prefabs folder
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{searchName} t:GameObject", new[] { "Assets/Prefabs" });
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Debug.Log($"[CharacterPrefabManager] Found prefab by name '{searchName}' at {path}");
                return prefab;
            }
        }
        catch
        {
            // AssetDatabase only works in editor, skip in runtime
        }
#endif

        return null;
    }

    /// <summary>
    /// Lấy prefab cho một nhân vật
    /// </summary>
    public GameObject GetPrefabForCharacter(string characterName)
    {
        if (string.IsNullOrEmpty(characterName))
        {
            Debug.LogError("[CharacterPrefabManager] Character name is null or empty");
            return null;
        }

        if (prefabCache.TryGetValue(characterName, out GameObject prefab))
        {
            Debug.Log($"[CharacterPrefabManager] Retrieved prefab for '{characterName}'");
            return prefab;
        }

        Debug.LogWarning($"[CharacterPrefabManager] No prefab found for character: '{characterName}'");
        return null;
    }

    public bool TryGetPrefabHashForCharacter(string characterName, out uint prefabHash)
    {
        prefabHash = 0;

        if (string.IsNullOrEmpty(characterName))
        {
            return false;
        }

        if (prefabHashCache.TryGetValue(characterName, out prefabHash))
        {
            return true;
        }

        GameObject prefab = GetPrefabForCharacter(characterName);
        if (prefab == null)
        {
            return false;
        }

        NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError($"[CharacterPrefabManager] Prefab '{prefab.name}' does not have NetworkObject, cannot derive hash.");
            return false;
        }

        prefabHash = networkObject.PrefabIdHash;
        prefabHashCache[characterName] = prefabHash;
        return true;
    }

    /// <summary>
    /// Lấy tất cả tên nhân vật available
    /// </summary>
    public string[] GetAvailableCharacters()
    {
        string[] characters = new string[characterMappings.Count];
        for (int i = 0; i < characterMappings.Count; i++)
        {
            characters[i] = characterMappings[i].characterName;
        }
        return characters;
    }

    /// <summary>
    /// Thêm ánh xạ mới (runtime)
    /// </summary>
    public void AddMapping(string characterName, GameObject prefab)
    {
        if (string.IsNullOrEmpty(characterName) || prefab == null)
        {
            Debug.LogError("[CharacterPrefabManager] Invalid characterName or prefab");
            return;
        }

        var existing = characterMappings.Find(m => m.characterName == characterName);
        if (existing != null)
        {
            existing.prefabReference = prefab;
        }
        else
        {
            characterMappings.Add(new CharacterPrefabMapping 
            { 
                characterName = characterName, 
                prefabReference = prefab 
            });
        }

        prefabCache[characterName] = prefab;
        Debug.Log($"[CharacterPrefabManager] Added/Updated mapping for '{characterName}'");
    }

    /// <summary>
    /// Xóa ánh xạ
    /// </summary>
    public void RemoveMapping(string characterName)
    {
        characterMappings.RemoveAll(m => m.characterName == characterName);
        prefabCache.Remove(characterName);
        Debug.Log($"[CharacterPrefabManager] Removed mapping for '{characterName}'");
    }
}
