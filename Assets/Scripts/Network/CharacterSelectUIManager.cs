using System;
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
    private bool pendingRoomCharacterUpdate = false;
    private string confirmedCharacterBeforeSelection;
    private bool multiplayerSelectionContext = false;

    private string[] availableCharacters = { "Knight", "Archer", "Mage", "Rogue", "Paladin" };
    private readonly HashSet<string> takenRoomCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<Button> boundCharacterButtons = new List<Button>();

    private void OnEnable()
    {
        Debug.Log("CharacterSelectUIManager.OnEnable called, gameObject: " + gameObject.name);
        
        // Print hierarchy for debugging
        PrintHierarchy();

        selectedCharacterIndex = -1;
        selectedCharacter = null;
        pendingRoomCharacterUpdate = false;
        takenRoomCharacters.Clear();
        multiplayerSelectionContext = GameManager.Instance != null && GameManager.Instance.IsMultiplayer;
        
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

        if (CharactersClient.Instance != null)
        {
            CharactersClient.Instance.OnGetCharactersComplete += HandleGetCharactersComplete;
            CharactersClient.Instance.OnSetUserCharacterComplete += HandleSetUserCharacterComplete;
        }

        if (RoomClient.Instance != null)
        {
            RoomClient.Instance.OnGetPlayersComplete += HandleGetRoomPlayersComplete;
            RoomClient.Instance.OnSetPlayerCharacterComplete += HandleSetPlayerCharacterComplete;
        }

        if (GameManager.Instance != null)
        {
            Debug.Log("CharacterSelectUIManager.OnEnable: GameManager found. Subscribing to state changes.");
            confirmedCharacterBeforeSelection = GameManager.Instance.IsMultiplayer ? null : GameManager.Instance.SelectedCharacter;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            gameManagerSubscribed = true;
            HandleStateChanged(GameManager.Instance.CurrentState);
        }
        else
        {
            Debug.LogWarning("CharacterSelectUIManager.OnEnable: GameManager.Instance is NULL. Will retry in Update.");
        }

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

        if (RoomClient.Instance != null)
        {
            RoomClient.Instance.OnGetPlayersComplete -= HandleGetRoomPlayersComplete;
            RoomClient.Instance.OnSetPlayerCharacterComplete -= HandleSetPlayerCharacterComplete;
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
            confirmedCharacterBeforeSelection = GameManager.Instance.IsMultiplayer ? null : GameManager.Instance.SelectedCharacter;
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
        if (pendingRoomCharacterUpdate)
        {
            return;
        }

        if (index < 0 || index >= availableCharacters.Length)
        {
            return;
        }

        if (!IsCharacterSelectable(index))
        {
            string blockedCharacter = availableCharacters[index];
            Debug.LogWarning($"Character '{blockedCharacter}' is already taken in this room.");
            if (characterNameText != null)
            {
                characterNameText.text = "Character already taken";
            }
            return;
        }

        selectedCharacterIndex = index;
        selectedCharacter = availableCharacters[index];

        Debug.Log($"Character selected: {selectedCharacter}");

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

        RefreshCharacterButtonStates();

        if (GameManager.Instance != null)
        {
            if (!multiplayerSelectionContext)
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

        if (!IsCharacterSelectable(selectedCharacterIndex))
        {
            Debug.LogWarning($"Character '{selectedCharacter}' is already taken.");
            if (characterNameText != null)
            {
                characterNameText.text = "Character already taken";
            }
            return;
        }

        Debug.Log($"Character confirmed: {selectedCharacter}");

        string userId = AuthClient.Instance != null ? AuthClient.Instance.GetStoredUserId() : string.Empty;

        // Multiplayer: wait for server confirmation before returning to RoomLobby
        if (multiplayerSelectionContext)
        {
            if (pendingRoomCharacterUpdate)
            {
                return;
            }

            if (RoomClient.Instance == null || string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode) || string.IsNullOrEmpty(userId))
            {
                Debug.LogWarning("[CharacterSelectUIManager] Cannot save room character because room or user info is missing.");
                return;
            }

            pendingRoomCharacterUpdate = true;
            if (selectButton != null)
            {
                selectButton.interactable = false;
            }

            Debug.Log("[CharacterSelectUIManager] Multiplayer selected → Waiting for server confirmation");

            RoomClient.Instance.SetPlayerCharacterInRoom(GameManager.Instance.CurrentRoomCode, userId, selectedCharacter);

            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetSelectedCharacter(selectedCharacter);
        }

        if (!string.IsNullOrEmpty(userId) && CharactersClient.Instance != null)
        {
            CharactersClient.Instance.SetUserCharacter(userId, selectedCharacter);
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

        GameSceneLoader.LoadGameplayScene("SampleScene");
    }

    private void OnBackClicked()
    {
        if (pendingRoomCharacterUpdate)
        {
            Debug.LogWarning("Please wait for the character update to finish before going back.");
            return;
        }

        Debug.Log(multiplayerSelectionContext ? "Back to Room Lobby" : "Back to Mode Select");
        if (GameManager.Instance != null && !multiplayerSelectionContext)
        {
            GameManager.Instance.SetSelectedCharacter(confirmedCharacterBeforeSelection);
        }
        selectedCharacterIndex = -1;
        selectedCharacter = null;
        if (selectButton != null)
        {
            selectButton.interactable = true;
        }
        RefreshCharacterButtonStates();
        if (GameManager.Instance != null)
        {
            if (multiplayerSelectionContext)
            {
                if (characterSelectPanel != null)
                {
                    characterSelectPanel.SetActive(false);
                }

                RoomUIManager roomUIManager = FindAnyObjectByType<RoomUIManager>(FindObjectsInactive.Include);
                if (roomUIManager != null)
                {
                    roomUIManager.ShowRoomPanel();
                }

                GameManager.Instance.ChangeState(GameManager.GameState.RoomLobby);
            }
            else
            {
                GameManager.Instance.ChangeState(GameManager.GameState.ModeSelect);
            }
        }
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

        if (shouldShow)
        {
            selectedCharacterIndex = -1;
            selectedCharacter = null;
            pendingRoomCharacterUpdate = false;

            if (selectButton != null)
            {
                selectButton.interactable = true;
            }

            if (characterNameText != null)
            {
                characterNameText.text = "Select a Character";
            }

            if (selectedCharacterImage != null)
            {
                selectedCharacterImage.sprite = null;
                selectedCharacterImage.enabled = false;
            }

            if (GameManager.Instance != null)
            {
                multiplayerSelectionContext = GameManager.Instance.IsMultiplayer;
                confirmedCharacterBeforeSelection = multiplayerSelectionContext ? null : GameManager.Instance.SelectedCharacter;
            }

            takenRoomCharacters.Clear();

            RefreshAvailableCharacters();

            if (RoomClient.Instance != null && GameManager.Instance != null && multiplayerSelectionContext &&
                !string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
            {
                RoomClient.Instance.GetRoomPlayers(GameManager.Instance.CurrentRoomCode);
            }
        }
    }

    private void HandleGetCharactersComplete(CharactersClient.CharactersListResult result)
    {
        if (!result.success || result.characters == null || result.characters.Length == 0)
        {
            Debug.LogWarning($"Failed to load characters: {result.error}");
            return;
        }

        if (result.characters.Length == characterButtons.Length)
            availableCharacters = result.characters;
        if (multiplayerSelectionContext)
        {
            ApplyTakenCharacters(result.takenCharacters);
        }
        else
        {
            takenRoomCharacters.Clear();
        }
        RefreshCharacterButtonStates();

        Debug.Log("CharacterSelectUIManager: Characters loaded from backend.");
    }

    private void HandleGetRoomPlayersComplete(RoomClient.RoomDetailsResult result)
    {
        if (!result.success || result.players == null)
        {
            Debug.LogWarning($"Failed to load room players: {result.error}");
            return;
        }

        if (GameManager.Instance == null || string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            return;
        }

        if (!string.IsNullOrEmpty(result.requestedRoomCode) &&
            !string.Equals(result.requestedRoomCode, GameManager.Instance.CurrentRoomCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        takenRoomCharacters.Clear();

        string currentUsername = GameManager.Instance != null ? GameManager.Instance.CurrentUsername : null;
        foreach (var player in result.players)
        {
            if (player == null || string.IsNullOrEmpty(player.character))
                continue;

            bool isSelf = !string.IsNullOrEmpty(currentUsername) &&
                          !string.IsNullOrEmpty(player.username) &&
                          string.Equals(player.username, currentUsername, StringComparison.OrdinalIgnoreCase);
            if (!isSelf)
            {
                takenRoomCharacters.Add(player.character);
            }
        }

        if (multiplayerSelectionContext &&
            !string.IsNullOrEmpty(selectedCharacter) &&
            takenRoomCharacters.Contains(selectedCharacter) &&
            (string.IsNullOrEmpty(confirmedCharacterBeforeSelection) ||
             !string.Equals(selectedCharacter, confirmedCharacterBeforeSelection, StringComparison.OrdinalIgnoreCase)))
        {
            selectedCharacter = confirmedCharacterBeforeSelection;
            selectedCharacterIndex = FindCharacterIndex(selectedCharacter);
            RefreshSelectionPreview();
        }

        RefreshCharacterButtonStates();
    }

    private void HandleSetPlayerCharacterComplete(RoomClient.RoomResult result)
    {
        pendingRoomCharacterUpdate = false;

        if (selectButton != null)
        {
            selectButton.interactable = true;
        }

        if (!result.success)
        {
            Debug.LogWarning($"Failed to save room character: {result.error}");
            if (multiplayerSelectionContext)
            {
                selectedCharacter = null;
                selectedCharacterIndex = -1;
                RefreshSelectionPreview();
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetRoomSelectedCharacter(null);
                }
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.SetSelectedCharacter(confirmedCharacterBeforeSelection);
                selectedCharacter = confirmedCharacterBeforeSelection;
                selectedCharacterIndex = FindCharacterIndex(selectedCharacter);
                RefreshSelectionPreview();
            }

            if (characterNameText != null && !string.IsNullOrEmpty(result.error))
            {
                characterNameText.text = result.error;
            }
            return;
        }

        confirmedCharacterBeforeSelection = selectedCharacter;

        if (multiplayerSelectionContext && GameManager.Instance != null)
        {
            GameManager.Instance.SetRoomSelectedCharacter(selectedCharacter);
        }

        if (!multiplayerSelectionContext)
        {
            string userId = AuthClient.Instance != null ? AuthClient.Instance.GetStoredUserId() : string.Empty;
            if (!string.IsNullOrEmpty(userId) && CharactersClient.Instance != null)
            {
                CharactersClient.Instance.SetUserCharacter(userId, selectedCharacter);
            }
        }

        RefreshCharacterButtonStates();

        RoomUIManager roomUIManager = FindAnyObjectByType<RoomUIManager>(FindObjectsInactive.Include);
        if (roomUIManager != null)
        {
            roomUIManager.ShowRoomPanel();
        }

        if (characterSelectPanel != null)
        {
            characterSelectPanel.SetActive(false);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameManager.GameState.RoomLobby);
            if (multiplayerSelectionContext)
            {
                GameManager.Instance.SetRoomSelectedCharacter(selectedCharacter);
            }
        }
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

    private void RefreshAvailableCharacters()
    {
        if (CharactersClient.Instance == null)
            return;

        string roomCode = null;
        if (multiplayerSelectionContext && GameManager.Instance != null)
        {
            roomCode = GameManager.Instance.CurrentRoomCode;
        }

        CharactersClient.Instance.GetAvailableCharacters(roomCode);
    }

    private void RefreshCharacterButtonStates()
    {
        if (characterButtons == null)
            return;

        for (int i = 0; i < characterButtons.Length; i++)
        {
            if (characterButtons[i] == null)
                continue;

            bool selectable = IsCharacterSelectable(i);
            bool isSelected = i == selectedCharacterIndex && !string.IsNullOrEmpty(selectedCharacter);
            characterButtons[i].interactable = !pendingRoomCharacterUpdate && (selectable || isSelected);

            Image buttonImage = characterButtons[i].GetComponent<Image>();
            if (buttonImage != null)
            {
                if (isSelected)
                {
                    buttonImage.color = Color.green;
                }
                else if (!selectable)
                {
                    buttonImage.color = Color.gray;
                }
                else
                {
                    buttonImage.color = Color.white;
                }
            }
        }
    }

    private void RefreshSelectionPreview()
    {
        if (selectedCharacterIndex < 0 || selectedCharacterIndex >= availableCharacters.Length)
        {
            if (selectedCharacterImage != null)
            {
                selectedCharacterImage.sprite = null;
                selectedCharacterImage.enabled = false;
            }

            if (characterNameText != null)
            {
                characterNameText.text = "Select a Character";
            }

            RefreshCharacterButtonStates();
            return;
        }

        if (characterNameText != null)
        {
            characterNameText.text = selectedCharacter;
        }

        if (selectedCharacterImage != null)
        {
            Sprite previewSprite = GetButtonSprite(selectedCharacterIndex);
            if (previewSprite != null)
            {
                selectedCharacterImage.sprite = previewSprite;
                selectedCharacterImage.enabled = true;
            }
        }

        RefreshCharacterButtonStates();
    }

    private void ApplyTakenCharacters(string[] takenCharacters)
    {
        takenRoomCharacters.Clear();
        if (takenCharacters == null)
        {
            return;
        }

        string ownCharacter = GameManager.Instance != null ? GameManager.Instance.RoomSelectedCharacter : null;
        foreach (var character in takenCharacters)
        {
            if (string.IsNullOrEmpty(character))
                continue;

            if (!string.IsNullOrEmpty(ownCharacter) &&
                string.Equals(character, ownCharacter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            takenRoomCharacters.Add(character);
        }
    }

    private bool IsCharacterSelectable(int index)
    {
        if (availableCharacters == null || index < 0 || index >= availableCharacters.Length)
            return false;

        return IsCharacterSelectable(availableCharacters[index]);
    }

    private bool IsCharacterSelectable(string character)
    {
        if (string.IsNullOrEmpty(character))
            return false;

        if (!takenRoomCharacters.Contains(character))
            return true;

        return !string.IsNullOrEmpty(selectedCharacter) &&
               string.Equals(selectedCharacter, character, StringComparison.OrdinalIgnoreCase);
    }

    private int FindCharacterIndex(string character)
    {
        if (string.IsNullOrEmpty(character) || availableCharacters == null)
            return -1;

        for (int i = 0; i < availableCharacters.Length; i++)
        {
            if (string.Equals(availableCharacters[i], character, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
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
