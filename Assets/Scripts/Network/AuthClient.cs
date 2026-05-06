using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AuthClient : MonoBehaviour
{
    public string baseUrl = "http://localhost:3000";
    public int timeoutSeconds = 10;

    [System.Serializable]
    public class AuthRequest
    {
        public string username;
        public string password;
    }

    [System.Serializable]
    public class LoginResponse
    {
        public string token;
        public string userId;
    }

    [System.Serializable]
    public class ErrorResponse
    {
        public string message;
        public int code;
    }

    public class AuthResult
    {
        public bool success;
        public string error;
        public string token;
        public string userId;
    }

    // Validate stored token by calling a protected profile endpoint.
    // Calls callback(true) if token is valid, false otherwise.
    public void ValidateToken(Action<bool> callback)
    {
        string token = GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            callback?.Invoke(false);
            return;
        }

        StartCoroutine(ValidateTokenCoroutine(token, callback));
    }

    IEnumerator ValidateTokenCoroutine(string token, Action<bool> callback)
    {
        using (var req = UnityWebRequest.Get(baseUrl + "/auth/profile"))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
            {
                try
                {
                    // Optionally parse profile to refresh stored info
                    // For now, treat 200 as valid token
                    callback?.Invoke(true);
                    yield break;
                }
                catch (Exception)
                {
                    callback?.Invoke(false);
                    yield break;
                }
            }
            else
            {
                // Token invalid or expired
                ClearAuth();
                callback?.Invoke(false);
                yield break;
            }
        }
    }

    // Callbacks
    public event Action<AuthResult> OnRegisterComplete;
    public event Action<AuthResult> OnLoginComplete;

    private static AuthClient instance;

    public static AuthClient Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<AuthClient>();
                if (instance == null)
                {
                    GameObject go = new GameObject("AuthClient");
                    instance = go.AddComponent<AuthClient>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Register(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            var result = new AuthResult { success = false, error = "Username and password cannot be empty" };
            OnRegisterComplete?.Invoke(result);
            return;
        }
        StartCoroutine(RegisterCoroutine(username, password));
    }

    public void Login(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            var result = new AuthResult { success = false, error = "Username and password cannot be empty" };
            OnLoginComplete?.Invoke(result);
            return;
        }
        StartCoroutine(LoginCoroutine(username, password));
    }

    IEnumerator RegisterCoroutine(string username, string password)
    {
        var body = new AuthRequest { username = username, password = password };
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(baseUrl + "/auth/register", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = new AuthResult();

            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                if (req.responseCode == 409)
                {
                    result.error = "Username already exists";
                }
                else if (req.responseCode == 400)
                {
                    result.error = "Invalid request. Check password requirements.";
                }
                else if (req.result == UnityWebRequest.Result.ConnectionError)
                {
                    result.error = $"Connection failed: {req.error}";
                }
                else
                {
                    result.error = $"Error: {req.responseCode} - {req.downloadHandler.text}";
                }
            }
            else
            {
                result.success = true;
                Debug.Log("Register successful: " + req.downloadHandler.text);
            }

            OnRegisterComplete?.Invoke(result);
        }
    }

    IEnumerator LoginCoroutine(string username, string password)
    {
        var body = new AuthRequest { username = username, password = password };
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(baseUrl + "/auth/login", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = new AuthResult();

            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                if (req.responseCode == 401)
                {
                    result.error = "Invalid username or password";
                }
                else if (req.responseCode == 404)
                {
                    result.error = "User not found";
                }
                else if (req.result == UnityWebRequest.Result.ConnectionError)
                {
                    result.error = $"Connection failed: {req.error}";
                }
                else
                {
                    result.error = $"Error: {req.responseCode}";
                }
            }
            else
            {
                try
                {
                    var loginRes = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
                    if (loginRes == null || string.IsNullOrEmpty(loginRes.token))
                    {
                        result.success = false;
                        result.error = "No token received from server";
                    }
                    else
                    {
                        result.success = true;
                        result.token = loginRes.token;
                        result.userId = loginRes.userId;
                        PlayerPrefs.SetString("token", loginRes.token);
                        PlayerPrefs.SetString("userId", loginRes.userId ?? username);
                        PlayerPrefs.SetString("username", username);
                        PlayerPrefs.Save();
                        Debug.Log("Login successful. Token saved.");
                    }
                }
                catch (Exception e)
                {
                    result.success = false;
                    result.error = "Failed to parse login response: " + e.Message;
                }
            }

            if (result != null)
            {
                OnLoginComplete?.Invoke(result);
            }
        }
    }

    public string GetStoredToken()
    {
        return PlayerPrefs.GetString("token", "");
    }

    public string GetStoredUserId()
    {
        return PlayerPrefs.GetString("userId", "");
    }

    public string GetStoredUsername()
    {
        return PlayerPrefs.GetString("username", "");
    }

    public void ClearAuth()
    {
        PlayerPrefs.DeleteKey("token");
        PlayerPrefs.DeleteKey("userId");
        PlayerPrefs.DeleteKey("username");
        PlayerPrefs.Save();
    }
}
