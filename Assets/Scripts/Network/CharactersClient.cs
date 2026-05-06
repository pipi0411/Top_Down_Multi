using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class CharactersClient : MonoBehaviour
{
    public string baseUrl = "http://localhost:3000";
    public int timeoutSeconds = 10;

    private static CharactersClient instance;
    public static CharactersClient Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<CharactersClient>();
                if (instance == null)
                {
                    GameObject go = new GameObject("CharactersClient");
                    instance = go.AddComponent<CharactersClient>();
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

    [Serializable]
    public class CharactersListResponse { public string[] characters; }

    [Serializable]
    public class CharacterResponse { public string character; }

    public class CharactersListResult { public bool success; public string error; public string[] characters; }
    public class CharacterResult { public bool success; public string error; public string character; }

    public event Action<CharactersListResult> OnGetCharactersComplete;
    public event Action<CharacterResult> OnGetUserCharacterComplete;
    public event Action<CharacterResult> OnSetUserCharacterComplete;

    public void GetAvailableCharacters()
    {
        StartCoroutine(GetAvailableCharactersCoroutine());
    }

    IEnumerator GetAvailableCharactersCoroutine()
    {
        string url = baseUrl + "/characters";
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = timeoutSeconds;
            yield return req.SendWebRequest();

            var result = new CharactersListResult();
            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                result.error = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                Debug.LogError("GetAvailableCharacters error: " + result.error);
            }
            else
            {
                try
                {
                    var res = JsonUtility.FromJson<CharactersListResponse>(req.downloadHandler.text);
                    result.success = true;
                    result.characters = res.characters;
                }
                catch (Exception e)
                {
                    result.success = false;
                    result.error = "Failed to parse characters: " + e.Message;
                    Debug.LogError(result.error);
                }
            }

            OnGetCharactersComplete?.Invoke(result);
        }
    }

    public void GetUserCharacter(string userId)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var r = new CharacterResult { success = false, error = "Not authenticated." };
            OnGetUserCharacterComplete?.Invoke(r);
            return;
        }

        StartCoroutine(GetUserCharacterCoroutine(userId, token));
    }

    IEnumerator GetUserCharacterCoroutine(string userId, string token)
    {
        string url = baseUrl + "/characters/users/" + UnityWebRequest.EscapeURL(userId) + "/character";
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;
            yield return req.SendWebRequest();

            var result = new CharacterResult();
            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                result.error = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                Debug.LogError("GetUserCharacter error: " + result.error);
            }
            else
            {
                try
                {
                    var res = JsonUtility.FromJson<CharacterResponse>(req.downloadHandler.text);
                    result.success = true;
                    result.character = res.character;
                }
                catch (Exception e)
                {
                    result.success = false;
                    result.error = "Failed to parse user character: " + e.Message;
                    Debug.LogError(result.error);
                }
            }

            OnGetUserCharacterComplete?.Invoke(result);
        }
    }

    [Serializable]
    private class CharacterRequest { public string character; }

    public void SetUserCharacter(string userId, string character)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var r = new CharacterResult { success = false, error = "Not authenticated." };
            OnSetUserCharacterComplete?.Invoke(r);
            return;
        }

        StartCoroutine(SetUserCharacterCoroutine(userId, character, token));
    }

    IEnumerator SetUserCharacterCoroutine(string userId, string character, string token)
    {
        string url = baseUrl + "/characters/users/" + UnityWebRequest.EscapeURL(userId) + "/character";
        var body = new CharacterRequest { character = character };
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = new CharacterResult();
            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                result.error = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                Debug.LogError("SetUserCharacter error: " + result.error);
            }
            else
            {
                result.success = true;
            }

            OnSetUserCharacterComplete?.Invoke(result);
        }
    }
}
