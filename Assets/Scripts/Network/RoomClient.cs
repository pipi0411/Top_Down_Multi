using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class RoomClient : MonoBehaviour
{
    public string baseUrl = "https://servergame-production-eee3.up.railway.app";
    public int timeoutSeconds = 10;

    private string EffectiveBaseUrl => ServerEndpointConfig.Resolve(baseUrl);

    [System.Serializable]
    public class CreateRoomRequest
    {
        public string name;
        public int maxPlayers = 2;
    }

    [System.Serializable]
    public class JoinRoomRequest
    {
        public string roomCode;
    }

    [System.Serializable]
    public class HeartbeatRequest
    {
        public string roomCode;
    }

    [System.Serializable]
    public class CloseRoomRequest
    {
        public string roomCode;
    }

    [System.Serializable]
    public class LeaveRoomRequest
    {
        public string roomCode;
    }

    [System.Serializable]
    public class Room
    {
        public string _id;
        public string roomCode;
        public string name;
        public int maxPlayers;
        public int currentPlayers;
        public string status;
    }

    [System.Serializable]
    public class PlayerInfo
    {
        public string userId;
        public string username;
        public string character;
        public bool isReady;
        public string role;
    }

    [System.Serializable]
    public class RoomDetailsResponse
    {
        public Room room;
        public PlayerInfo[] players;
    }

    [System.Serializable]
    public class RoomResponse
    {
        public Room room;
        public string message;
    }

    [System.Serializable]
    public class RoomActionResponse
    {
        public string message;
        public string roomCode;
    }

    public class RoomResult
    {
        public bool success;
        public string error;
        public Room room;
    }

    public class RoomDetailsResult
    {
        public bool success;
        public string error;
        public Room room;
        public PlayerInfo[] players;
    }

    // Callbacks
    public event Action<RoomResult> OnCreateRoomComplete;
    public event Action<RoomResult> OnJoinRoomComplete;
    public event Action<RoomResult> OnLeaveRoomComplete;
    public event Action<RoomResult> OnCloseRoomComplete;
    public event Action<RoomResult> OnHeartbeatComplete;
    public event Action<RoomDetailsResult> OnGetRoomDetailsComplete;
    public event Action<RoomDetailsResult> OnGetPlayersComplete;
    public event Action<RoomResult> OnSetPlayerStatusComplete;
    public event Action<RoomResult> OnSetPlayerCharacterComplete;
    public event Action<RoomResult> OnStartRoomComplete;

    private static RoomClient instance;
    public static RoomClient Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<RoomClient>();
                if (instance == null)
                {
                    GameObject go = new GameObject("RoomClient");
                    instance = go.AddComponent<RoomClient>();
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

    public void CreateRoom(string roomName, int maxPlayers = 2)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var result = new RoomResult { success = false, error = "Not authenticated. Please login first." };
            OnCreateRoomComplete?.Invoke(result);
            return;
        }

        var body = new CreateRoomRequest { name = roomName, maxPlayers = maxPlayers };
        StartCoroutine(CreateRoomCoroutine(body, token));
    }

    public void JoinRoom(string roomCode)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var result = new RoomResult { success = false, error = "Not authenticated. Please login first." };
            OnJoinRoomComplete?.Invoke(result);
            return;
        }

        if (string.IsNullOrEmpty(roomCode))
        {
            var result = new RoomResult { success = false, error = "Room code cannot be empty." };
            OnJoinRoomComplete?.Invoke(result);
            return;
        }

        var body = new JoinRoomRequest { roomCode = roomCode };
        StartCoroutine(JoinRoomCoroutine(body, token));
    }

    public void LeaveRoom(string roomCode)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var result = new RoomResult { success = false, error = "Not authenticated." };
            OnLeaveRoomComplete?.Invoke(result);
            return;
        }

        var body = new LeaveRoomRequest { roomCode = roomCode };
        StartCoroutine(LeaveRoomCoroutine(body, token));
    }

    public void CloseRoom(string roomCode)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var result = new RoomResult { success = false, error = "Not authenticated." };
            OnCloseRoomComplete?.Invoke(result);
            return;
        }

        var body = new CloseRoomRequest { roomCode = roomCode };
        StartCoroutine(CloseRoomCoroutine(body, token));
    }

    public void SendHeartbeat(string roomCode)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var result = new RoomResult { success = false, error = "Not authenticated." };
            OnHeartbeatComplete?.Invoke(result);
            return;
        }

        var body = new HeartbeatRequest { roomCode = roomCode };
        StartCoroutine(HeartbeatCoroutine(body, token));
    }

    public void GetRoomDetails(string roomCode)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var r = new RoomDetailsResult { success = false, error = "Not authenticated." };
            OnGetRoomDetailsComplete?.Invoke(r);
            return;
        }

        StartCoroutine(GetRoomDetailsCoroutine(roomCode, token));
    }

    public void GetRoomPlayers(string roomCode)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var r = new RoomDetailsResult { success = false, error = "Not authenticated." };
            OnGetPlayersComplete?.Invoke(r);
            return;
        }

        StartCoroutine(GetRoomPlayersCoroutine(roomCode, token));
    }

    public void SetPlayerReady(string roomCode, string userId, bool isReady)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var r = new RoomResult { success = false, error = "Not authenticated." };
            OnSetPlayerStatusComplete?.Invoke(r);
            return;
        }

        StartCoroutine(SetPlayerStatusCoroutine(roomCode, userId, isReady, token));
    }

    public void SetPlayerCharacterInRoom(string roomCode, string userId, string character)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var r = new RoomResult { success = false, error = "Not authenticated." };
            OnSetPlayerCharacterComplete?.Invoke(r);
            return;
        }

        StartCoroutine(SetPlayerCharacterCoroutine(roomCode, userId, character, token));
    }

    public void StartRoom(string roomCode)
    {
        string token = AuthClient.Instance.GetStoredToken();
        if (string.IsNullOrEmpty(token))
        {
            var r = new RoomResult { success = false, error = "Not authenticated." };
            OnStartRoomComplete?.Invoke(r);
            return;
        }

        StartCoroutine(StartRoomCoroutine(roomCode, token));
    }

    IEnumerator CreateRoomCoroutine(CreateRoomRequest body, string token)
    {
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(EffectiveBaseUrl + "/rooms/create", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = HandleResponse(req, "Create Room");
            OnCreateRoomComplete?.Invoke(result);
        }
    }

    IEnumerator JoinRoomCoroutine(JoinRoomRequest body, string token)
    {
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(EffectiveBaseUrl + "/rooms/join", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = HandleResponse(req, "Join Room");
            OnJoinRoomComplete?.Invoke(result);
        }
    }

    IEnumerator LeaveRoomCoroutine(LeaveRoomRequest body, string token)
    {
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(EffectiveBaseUrl + "/rooms/leave", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = HandleResponse(req, "Leave Room");
            OnLeaveRoomComplete?.Invoke(result);
        }
    }

    IEnumerator CloseRoomCoroutine(CloseRoomRequest body, string token)
    {
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(EffectiveBaseUrl + "/rooms/close", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = HandleResponse(req, "Close Room");
            OnCloseRoomComplete?.Invoke(result);
        }
    }

    IEnumerator HeartbeatCoroutine(HeartbeatRequest body, string token)
    {
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(EffectiveBaseUrl + "/rooms/heartbeat", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = HandleResponse(req, "Heartbeat");
            OnHeartbeatComplete?.Invoke(result);
        }
    }

    IEnumerator GetRoomDetailsCoroutine(string roomCode, string token)
    {
        string url = EffectiveBaseUrl + "/rooms/" + UnityWebRequest.EscapeURL(roomCode);
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = new RoomDetailsResult();
            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                result.error = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                Debug.LogError("GetRoomDetails error: " + result.error);
            }
            else
            {
                try
                {
                    var details = JsonUtility.FromJson<RoomDetailsResponse>(req.downloadHandler.text);
                    result.success = true;
                    result.room = details.room;
                    result.players = details.players;
                }
                catch (Exception e)
                {
                    result.success = false;
                    result.error = "Failed to parse room details: " + e.Message;
                    Debug.LogError(result.error);
                }
            }

            OnGetRoomDetailsComplete?.Invoke(result);
        }
    }

    IEnumerator GetRoomPlayersCoroutine(string roomCode, string token)
    {
        string url = EffectiveBaseUrl + "/rooms/" + UnityWebRequest.EscapeURL(roomCode) + "/players";
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = new RoomDetailsResult();
            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                result.error = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                Debug.LogError("GetRoomPlayers error: " + result.error);
            }
            else
            {
                try
                {
                    var details = JsonUtility.FromJson<RoomDetailsResponse>(req.downloadHandler.text);
                    result.success = true;
                    result.room = details.room;
                    result.players = details.players;
                }
                catch (Exception e)
                {
                    result.success = false;
                    result.error = "Failed to parse players: " + e.Message;
                    Debug.LogError(result.error);
                }
            }

            OnGetPlayersComplete?.Invoke(result);
        }
    }

    [System.Serializable]
    private class ReadyStatusRequest { public bool isReady; }

    IEnumerator SetPlayerStatusCoroutine(string roomCode, string userId, bool isReady, string token)
    {
        string url = EffectiveBaseUrl + "/rooms/" + UnityWebRequest.EscapeURL(roomCode) + "/players/" + UnityWebRequest.EscapeURL(userId) + "/status";
        var body = new ReadyStatusRequest { isReady = isReady };
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = new RoomResult();
            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                result.error = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                Debug.LogError("SetPlayerStatus error: " + result.error);
            }
            else
            {
                result.success = true;
            }

            OnSetPlayerStatusComplete?.Invoke(result);
        }
    }

    [System.Serializable]
    private class CharacterRequest { public string character; }

    IEnumerator SetPlayerCharacterCoroutine(string roomCode, string userId, string character, string token)
    {
        string url = EffectiveBaseUrl + "/rooms/" + UnityWebRequest.EscapeURL(roomCode) + "/players/" + UnityWebRequest.EscapeURL(userId) + "/character";
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

            var result = new RoomResult();
            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                result.error = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                Debug.LogError("SetPlayerCharacter error: " + result.error);
            }
            else
            {
                result.success = true;
            }

            OnSetPlayerCharacterComplete?.Invoke(result);
        }
    }

    IEnumerator StartRoomCoroutine(string roomCode, string token)
    {
        string url = EffectiveBaseUrl + "/rooms/" + UnityWebRequest.EscapeURL(roomCode) + "/start";

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            var result = new RoomResult();
            if (req.result != UnityWebRequest.Result.Success)
            {
                result.success = false;
                result.error = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                Debug.LogError("StartRoom error: " + result.error);
            }
            else
            {
                result.success = true;
            }

            OnStartRoomComplete?.Invoke(result);
        }
    }

    private RoomResult HandleResponse(UnityWebRequest req, string operation)
    {
        var result = new RoomResult();

        if (req.result != UnityWebRequest.Result.Success)
        {
            result.success = false;
            if (req.responseCode == 401)
            {
                result.error = "Authentication failed. Please login again.";
            }
            else if (req.responseCode == 403)
            {
                result.error = "You don't have permission for this action.";
            }
            else if (req.responseCode == 404)
            {
                result.error = "Room not found.";
            }
            else if (req.responseCode == 400)
            {
                result.error = "Bad request. Check your input.";
            }
            else if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                result.error = $"Connection failed: {req.error}";
            }
            else
            {
                result.error = $"{operation} failed: {req.responseCode}";
            }
            Debug.LogError($"{operation} error: {result.error}");
        }
        else
        {
            try
            {
                string responseText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;

                if (operation == "Create Room" || operation == "Join Room")
                {
                    var roomRes = JsonUtility.FromJson<RoomResponse>(responseText);
                    result.success = true;
                    result.room = roomRes != null ? roomRes.room : null;

                    if (result.room != null)
                    {
                        Debug.Log($"{operation} successful. Room: {result.room.roomCode}");
                    }
                    else
                    {
                        Debug.Log($"{operation} successful.");
                    }

                    return result;
                }

                var actionRes = JsonUtility.FromJson<RoomActionResponse>(responseText);
                result.success = true;
                result.room = null;

                if (!string.IsNullOrEmpty(actionRes?.roomCode))
                {
                    Debug.Log($"{operation} successful. RoomCode: {actionRes.roomCode}");
                }
                else
                {
                    Debug.Log($"{operation} successful.");
                }
            }
            catch (Exception e)
            {
                result.success = false;
                result.error = $"Failed to parse response: {e.Message}";
                Debug.LogError(result.error);
            }
        }

        return result;
    }
}
