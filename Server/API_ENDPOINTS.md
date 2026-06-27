# Backend API Endpoints - Tổng Hợp Đầy Đủ

**Base URL:** `http://localhost:3000`  
**Auth Type:** Bearer Token (JWT)  
**Token Expiry:** 1 hour

---

## 📋 1. AUTHENTICATION (`/auth`)

### 1.1 Register User
- **Method:** `POST`
- **Path:** `/auth/register`
- **Auth Required:** ❌ No
- **Request Body:**
  ```json
  {
    "username": "string (required)",
    "password": "string (required)"
  }
  ```
- **Response (201):**
  ```json
  {
    "message": "Registered"
  }
  ```
- **Error (409):** User already exists
- **Error (400):** Missing username or password

### 1.2 Login User
- **Method:** `POST`
- **Path:** `/auth/login`
- **Auth Required:** ❌ No
- **Request Body:**
  ```json
  {
    "username": "string (required)",
    "password": "string (required)"
  }
  ```
- **Response (200):**
  ```json
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }
  ```
- **Usage:** Add `Authorization: Bearer {token}` header to all protected requests

### 1.3 Protected Test Endpoint
- **Method:** `GET`
- **Path:** `/auth/protected`
- **Auth Required:** ✅ Yes (Bearer token)
- **Response (200):**
  ```json
  {
    "message": "Access granted",
    "user": {
      "id": "USER_ID",
      "username": "username"
    }
  }
  ```

---

## 🎭 2. CHARACTER SYSTEM (`/characters`)

### 2.1 Get Available Characters
- **Method:** `GET`
- **Path:** `/characters`
- **Auth Required:** ❌ No
- **Query Params:**
  - `roomCode` (optional): nếu truyền vào, API chỉ trả về các nhân vật còn trống trong phòng đó
- **Response (200):**
  ```json
  {
    "characters": [
      "Knight",
      "Archer",
      "Mage",
      "Rogue",
      "Paladin"
    ]
  }
  ```

- **Response (200) khi có `roomCode`:**
  ```json
  {
    "characters": ["Knight", "Archer"],
    "takenCharacters": ["Mage", "Rogue", "Paladin"]
  }
  ```

### 2.2 Set User Character
- **Method:** `POST`
- **Path:** `/characters/users/{userId}/character/{characterName}`
- **Auth Required:** ✅ Yes
- **Path Params:**
  - `userId`: User's MongoDB ID (from protected endpoint)
  - `characterName`: One of ["Knight", "Archer", "Mage", "Rogue", "Paladin"]
- **Request Body:** `{}` (empty)
- **Response (200):**
  ```json
  {
    "success": true,
    "character": "Mage"
  }
  ```
- **Error (403):** Can only set own character
- **Error (400):** Invalid character

### 2.3 Get User Character
- **Method:** `GET`
- **Path:** `/characters/users/{userId}/character`
- **Auth Required:** ✅ Yes
- **Response (200):**
  ```json
  {
    "character": "Mage"
  }
  ```

### 2.4 Get User Profile
- **Method:** `GET`
- **Path:** `/characters/users/{userId}/profile`
- **Auth Required:** ✅ Yes
- **Response (200):**
  ```json
  {
    "userId": "USER_ID",
    "username": "game123",
    "selectedCharacter": "Mage",
    "createdAt": "2024-05-06T10:30:00Z"
  }
  ```

---

## 🏠 3. ROOM MANAGEMENT (`/rooms`)

### 3.1 Create Room
- **Method:** `POST`
- **Path:** `/rooms/create`
- **Auth Required:** ✅ Yes
- **Request Body:**
  ```json
  {
    "name": "string (optional, default: 'New Room')"
  }
  ```
- **Response (201):**
  ```json
  {
    "message": "Room created",
    "room": {
      "id": "ROOM_ID",
      "roomCode": "ABC123",
      "name": "My Game Room",
      "status": "waiting",
      "ownerId": "USER_ID"
    }
  }
  ```
- **Notes:** Host (creator) automatically added as owner
- **Notes:** Host (creator) automatically added as owner with `character = null`; room character selection is separate from the global `/characters/users/{userId}/character/{characterName}` selection

### 3.2 Join Room
- **Method:** `POST`
- **Path:** `/rooms/join`
- **Auth Required:** ✅ Yes
- **Request Body:**
  ```json
  {
    "roomCode": "ABC123"
  }
  ```
- **Response (200):**
  ```json
  {
    "message": "Joined room",
    "room": {
      "id": "ROOM_ID",
      "roomCode": "ABC123",
      "name": "My Game Room",
      "status": "waiting"
    }
  }
  ```
- **Error (404):** Room not found or closed
- **Notes:** If already member, returns "Already in room"

### 3.3 Get Room Details & Players
- **Method:** `GET`
- **Path:** `/rooms/{roomCode}`
- **Auth Required:** ✅ Yes
- **Response (200):**
  ```json
  {
    "room": {
      "id": "ROOM_ID",
      "roomCode": "ABC123",
      "name": "My Game Room",
      "status": "waiting|playing|closed",
      "ownerId": "USER_ID",
      "hostLastHeartbeatAt": "2024-05-06T10:30:00Z",
      "closeReason": null
    },
    "players": [
      {
        "userId": "USER_ID_1",
        "username": "player1",
        "character": "Knight",
        "isReady": false,
        "role": "owner"
      },
      {
        "userId": "USER_ID_2",
        "username": "player2",
        "character": "Mage",
        "isReady": true,
        "role": "player"
      }
    ]
  }
  ```

### 3.4 Get Room Players List
- **Method:** `GET`
- **Path:** `/rooms/{roomCode}/players`
- **Auth Required:** ✅ Yes
- **Response (200):**
  ```json
  {
    "roomCode": "ABC123",
    "players": [
      {
        "userId": "USER_ID_1",
        "username": "player1",
        "character": "Knight",
        "isReady": false,
        "role": "owner"
      },
      {
        "userId": "USER_ID_2",
        "username": "player2",
        "character": "Mage",
        "isReady": true,
        "role": "player"
      }
    ]
  }
  ```

### 3.5 Set Player Ready Status
- **Method:** `POST`
- **Path:** `/rooms/{roomCode}/players/{userId}/status`
- **Auth Required:** ✅ Yes
- **Request Body:**
  ```json
  {
    "isReady": true
  }
  ```
- **Response (200):**
  ```json
  {
    "success": true,
    "message": "Player ready status updated",
    "isReady": true
  }
  ```

### 3.6 Set Player Character in Room
- **Method:** `POST`
- **Path:** `/rooms/{roomCode}/players/{userId}/character`
- **Auth Required:** ✅ Yes
- **Request Body:**
  ```json
  {
    "character": "Mage"
  }
  ```
- **Response (200):**
  ```json
  {
    "success": true,
    "message": "Player character updated",
    "character": "Mage"
  }
  ```

### 3.7 Start Game (Validate & Begin)
- **Method:** `POST`
- **Path:** `/rooms/{roomCode}/start`
- **Auth Required:** ✅ Yes (Host only)
- **Request Body:** `{}`
- **Response (200):** (All players ready)
  ```json
  {
    "success": true,
    "message": "Game started",
    "room": {
      "status": "playing"
    }
  }
  ```
- **Response (400):** (Not all ready)
  ```json
  {
    "message": "Not all players are ready",
    "readyCount": 1,
    "totalPlayers": 2
  }
  ```
- **Error (403):** Only host can start

### 3.8 Leave Room
- **Method:** `POST`
- **Path:** `/rooms/leave`
- **Auth Required:** ✅ Yes
- **Request Body:**
  ```json
  {
    "roomCode": "ABC123"
  }
  ```
- **Response (200):**
  ```json
  {
    "message": "Left room",
    "roomCode": "ABC123"
  }
  ```
- **Error (400):** Host cannot leave (must close room instead)

### 3.9 Close Room
- **Method:** `POST`
- **Path:** `/rooms/close`
- **Auth Required:** ✅ Yes (Host only)
- **Request Body:**
  ```json
  {
    "roomCode": "ABC123"
  }
  ```
- **Response (200):**
  ```json
  {
    "message": "Room closed",
    "roomCode": "ABC123"
  }
  ```

### 3.10 Heartbeat (Keep Room Alive)
- **Method:** `POST`
- **Path:** `/rooms/heartbeat`
- **Auth Required:** ✅ Yes (Host only)
- **Request Body:**
  ```json
  {
    "roomCode": "ABC123"
  }
  ```
- **Response (200):**
  ```json
  {
    "message": "Heartbeat received",
    "roomCode": "ABC123"
  }
  ```
- **Notes:** Host should send every 10-15 seconds to prevent auto-close (timeout = 30s)

---

## 🎮 4. GAME SYSTEM (`/game`)

### 4.1 Start Game Session
- **Method:** `POST`
- **Path:** `/game/{roomCode}/game/start`
- **Auth Required:** ✅ Yes (Host only)
- **Request Body:** `{}`
- **Response (200):**
  ```json
  {
    "success": true,
    "gameSessionId": "SESSION_ID",
    "round": {
      "roundNumber": 1,
      "question": "What is 2 + 2?",
      "timeLimit": 30
    }
  }
  ```
- **Prerequisites:** Room status must be "playing"

### 4.2 Get Game Session Status
- **Method:** `GET`
- **Path:** `/game/{roomCode}/game/session`
- **Auth Required:** ✅ Yes
- **Response (200):**
  ```json
  {
    "sessionId": "SESSION_ID",
    "status": "in_progress|completed",
    "currentRound": 1,
    "totalRounds": 3,
    "round": {
      "roundNumber": 1,
      "question": "What is 2 + 2?",
      "timeLimit": 30,
      "status": "active"
    }
  }
  ```

### 4.3 Get Current Question
- **Method:** `GET`
- **Path:** `/game/{roomCode}/game/question`
- **Auth Required:** ✅ Yes
- **Response (200):**
  ```json
  {
    "roundNumber": 1,
    "question": "What is 2 + 2?",
    "timeRemaining": 25,
    "timeLimit": 30
  }
  ```
- **Notes:** `timeRemaining` = seconds left for this question

### 4.4 Submit Answer
- **Method:** `POST`
- **Path:** `/game/{roomCode}/game/answer`
- **Auth Required:** ✅ Yes
- **Request Body:**
  ```json
  {
    "answer": "4"
  }
  ```
- **Response (200):** (Correct)
  ```json
  {
    "success": true,
    "isCorrect": true,
    "message": "Correct!"
  }
  ```
- **Response (200):** (Incorrect)
  ```json
  {
    "success": true,
    "isCorrect": false,
    "message": "Wrong! The answer was: 4"
  }
  ```
- **Scoring:** +10 points for correct answer
- **Notes:** 
  - Case-insensitive matching
  - Can only submit once per round

### 4.5 Complete Round & Move to Next
- **Method:** `POST`
- **Path:** `/game/{roomCode}/game/round/complete`
- **Auth Required:** ✅ Yes (Host only)
- **Request Body:** `{}`
- **Response (200):** (More rounds remaining)
  ```json
  {
    "success": true,
    "message": "Round completed, next round starting",
    "nextRound": {
      "roundNumber": 2,
      "question": "What is the capital of France?",
      "timeLimit": 30
    }
  }
  ```
- **Response (200):** (Game ended)
  ```json
  {
    "success": true,
    "message": "Game completed",
    "gameEnded": true
  }
  ```

### 4.6 Get Final Results & Leaderboard
- **Method:** `GET`
- **Path:** `/game/{roomCode}/game/results`
- **Auth Required:** ✅ Yes
- **Response (200):**
  ```json
  {
    "gameSessionId": "SESSION_ID",
    "status": "completed",
    "completedRounds": 3,
    "totalRounds": 3,
    "leaderboard": [
      {
        "rank": 1,
        "userId": "USER_ID_1",
        "username": "player1",
        "character": "Knight",
        "correctCount": 3,
        "totalScore": 30,
        "roundScores": [
          {
            "roundNumber": 1,
            "correct": true,
            "points": 10
          },
          {
            "roundNumber": 2,
            "correct": true,
            "points": 10
          },
          {
            "roundNumber": 3,
            "correct": true,
            "points": 10
          }
        ]
      },
      {
        "rank": 2,
        "userId": "USER_ID_2",
        "username": "player2",
        "character": "Mage",
        "correctCount": 2,
        "totalScore": 20,
        "roundScores": [
          {
            "roundNumber": 1,
            "correct": false,
            "points": 0
          },
          {
            "roundNumber": 2,
            "correct": true,
            "points": 10
          },
          {
            "roundNumber": 3,
            "correct": true,
            "points": 10
          }
        ]
      }
    ]
  }
  ```

### 4.7 Get Specific Round Results
- **Method:** `GET`
- **Path:** `/game/{roomCode}/game/round/results?round={roundNumber}`
- **Auth Required:** ✅ Yes
- **Query Params:**
  - `round`: Round number (default: 1)
- **Response (200):**
  ```json
  {
    "roundNumber": 1,
    "question": "What is 2 + 2?",
    "correctAnswer": "4",
    "answers": [
      {
        "username": "player1",
        "answer": "4",
        "isCorrect": true,
        "submittedAt": "2024-05-06T10:35:00Z"
      },
      {
        "username": "player2",
        "answer": "5",
        "isCorrect": false,
        "submittedAt": "2024-05-06T10:35:02Z"
      }
    ]
  }
  ```

---

## 🔑 Request Header Template

Thêm header này vào tất cả protected API calls:

```
Authorization: Bearer {jwt_token_from_login}
Content-Type: application/json
```

---

## 📱 Common Unity C# Implementation Pattern

```csharp
// Setup
string token = loginResponse.token;
string baseUrl = "http://localhost:3000";
var headers = new Dictionary<string, string> 
{ 
    { "Authorization", $"Bearer {token}" },
    { "Content-Type", "application/json" }
};

// GET request
using (UnityWebRequest www = UnityWebRequest.Get($"{baseUrl}/characters"))
{
    foreach (var header in headers)
        www.SetRequestHeader(header.Key, header.Value);
    yield return www.SendWebRequest();
}

// POST request
using (UnityWebRequest www = new UnityWebRequest($"{baseUrl}/rooms/create", "POST"))
{
    www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new { name = "Game Room" })));
    www.downloadHandler = new DownloadHandlerBuffer();
    foreach (var header in headers)
        www.SetRequestHeader(header.Key, header.Value);
    yield return www.SendWebRequest();
}
```

---

## ✅ Game Flow Sequence

1. **Register** → `/auth/register`
2. **Login** → `/auth/login` (get token)
3. **Get Characters** → `/characters`
4. **Select Character** → `/characters/users/{userId}/character/{characterName}`
5. **Create/Join Room** → `/rooms/create` or `/rooms/join`
6. **Set Ready** → `/rooms/{roomCode}/players/{userId}/status`
7. **Start Game** → `/rooms/{roomCode}/start` (Host)
8. **Begin Session** → `/game/{roomCode}/game/start` (Host)
9. **Get Question** → `/game/{roomCode}/game/question` (loop)
10. **Submit Answer** → `/game/{roomCode}/game/answer`
11. **Complete Round** → `/game/{roomCode}/game/round/complete` (Host)
12. **View Results** → `/game/{roomCode}/game/results` (after game ends)

---

## 🔧 Status Codes

- `200` - Success (GET, POST)
- `201` - Created (POST)
- `400` - Bad request (missing params, validation error)
- `403` - Forbidden (not authorized for this action)
- `404` - Not found (resource doesn't exist)
- `409` - Conflict (user already exists)
- `500` - Server error

