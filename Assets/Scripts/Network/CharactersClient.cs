using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class CharactersClient : MonoBehaviour
{
    public string baseUrl = "https://servergame-production-eee3.up.railway.app";
    public int timeoutSeconds = 10;

    private string EffectiveBaseUrl => ServerEndpointConfig.Resolve(baseUrl);

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

    [Serializable]
    public class CharacterSetResponse { public bool success; public string character; }

    [Serializable]
    public class UserProfileResponse
    {
        public string userId;
        public string username;
        public string selectedCharacter;
        public string createdAt;
    }

    public class CharactersListResult { public bool success; public string error; public string[] characters; }
    public class CharacterResult { public bool success; public string error; public string character; }
    public class UserProfileResult { public bool success; public string error; public string userId; public string username; public string selectedCharacter; }

    public event Action<CharactersListResult> OnGetCharactersComplete;
    public event Action<CharacterResult> OnGetUserCharacterComplete;
    public event Action<CharacterResult> OnSetUserCharacterComplete;
    public event Action<UserProfileResult> OnGetUserProfileComplete;

    public void GetAvailableCharacters()
    {
        StartCoroutine(GetAvailableCharactersCoroutine());
    }

    IEnumerator GetAvailableCharactersCoroutine()
    {
        string url = EffectiveBaseUrl + "/characters";
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
        string url = EffectiveBaseUrl + "/characters/users/" + UnityWebRequest.EscapeURL(userId) + "/character";
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
        string url = EffectiveBaseUrl + "/characters/users/" + UnityWebRequest.EscapeURL(userId) + "/character/" + UnityWebRequest.EscapeURL(character);

        using (var req = new UnityWebRequest(url, "POST"))
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
                Debug.LogError("SetUserCharacter error: " + result.error);
            }
            else
            {
                result.success = true;
            }

            OnSetUserCharacterComplete?.Invoke(result);
        }
    }

    public void GetUserProfile(string userId)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var r = new UserProfileResult { success = false, error = "Not authenticated." };
            OnGetUserProfileComplete?.Invoke(r);
            return;
        }

        StartCoroutine(GetUserProfileCoroutine(userId, token));
    }

    IEnumerator GetUserProfileCoroutine(string userId, string token)
    {
        string url = EffectiveBaseUrl + "/characters/users/" + UnityWebRequest.EscapeURL(userId) + "/profile";
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = new UserProfileResult();
            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                result.error = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                Debug.LogError("GetUserProfile error: " + result.error);
            }
            else
            {
                try
                {
                    var profile = JsonUtility.FromJson<UserProfileResponse>(req.downloadHandler.text);
                    result.success = true;
                    result.userId = profile.userId;
                    result.username = profile.username;
                    result.selectedCharacter = profile.selectedCharacter;
                }
                catch (Exception e)
                {
                    result.success = false;
                    result.error = "Failed to parse user profile: " + e.Message;
                    Debug.LogError(result.error);
                }
            }

            OnGetUserProfileComplete?.Invoke(result);
        }
    }
}
