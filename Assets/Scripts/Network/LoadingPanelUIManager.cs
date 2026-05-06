using TMPro;
using UnityEngine;

public class LoadingPanelUIManager : MonoBehaviour
{
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;

    private void OnEnable()
    {
        if (loadingPanel == null)
        {
            Debug.LogError("LoadingPanelUIManager is missing loadingPanel reference in the Inspector.");
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            HandleStateChanged(GameManager.Instance.CurrentState);
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        Debug.Log($"LoadingPanelUIManager: State changed to {newState}");
        // GameManager now manages panel visibility directly
        // If needed for loading, use ShowLoading/HideLoading methods manually
    }

    public void ShowLoading(string message = "Loading...")
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        if (loadingText != null)
            loadingText.text = message;
    }

    public void HideLoading()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }

    public void UpdateLoadingText(string message)
    {
        if (loadingText != null)
            loadingText.text = message;
    }
}
