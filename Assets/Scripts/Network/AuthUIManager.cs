using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AuthUIManager : MonoBehaviour
{
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;

    [Header("Login UI")]
    [SerializeField] private TMP_InputField loginUsernameInput;
    [SerializeField] private TMP_InputField loginPasswordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private TMP_Text loginStatusText;
    [SerializeField] private Button switchToRegisterButton;

    [Header("Register UI")]
    [SerializeField] private TMP_InputField registerUsernameInput;
    [SerializeField] private TMP_InputField registerPasswordInput;
    [SerializeField] private TMP_InputField registerConfirmPasswordInput;
    [SerializeField] private Button registerButton;
    [SerializeField] private TMP_Text registerStatusText;
    [SerializeField] private Button switchToLoginButton;

    private void OnEnable()
    {
        if (loginPanel == null || registerPanel == null || loginButton == null || registerButton == null || switchToRegisterButton == null || switchToLoginButton == null)
        {
            Debug.LogError("AuthUIManager is missing required references in the Inspector.");
            return;
        }

        ShowLoginPanel();
        
        loginButton.onClick.AddListener(OnLoginClicked);
        registerButton.onClick.AddListener(OnRegisterClicked);
        switchToRegisterButton.onClick.AddListener(ShowRegisterPanel);
        switchToLoginButton.onClick.AddListener(ShowLoginPanel);

        AuthClient.Instance.OnLoginComplete += HandleLoginComplete;
        AuthClient.Instance.OnRegisterComplete += HandleRegisterComplete;

        if (GameManager.Instance != null)
        {
            SubscribeToGameManager();
        }
        else
        {
            Debug.LogWarning("AuthUIManager: GameManager.Instance is null, will retry in Update");
            ShowLoginPanel();
        }
    }

    private void OnDisable()
    {
        if (loginButton != null)
            loginButton.onClick.RemoveListener(OnLoginClicked);
        if (registerButton != null)
            registerButton.onClick.RemoveListener(OnRegisterClicked);
        if (switchToRegisterButton != null)
            switchToRegisterButton.onClick.RemoveListener(ShowRegisterPanel);
        if (switchToLoginButton != null)
            switchToLoginButton.onClick.RemoveListener(ShowLoginPanel);

        if (AuthClient.Instance != null)
        {
            AuthClient.Instance.OnLoginComplete -= HandleLoginComplete;
            AuthClient.Instance.OnRegisterComplete -= HandleRegisterComplete;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnError -= HandleError;
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        gameManagerSubscribed = false;
    }
    private void OnDestroy()
    {
        // Gỡ đăng ký sự kiện khi Object bị hủy
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnError -= HandleError;
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    private bool gameManagerSubscribed = false;

    private void Update()
    {
        if (!gameManagerSubscribed && GameManager.Instance != null)
        {
            Debug.Log("AuthUIManager: GameManager is now available, subscribing");
            SubscribeToGameManager();
        }
    }

    private void SubscribeToGameManager()
    {
        if (gameManagerSubscribed)
            return;

        GameManager.Instance.OnError += HandleError;
        GameManager.Instance.OnStateChanged += HandleStateChanged;
        gameManagerSubscribed = true;
        Debug.Log("AuthUIManager: Successfully subscribed to GameManager");

        HandleStateChanged(GameManager.Instance.CurrentState);
    }

    private void ShowLoginPanel()
    {
        if (loginPanel == null || registerPanel == null)
        {
            return;
        }

        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        ClearUI();
    }

    public void ShowLoginUI()
    {
        ShowLoginPanel();
    }

    private void ShowRegisterPanel()
    {
        if (loginPanel == null || registerPanel == null)
        {
            return;
        }

        loginPanel.SetActive(false);
        registerPanel.SetActive(true);
        ClearUI();
    }

    public void ShowRegisterUI()
    {
        ShowRegisterPanel();
    }

    public void SetVisible(bool visible)
    {
        if (gameObject != null)
        {
            gameObject.SetActive(visible);
        }
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        Debug.Log($"AuthUIManager: State changed to {newState}");
        if (loginPanel == null || registerPanel == null)
        {
            Debug.LogWarning("AuthUIManager: UI panels were destroyed or are missing, skipping state update.");
            return;
        }

        if (newState == GameManager.GameState.Auth)
        {
            ShowLoginPanel();
        }
        else
        {
            loginPanel.SetActive(false);
            registerPanel.SetActive(false);
        }
    }

    private void OnLoginClicked()
    {
        string username = loginUsernameInput.text.Trim();
        string password = loginPasswordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            loginStatusText.text = "Please enter username and password";
            loginStatusText.color = Color.red;
            return;
        }

        loginStatusText.text = "Logging in...";
        loginStatusText.color = Color.yellow;
        loginButton.interactable = false;

        AuthClient.Instance.Login(username, password);
    }

    private void OnRegisterClicked()
    {
        string username = registerUsernameInput.text.Trim();
        string password = registerPasswordInput.text;
        string confirmPassword = registerConfirmPasswordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
        {
            registerStatusText.text = "Please fill in all fields";
            registerStatusText.color = Color.red;
            return;
        }

        if (password != confirmPassword)
        {
            registerStatusText.text = "Passwords do not match";
            registerStatusText.color = Color.red;
            return;
        }

        if (password.Length < 6)
        {
            registerStatusText.text = "Password must be at least 6 characters";
            registerStatusText.color = Color.red;
            return;
        }

        registerStatusText.text = "Registering...";
        registerStatusText.color = Color.yellow;
        registerButton.interactable = false;

        AuthClient.Instance.Register(username, password);
    }

    private void HandleLoginComplete(AuthClient.AuthResult result)
    {
        loginButton.interactable = true;

        if (result.success)
        {
            if (loginStatusText != null)
            {
                loginStatusText.text = "Login successful!";
                loginStatusText.color = Color.green;
            }
            Debug.Log("Login successful, switching to host panel...");
        }
        else
        {
            if (loginStatusText != null)
            {
                loginStatusText.text = result.error;
                loginStatusText.color = Color.red;
            }
        }
    }

    private void HandleRegisterComplete(AuthClient.AuthResult result)
    {
        registerButton.interactable = true;

        if (result.success)
        {
            if (registerStatusText != null)
            {
                registerStatusText.text = "Registration successful! Please login.";
                registerStatusText.color = Color.green;
            }
            Invoke(nameof(ShowLoginPanel), 1.5f);
        }
        else
        {
            if (registerStatusText != null)
            {
                registerStatusText.text = result.error;
                registerStatusText.color = Color.red;
            }
        }
    }

    private void HandleError(string error)
    {
        if (loginStatusText != null)
        {
            loginStatusText.text = error;
            loginStatusText.color = Color.red;
        }

        if (registerStatusText != null)
        {
            registerStatusText.text = error;
            registerStatusText.color = Color.red;
        }
    }

    private void ClearUI()
    {
        if (loginStatusText != null)
        {
            loginStatusText.text = "";
        }

        if (registerStatusText != null)
        {
            registerStatusText.text = "";
        }

        if (loginButton != null)
            loginButton.interactable = true;
        if (registerButton != null)
            registerButton.interactable = true;
    }
}
