using UnityEngine;
using UnityEngine.UI;

public class ModeSelectUIManager : MonoBehaviour
{
    [SerializeField] private GameObject modeSelectPanel;
    
    [Header("Buttons")]
    [SerializeField] private Button singlePlayerButton;
    [SerializeField] private Button multiplayerButton;
    [SerializeField] private Button backButton;

    private bool gameManagerSubscribed;

    private void OnEnable()
    {
        if (modeSelectPanel == null || singlePlayerButton == null || multiplayerButton == null || backButton == null)
        {
            Debug.LogError("ModeSelectUIManager is missing required references in the Inspector.");
            return;
        }

        singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
        multiplayerButton.onClick.AddListener(OnMultiplayerClicked);
        backButton.onClick.AddListener(OnBackClicked);

        if (GameManager.Instance != null)
        {
            TrySubscribeToGameManager();
        }
    }

    private void Update()
    {
        if (!gameManagerSubscribed && GameManager.Instance != null)
        {
            TrySubscribeToGameManager();
        }
    }

    private void OnDisable()
    {
        if (singlePlayerButton != null)
            singlePlayerButton.onClick.RemoveListener(OnSinglePlayerClicked);
        if (multiplayerButton != null)
            multiplayerButton.onClick.RemoveListener(OnMultiplayerClicked);
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null && gameManagerSubscribed)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    private void TrySubscribeToGameManager()
    {
        if (gameManagerSubscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.OnStateChanged += HandleStateChanged;
        gameManagerSubscribed = true;
        HandleStateChanged(GameManager.Instance.CurrentState);
    }

    private void OnSinglePlayerClicked()
    {
        Debug.Log("Single Player Mode Selected");
        GameManager.Instance.SetMultiplayerMode(false);
        GameManager.Instance.ChangeState(GameManager.GameState.CharacterSelect);
    }

    private void OnMultiplayerClicked()
    {
        Debug.Log("Multiplayer Mode Selected");
        GameManager.Instance.SetMultiplayerMode(true);
        GameManager.Instance.ChangeState(GameManager.GameState.CharacterSelect);
    }

    private void OnBackClicked()
    {
        Debug.Log("Back to Main Menu");
        GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        modeSelectPanel.SetActive(newState == GameManager.GameState.ModeSelect);
    }
}
