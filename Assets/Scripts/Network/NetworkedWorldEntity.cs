using System.Collections.Generic;
using UnityEngine;

public class NetworkedWorldEntity : MonoBehaviour
{
    static readonly Dictionary<string, NetworkedWorldEntity> Registry = new();

    [SerializeField] string networkId;

    public string NetworkId => networkId;

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

    void OnEnable()
    {
        Register();
    }

    void OnDisable()
    {
        Unregister();
    }

    void Register()
    {
        if (string.IsNullOrWhiteSpace(networkId)) return;
        Registry[networkId] = this;
    }

    void Unregister()
    {
        if (!string.IsNullOrWhiteSpace(networkId)
            && Registry.TryGetValue(networkId, out NetworkedWorldEntity entity)
            && entity == this)
        {
            Registry.Remove(networkId);
        }
    }
}
