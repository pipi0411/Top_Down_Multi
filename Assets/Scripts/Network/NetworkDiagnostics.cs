using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Kiểm tra setup Netcode có đúng không
/// </summary>
public class NetworkDiagnostics : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log("\n=== NETWORK DIAGNOSTICS ===");
        
        // Check NetworkManager
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("❌ NetworkManager.Singleton is NULL!");
            Debug.LogError("   SOLUTION: Add NetworkManager to scene (Main Manager or SampleScene)");
            Debug.LogError("   - Must have NetworkManager component");
            Debug.LogError("   - Must have DontDestroyOnLoad if in Main Manager");
        }
        else
        {
            Debug.Log("✓ NetworkManager found: " + NetworkManager.Singleton.gameObject.name);
        }

        // Check GameManager
        if (GameManager.Instance == null)
        {
            Debug.LogError("❌ GameManager.Instance is NULL!");
        }
        else
        {
            Debug.Log("✓ GameManager found");
            Debug.Log($"  - CurrentState: {GameManager.Instance.CurrentState}");
            Debug.Log($"  - SelectedCharacter: {GameManager.Instance.SelectedCharacter}");
            Debug.Log($"  - IsHost: {GameManager.Instance.IsHost}");
        }

        // Check CharacterPrefabManager
        if (CharacterPrefabManager.Instance == null)
        {
            Debug.LogError("❌ CharacterPrefabManager.Instance is NULL!");
        }
        else
        {
            Debug.Log("✓ CharacterPrefabManager found");
        }

        // Check NetworkButtons
        if (NetworkButtons.Instance == null)
        {
            Debug.LogError("❌ NetworkButtons.Instance is NULL!");
        }
        else
        {
            Debug.Log("✓ NetworkButtons found");
        }

        Debug.Log("=== END DIAGNOSTICS ===\n");
    }
}
