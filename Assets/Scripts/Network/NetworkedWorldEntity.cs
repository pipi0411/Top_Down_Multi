using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkedWorldEntity : MonoBehaviour
{
    static readonly Dictionary<string, NetworkedWorldEntity> Registry = new();

    [SerializeField] string networkId;
    NetworkObject networkObject;
    string registeredId;
    string stableSceneId;

    public string NetworkId
    {
        get
        {
            string runtimeId = GetRuntimeNetworkObjectId();
            if (!string.IsNullOrWhiteSpace(runtimeId))
                return runtimeId;

            if (!string.IsNullOrWhiteSpace(networkId))
                return networkId;

            return GetStableSceneId();
        }
    }

    public static bool TryFind<T>(string id, out T component) where T : Component
    {
        component = null;
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (!Registry.TryGetValue(id, out NetworkedWorldEntity entity) || entity == null) return false;
        component = entity.GetComponentInParent<T>();
        if (component == null)
            component = entity.GetComponentInChildren<T>(true);
        return component != null;
    }

    public void Initialize(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        Unregister();
        networkId = id;
        Register();
    }

    void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
    }

    void OnEnable()
    {
        Register();
    }

    void Update()
    {
        if (registeredId != NetworkId)
            Register();
    }

    void OnDisable()
    {
        Unregister();
    }

    void Register()
    {
        string id = NetworkId;
        if (string.IsNullOrWhiteSpace(id)) return;

        Unregister();
        Registry[id] = this;
        registeredId = id;
    }

    void Unregister()
    {
        if (!string.IsNullOrWhiteSpace(registeredId)
            && Registry.TryGetValue(registeredId, out NetworkedWorldEntity entity)
            && entity == this)
        {
            Registry.Remove(registeredId);
        }

        registeredId = null;
    }

    string GetRuntimeNetworkObjectId()
    {
        if (networkObject == null)
            networkObject = GetComponent<NetworkObject>();

        if (networkObject == null || !networkObject.IsSpawned)
            return null;

        return $"NetObj_{networkObject.NetworkObjectId}";
    }

    string GetStableSceneId()
    {
        if (!string.IsNullOrWhiteSpace(stableSceneId))
            return stableSceneId;

        Scene scene = gameObject.scene;
        Vector3 position = transform.position;
        stableSceneId = $"SceneObj_{scene.name}_{BuildHierarchyPath(transform)}_{Mathf.RoundToInt(position.x * 100f)}_{Mathf.RoundToInt(position.y * 100f)}";
        return stableSceneId;
    }

    static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
            return "Unknown";

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }
}
