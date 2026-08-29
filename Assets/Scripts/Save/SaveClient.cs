using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SaveClient : MonoBehaviour
{
    public string baseUrl = ServerUrlSettings.ProductionBaseUrl;
    public int timeoutSeconds = 10;

    static SaveClient instance;
    string EffectiveBaseUrl => ServerEndpointConfig.Resolve(baseUrl);

    public static SaveClient Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<SaveClient>();
                if (instance == null)
                {
                    GameObject go = new GameObject("SaveClient");
                    instance = go.AddComponent<SaveClient>();
                }
            }

            return instance;
        }
    }

    [Serializable]
    public class SaveEnvelope
    {
        public SingleRunSaveData saveData;
    }

    [Serializable]
    public class SaveResponse
    {
        public bool success;
        public bool hasSave;
        public SingleRunSaveData saveData;
        public string message;
        public string updatedAt;
    }

    public class SaveResult
    {
        public bool success;
        public bool hasSave;
        public string error;
        public SingleRunSaveData saveData;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void GetCloudSave(Action<SaveResult> callback)
    {
        string token = GetToken();
        if (string.IsNullOrEmpty(token))
        {
            callback?.Invoke(new SaveResult { success = false, error = "Not authenticated." });
            return;
        }

        StartCoroutine(GetCloudSaveCoroutine(token, callback));
    }

    public void UploadCloudSave(SingleRunSaveData saveData, Action<SaveResult> callback = null)
    {
        string token = GetToken();
        if (string.IsNullOrEmpty(token))
        {
            callback?.Invoke(new SaveResult { success = false, error = "Not authenticated." });
            return;
        }

        if (saveData == null)
        {
            callback?.Invoke(new SaveResult { success = false, error = "Save data is empty." });
            return;
        }

        StartCoroutine(UploadCloudSaveCoroutine(token, saveData, callback));
    }

    public void DeleteCloudSave(Action<SaveResult> callback = null)
    {
        string token = GetToken();
        if (string.IsNullOrEmpty(token))
        {
            callback?.Invoke(new SaveResult { success = false, error = "Not authenticated." });
            return;
        }

        StartCoroutine(DeleteCloudSaveCoroutine(token, callback));
    }

    IEnumerator GetCloudSaveCoroutine(string token, Action<SaveResult> callback)
    {
        using (UnityWebRequest req = UnityWebRequest.Get(EffectiveBaseUrl + "/saves/me"))
        {
            AddAuth(req, token);
            req.timeout = timeoutSeconds;
            yield return req.SendWebRequest();
            callback?.Invoke(ParseResponse(req, "Get cloud save"));
        }
    }

    IEnumerator UploadCloudSaveCoroutine(string token, SingleRunSaveData saveData, Action<SaveResult> callback)
    {
        string json = JsonUtility.ToJson(new SaveEnvelope { saveData = saveData });
        using (UnityWebRequest req = new UnityWebRequest(EffectiveBaseUrl + "/saves/me", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            AddAuth(req, token);
            req.timeout = timeoutSeconds;
            yield return req.SendWebRequest();
            callback?.Invoke(ParseResponse(req, "Upload cloud save"));
        }
    }

    IEnumerator DeleteCloudSaveCoroutine(string token, Action<SaveResult> callback)
    {
        using (UnityWebRequest req = new UnityWebRequest(EffectiveBaseUrl + "/saves/me", "DELETE"))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            AddAuth(req, token);
            req.timeout = timeoutSeconds;
            yield return req.SendWebRequest();
            callback?.Invoke(ParseResponse(req, "Delete cloud save"));
        }
    }

    SaveResult ParseResponse(UnityWebRequest req, string action)
    {
        bool ok = req.result == UnityWebRequest.Result.Success;
        string body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
        if (!ok)
        {
            return new SaveResult
            {
                success = false,
                error = GetErrorMessage(req, action)
            };
        }

        try
        {
            SaveResponse response = JsonUtility.FromJson<SaveResponse>(body);
            if (response == null)
            {
                return new SaveResult
                {
                    success = false,
                    error = $"{action}: empty server response."
                };
            }

            response.saveData?.EnsureLists();
            return new SaveResult
            {
                success = response.success,
                hasSave = response.hasSave,
                saveData = response.saveData,
                error = response.success ? null : response.message
            };
        }
        catch (Exception exception)
        {
            return new SaveResult
            {
                success = false,
                error = $"{action}: failed to parse response. {exception.Message}"
            };
        }
    }

    string GetErrorMessage(UnityWebRequest req, string action)
    {
        string body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                SaveResponse response = JsonUtility.FromJson<SaveResponse>(body);
                if (response != null && !string.IsNullOrWhiteSpace(response.message))
                    return response.message;
            }
            catch
            {
                // Ignore parse errors and fall back to UnityWebRequest error.
            }
        }

        return $"{action} failed: {req.responseCode} {req.error}";
    }

    void AddAuth(UnityWebRequest req, string token)
    {
        req.SetRequestHeader("Authorization", "Bearer " + token);
    }

    string GetToken()
    {
        return AuthClient.Instance != null ? AuthClient.Instance.GetStoredToken() : string.Empty;
    }
}
