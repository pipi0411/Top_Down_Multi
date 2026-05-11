using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CharacterSelectUIManager : MonoBehaviour
{
    [SerializeField] private GameObject characterSelectPanel;
    
    [Header("Character Selection")]
    [SerializeField] private Button[] characterButtons;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Image selectedCharacterImage;
    [SerializeField] private TMP_Text characterNameText;
    
    private string selectedCharacter;
    private int selectedCharacterIndex = -1;
    private bool gameManagerSubscribed = false;

    private string[] availableCharacters = { "Knight", "Archer", "Mage", "Rogue", "Paladin" };
    private readonly List<Button> boundCharacterButtons = new List<Button>();

    private void OnEnable()
    {
        Debug.Log("CharacterSelectUIManager.OnEnable called, gameObject: " + gameObject.name);
        
        // Print hierarchy for debugging
        PrintHierarchy();
        
        // IMPORTANT: Try to find panel but don't return if not found - we'll handle it
        if (characterSelectPanel == null)
        {
            Debug.LogWarning("CharacterSelectUIManager: characterSelectPanel is NULL in inspector! Searching...");
            AutoBindPreviewElements();
            
            if (characterSelectPanel == null)
            {
                Debug.LogError("CharacterSelectUIManager: FAILED to find characterSelectPanel! Will attempt lazy binding.");
            }
            else
            {
                Debug.Log("CharacterSelectUIManager: Found characterSelectPanel via AutoBindPreviewElements: " + characterSelectPanel.name);
            }
        }

        if (characterSelectPanel != null)
        {
            Debug.Log($"CharacterSelectUIManager: characterSelectPanel ready = {characterSelectPanel.name}, active={characterSelectPanel.activeSelf}");
        }
        else
        {
            Debug.LogWarning("CharacterSelectUIManager: characterSelectPanel is still NULL after search!");
        }
        
        // IMPORTANT: Continue with binding buttons and handlers even if panel is null
        // We can do lazy initialization when the panel is actually needed
        
        // Only call AutoBindPreviewElements once - don't call it again since we already did in the if block above
        // BindButtons() depends on characterSelectPanel being set, so do it after AutoBindPreviewElements
        if (characterSelectPanel != null)
        {
            BindButtons();
        }
        else
        {
            Debug.LogWarning("CharacterSelectUIManager: Skipping BindButtons because characterSelectPanel is null. Will retry on state change.");
        }

        // Setup character buttons
        if (characterButtons != null)
        {
            for (int i = 0; i < characterButtons.Length && i < availableCharacters.Length; i++)
            {
                int index = i; // Local copy for closure
                if (characterButtons[i] == null)
                    continue;

                characterButtons[i].onClick.AddListener(() => OnCharacterSelected(index));

                TMP_Text buttonText = characterButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (buttonText != null)
                {
                    buttonText.text = "";
                }
            }
        }
        else
        {
            Debug.LogWarning("CharacterSelectUIManager: characterButtons is null, skipping button setup.");
        }

        if (selectButton != null)
            selectButton.onClick.AddListener(OnSelectClicked);
        else
            Debug.LogWarning("CharacterSelectUIManager: selectButton is NULL!");
            
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
        else
            Debug.LogWarning("CharacterSelectUIManager: backButton is NULL!");

        if (GameManager.Instance != null)
        {
            Debug.Log("CharacterSelectUIManager.OnEnable: GameManager found. Subscribing to state changes.");
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            gameManagerSubscribed = true;
            HandleStateChanged(GameManager.Instance.CurrentState);
        }
        else
        {
            Debug.LogWarning("CharacterSelectUIManager.OnEnable: GameManager.Instance is NULL. Will retry in Update.");
        }

        if (CharactersClient.Instance != null)
        {
            CharactersClient.Instance.OnGetCharactersComplete += HandleGetCharactersComplete;
            CharactersClient.Instance.OnSetUserCharacterComplete += HandleSetUserCharacterComplete;
            CharactersClient.Instance.GetAvailableCharacters();
        }

        // Reset selection when panel appears
        selectedCharacterIndex = -1;
        if (characterNameText != null)
        {
            characterNameText.text = "Select a Character";
            characterNameText.enabled = true;
        }

        if (selectedCharacterImage != null)
        {
            selectedCharacterImage.sprite = null;
            selectedCharacterImage.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (characterButtons != null)
        {
            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (characterButtons[i] != null)
                {
                    characterButtons[i].onClick.RemoveAllListeners();
                }
            }
        }

        if (selectButton != null)
            selectButton.onClick.RemoveListener(OnSelectClicked);
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);

        if (CharactersClient.Instance != null)
        {
            CharactersClient.Instance.OnGetCharactersComplete -= HandleGetCharactersComplete;
            CharactersClient.Instance.OnSetUserCharacterComplete -= HandleSetUserCharacterComplete;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    private void Update()
    {
        // If GameManager wasn't ready during OnEnable, try to subscribe here
        if (!gameManagerSubscribed && GameManager.Instance != null)
        {
            Debug.Log("CharacterSelectUIManager.Update: GameManager found! Subscribing now.");
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            gameManagerSubscribed = true;
            HandleStateChanged(GameManager.Instance.CurrentState);
        }
    }

    private void OnDestroy()
    {
        // Đảm bảo gỡ đăng ký với GameManager dù OnDisable có được gọi hay chưa
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    private void BindButtons()
    {
        Debug.Log($"BindButtons: characterSelectPanel = {(characterSelectPanel != null ? characterSelectPanel.name : "NULL")}");
        
        if (characterSelectPanel == null)
        {
            Debug.LogWarning("BindButtons: characterSelectPanel is NULL, cannot bind buttons yet. Skipping.");
            return;
        }
        
        if (characterButtons == null || characterButtons.Length == 0)
        {
            var foundButtons = new List<Button>();
            foreach (var button in characterSelectPanel.GetComponentsInChildren<Button>(true))
            {
                if (button == null)
                    continue;

                string n = button.name.ToLowerInvariant();
                if (n.Contains("confirm") || n.Contains("conjirm") || n.Contains("select"))
                {
                    selectButton = button;
                }
                else if (n.Contains("back"))
                {
                    backButton = button;
                }
                else if (n.Contains("character"))
                {
                    foundButtons.Add(button);
                }
            }

            characterButtons = foundButtons.ToArray();
        }

        // Fallback for confirm/back if not explicitly set
        if (selectButton == null || backButton == null)
        {
            foreach (var button in characterSelectPanel.GetComponentsInChildren<Button>(true))
            {
                string n = button.name.ToLowerInvariant();
                if (selectButton == null && (n.Contains("confirm") || n.Contains("conjirm") || n.Contains("select")))
                    selectButton = button;
                if (backButton == null && n.Contains("back"))
                    backButton = button;
            }
        }

        if (characterButtons != null)
        {
            boundCharacterButtons.Clear();
            foreach (var button in characterButtons)
            {
                if (button != null)
                    boundCharacterButtons.Add(button);
            }
            characterButtons = boundCharacterButtons.ToArray();
        }
    }

    private void AutoBindPreviewElements()
    {
        Debug.Log("CharacterSelectUIManager.AutoBindPreviewElements: Starting...");
        
        // Auto-find characterSelectPanel if not assigned
        if (characterSelectPanel == null)
        {
            Debug.Log("CharacterSelectUIManager: characterSelectPanel not assigned, searching for it...");
            
            // First try to find by name in immediate children
            Transform panelTransform = transform.Find("CharacterSelect");
            if (panelTransform != null)
            {
                characterSelectPanel = panelTransform.gameObject;
                Debug.Log($"✓ Found CharacterSelect by name: {characterSelectPanel.name}");
            }
            else
            {
                Debug.Log("✗ CharacterSelect not found by name, searching children...");
                // Try to find any child with "character" in name
                foreach (Transform child in transform)
                {
                    if (child.name.ToLowerInvariant().Contains("character"))
                    {
                        characterSelectPanel = child.gameObject;
                        Debug.Log($"✓ Found panel by search: {characterSelectPanel.name}");
                        break;
                    }
                }
            }
            
            // Last resort: use the first child
            if (characterSelectPanel == null && transform.childCount > 0)
            {
                characterSelectPanel = transform.GetChild(0).gameObject;
                Debug.Log($"⚠ Using first child as panel: {characterSelectPanel.name}");
            }
            
            if (characterSelectPanel == null)
            {
                Debug.LogError("✗ FAILED to find characterSelectPanel! No children available.");
                Debug.Log($"Debug: gameObject={gameObject.name}, childCount={transform.childCount}");
            }
            else
            {
                Debug.Log($"✓ AutoBindPreviewElements: Successfully found characterSelectPanel = {characterSelectPanel.name}");
            }
        }
        
        if (selectedCharacterImage == null)
        {
            Transform imgTransform = characterSelectPanel.transform.Find("ImgCharacter");
            if (imgTransform != null)
            {
                selectedCharacterImage = imgTransform.GetComponent<Image>();
            }
        }

        if (characterNameText == null)
        {
            TMP_Text[] texts = characterSelectPanel.GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null)
                    continue;

                string lower = text.name.ToLowerInvariant();
                if (lower.Contains("new text") || lower.Contains("selected") || lower.Contains("character") || lower.Contains("text"))
                {
                    characterNameText = text;
                    break;
                }
            }
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
                characterNameText.text = selectedCharacter;
            }

            if (selectedCharacterImage != null)
            {
                Sprite previewSprite = GetButtonSprite(index);
                if (previewSprite != null)
                {
                    selectedCharacterImage.sprite = previewSprite;
                    selectedCharacterImage.enabled = true;
                }
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

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetSelectedCharacter(selectedCharacter);
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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetSelectedCharacter(selectedCharacter);
        }

        // Save character to backend if possible
        string userId = AuthClient.Instance != null ? AuthClient.Instance.GetStoredUserId() : string.Empty;
        if (!string.IsNullOrEmpty(userId) && CharactersClient.Instance != null)
        {
            CharactersClient.Instance.SetUserCharacter(userId, selectedCharacter);
        }

        // Multiplayer: set selection and return to RoomLobby (do NOT require local prefab)
        if (GameManager.Instance != null && GameManager.Instance.IsMultiplayer)
        {
            Debug.Log("[CharacterSelectUIManager] Multiplayer selected → Returning to RoomLobby");

            if (GameManager.Instance != null && RoomClient.Instance != null &&
                !string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode) &&
                !string.IsNullOrEmpty(userId))
            {
                RoomClient.Instance.SetPlayerCharacterInRoom(GameManager.Instance.CurrentRoomCode, userId, selectedCharacter);
            }

            RoomUIManager roomUIManager = FindAnyObjectByType<RoomUIManager>(FindObjectsInactive.Include);
            if (roomUIManager != null)
            {
                roomUIManager.ShowRoomPanel();
            }

            if (characterSelectPanel != null)
                characterSelectPanel.SetActive(false);

            if (GameManager.Instance != null)
                GameManager.Instance.ChangeState(GameManager.GameState.RoomLobby);

            return;
        }

        // Single-player: require prefab and load game scene
        CharacterPrefabManager prefabManager = CharacterPrefabManager.Instance;
        if (prefabManager == null)
        {
            Debug.LogError("[CharacterSelectUIManager] CharacterPrefabManager not found! Cannot start single-player game.");
            return;
        }

        GameObject selectedPrefab = prefabManager.GetPrefabForCharacter(selectedCharacter);
        if (selectedPrefab == null)
        {
            Debug.LogError($"[CharacterSelectUIManager] Prefab not found for character: {selectedCharacter}");
            return;
        }

        Debug.Log($"[CharacterSelectUIManager] ✓ Character '{selectedCharacter}' → Prefab '{selectedPrefab.name}' verified");

        Debug.Log("[CharacterSelectUIManager] Single player selected → Loading game scene with character: " + selectedCharacter);
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(GameManager.GameState.GameStarting);

        SceneManager.LoadScene("SampleScene");
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
        Debug.Log($"[CharacterSelectUIManager.HandleStateChanged] newState = {newState}");
        Debug.Log($"[CharacterSelectUIManager.HandleStateChanged] characterSelectPanel = {(characterSelectPanel != null ? characterSelectPanel.name : "NULL")}");
        
        // If panel is still not found, try again (lazy binding)
        if (characterSelectPanel == null && newState == GameManager.GameState.CharacterSelect)
        {
            Debug.LogWarning("[CharacterSelectUIManager.HandleStateChanged] Panel still NULL, attempting lazy bind...");
            AutoBindPreviewElements();
            
            if (characterSelectPanel != null)
            {
                Debug.Log("[CharacterSelectUIManager.HandleStateChanged] ✓ Lazy bind successful! Panel found: " + characterSelectPanel.name);
                BindButtons();
            }
            else
            {
                Debug.LogError("[CharacterSelectUIManager.HandleStateChanged] ✗ Lazy bind failed! Panel still NULL after retry.");
                return;
            }
        }
        
        if (characterSelectPanel == null || !gameObject.activeInHierarchy)
        {
            Debug.LogError("[CharacterSelectUIManager.HandleStateChanged] characterSelectPanel is NULL! Cannot show/hide panel!");
            return;
        }
        
        bool shouldShow = (newState == GameManager.GameState.CharacterSelect);
        Debug.Log($"[CharacterSelectUIManager.HandleStateChanged] Setting characterSelectPanel.SetActive({shouldShow})");
        
        characterSelectPanel.SetActive(shouldShow);
        
        Debug.Log($"[CharacterSelectUIManager.HandleStateChanged] After SetActive - panel.activeSelf = {characterSelectPanel.activeSelf}, panel.activeInHierarchy = {characterSelectPanel.activeInHierarchy}");
    }

    private void HandleGetCharactersComplete(CharactersClient.CharactersListResult result)
    {
        if (!result.success || result.characters == null || result.characters.Length == 0)
        {
            Debug.LogWarning($"Failed to load characters: {result.error}");
            return;
        }

        availableCharacters = result.characters;

        for (int i = 0; i < characterButtons.Length && i < availableCharacters.Length; i++)
        {
            if (characterButtons[i] == null)
                continue;

            TMP_Text buttonText = characterButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (buttonText != null)
            {
                buttonText.text = "";
            }
        }

        Debug.Log("CharacterSelectUIManager: Characters loaded from backend.");
    }

    private Sprite GetButtonSprite(int index)
    {
        if (index < 0 || index >= characterButtons.Length || characterButtons[index] == null)
            return null;

        Image buttonImage = characterButtons[index].GetComponent<Image>();
        if (buttonImage != null && buttonImage.sprite != null)
            return buttonImage.sprite;

        Image childImage = characterButtons[index].GetComponentInChildren<Image>(true);
        if (childImage != null && childImage.sprite != null)
            return childImage.sprite;

        return null;
    }

    private void HandleSetUserCharacterComplete(CharactersClient.CharacterResult result)
    {
        if (result.success)
        {
            Debug.Log($"Character saved on backend: {result.character}");
        }
        else
        {
            Debug.LogWarning($"Failed to save character on backend: {result.error}");
        }
    }

    private void PrintHierarchy()
    {
        Debug.Log($"\n=== CharacterSelectUIManager Hierarchy ===");
        Debug.Log($"Manager GameObject: {gameObject.name}, Active: {gameObject.activeSelf}/{gameObject.activeInHierarchy}");
        Debug.Log($"Children count: {transform.childCount}");
        
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            Debug.Log($"  [{i}] {child.name} - Active: {child.gameObject.activeSelf}/{child.gameObject.activeInHierarchy}");
            
            // Check if it has Canvas or CanvasGroup
            Canvas canvas = child.GetComponent<Canvas>();
            CanvasGroup canvasGroup = child.GetComponent<CanvasGroup>();
            if (canvas != null) Debug.Log($"      └─ Has Canvas component");
            if (canvasGroup != null) Debug.Log($"      └─ Has CanvasGroup component");
        }
        Debug.Log($"=== End Hierarchy ===\n");
    }
}
