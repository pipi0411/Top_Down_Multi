using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectUIManager : MonoBehaviour
{
    [SerializeField] private GameObject characterSelectPanel;
    
    [Header("Character Selection")]
    [SerializeField] private Button[] characterButtons;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Text characterNameText;
    
    private string selectedCharacter;
    private int selectedCharacterIndex = -1;

    // Hardcoded character list (can be replaced with backend API call)
    private string[] availableCharacters = { "Knight", "Archer", "Mage", "Rogue", "Paladin" };

    private void OnEnable()
    {
        if (characterSelectPanel == null || selectButton == null || backButton == null || characterButtons.Length == 0)
        {
            Debug.LogError("CharacterSelectUIManager is missing required references in the Inspector.");
            return;
        }

        // Setup character buttons
        for (int i = 0; i < characterButtons.Length && i < availableCharacters.Length; i++)
        {
            int index = i; // Local copy for closure
            characterButtons[i].onClick.AddListener(() => OnCharacterSelected(index));
            
            Text buttonText = characterButtons[i].GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = availableCharacters[i];
            }
        }

        selectButton.onClick.AddListener(OnSelectClicked);
        backButton.onClick.AddListener(OnBackClicked);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            HandleStateChanged(GameManager.Instance.CurrentState);
        }

        // Reset selection when panel appears
        selectedCharacterIndex = -1;
        if (characterNameText != null)
        {
            characterNameText.text = "Select a Character";
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < characterButtons.Length; i++)
        {
            if (characterButtons[i] != null)
            {
                characterButtons[i].onClick.RemoveAllListeners();
            }
        }

        if (selectButton != null)
            selectButton.onClick.RemoveListener(OnSelectClicked);
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    private void OnCharacterSelected(int index)
    {
        if (index >= 0 && index < availableCharacters.Length)
        {
            selectedCharacterIndex = index;
            selectedCharacter = availableCharacters[index];
            
            Debug.Log($"Character selected: {selectedCharacter}");

            // Update UI to highlight selected character
            if (characterNameText != null)
            {
                characterNameText.text = $"Selected: {selectedCharacter}";
            }

            // Highlight selected button
            for (int i = 0; i < characterButtons.Length; i++)
            {
                Image buttonImage = characterButtons[i].GetComponent<Image>();
                if (i == index && buttonImage != null)
                {
                    buttonImage.color = Color.green;
                }
                else if (buttonImage != null)
                {
                    buttonImage.color = Color.white;
                }
            }
        }
    }

    private void OnSelectClicked()
    {
        if (selectedCharacterIndex == -1)
        {
            Debug.LogWarning("Please select a character first");
            return;
        }

        Debug.Log($"Character confirmed: {selectedCharacter}");
        GameManager.Instance.SetSelectedCharacter(selectedCharacter);

        // If multiplayer mode, go to room selection
        if (GameManager.Instance.IsMultiplayer)
        {
            GameManager.Instance.ChangeState(GameManager.GameState.RoomLobby);
        }
        else
        {
            // If single player, start game directly
            GameManager.Instance.ChangeState(GameManager.GameState.GameStarting);
        }
    }

    private void OnBackClicked()
    {
        Debug.Log("Back to Mode Select");
        selectedCharacterIndex = -1;
        selectedCharacter = null;
        GameManager.Instance.ChangeState(GameManager.GameState.ModeSelect);
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        if (characterSelectPanel != null)
        {
            characterSelectPanel.SetActive(newState == GameManager.GameState.CharacterSelect);
        }
    }
}
