# Day 4 Unity Integration Setup Guide

## Overview
Đã triển khai xong Ngày 4 với 6 scripts để tích hợp Auth + Room + UI:
- **AuthClient** (enhanced): register/login với error handling + callbacks
- **RoomClient** (new): API tạo/vào/rời phòng + heartbeat
- **GameManager** (new): state machine để quản lý flow
- **AuthUIManager** (new): UI login/register 2 panels
- **RoomUIManager** (new): UI tạo/vào phòng
- **SceneBootstrap** (new): tự động chuyển scene theo state

## Cấu trúc Scene cần tạo

### 1. **Auth Scene** (Cảnh đăng nhập)
Tạo scene mới tên `Auth`:

**Hierarchy:**
```
Canvas
├── AuthUIManager (script)
├── LoginPanel (Panel)
│   ├── Title (Text): "Login"
│   ├── UsernameInput (InputField)
│   ├── PasswordInput (InputField)
│   ├── LoginButton (Button)
│   ├── StatusText (Text)
│   └── SwitchToRegisterButton (Button): Text = "Create Account"
│
└── RegisterPanel (Panel)
    ├── Title (Text): "Register"
    ├── UsernameInput (InputField)
    ├── PasswordInput (InputField)
    ├── ConfirmPasswordInput (InputField)
    ├── RegisterButton (Button)
    ├── StatusText (Text)
    └── SwitchToLoginButton (Button): Text = "Back to Login"
```

**AuthUIManager References:**
- Login Panel → loginPanel
- Register Panel → registerPanel
- Login Username Input → loginUsernameInput
- Login Password Input → loginPasswordInput
- Login Button → loginButton
- Login Status Text → loginStatusText
- Switch to Register Button → switchToRegisterButton
- Register Username Input → registerUsernameInput
- Register Password Input → registerPasswordInput
- Register Confirm Password Input → registerConfirmPasswordInput
- Register Button → registerButton
- Register Status Text → registerStatusText
- Switch to Login Button → switchToLoginButton

---

### 2. **Lobby Scene** (Cảnh phòng chơi)
Tạo scene mới tên `Lobby`:

**Hierarchy:**
```
Canvas
├── RoomUIManager (script)
├── TopBar (Panel)
│   ├── CurrentUsernameText (Text): Hiển thị tên player
│   └── LogoutButton (Button)
│
├── CreateRoomPanel (Panel)
│   ├── Title (Text): "Create Room"
│   ├── RoomNameInput (InputField)
│   ├── CreateRoomButton (Button)
│   └── StatusText (Text)
│
├── JoinRoomPanel (Panel)
│   ├── Title (Text): "Join Room"
│   ├── RoomCodeInput (InputField): maxCharacters = 6
│   ├── JoinRoomButton (Button)
│   └── StatusText (Text)
│
└── RoomInfoPanel (Panel)
    ├── RoomCodeDisplayText (Text): Hiển thị mã phòng
    ├── IsHostDisplayText (Text): Hiển thị [HOST] hoặc [PLAYER]
    ├── StartGameButton (Button)
    └── LeaveRoomButton (Button)
```

**RoomUIManager References:**
- Room Lobby Panel → roomLobbyPanel
- Current Username Text → currentUsernameText
- Room Name Input → roomNameInput
- Create Room Button → createRoomButton
- Create Room Status Text → createRoomStatusText
- Join Room Code Input → joinRoomCodeInput
- Join Room Button → joinRoomButton
- Join Room Status Text → joinRoomStatusText
- Room Code Display Text → roomCodeDisplayText
- Is Host Display Text → isHostDisplayText
- Start Game Button → startGameButton
- Leave Room Button → leaveRoomButton
- Logout Button → logoutButton
- Gameplay Scene Name → "SampleScene"

---

### 3. **Bootstrap Scene** (Scene chính cho bootstrap)
Sửa hoặc tạo scene gọi là `Bootstrap` để khởi tạo GameManager:

**Hierarchy:**
```
Canvas (không cần UI gì, chỉ cần canvas tồn tại)

GameManager (GameObject)
├── Script: GameManager

AuthClient (GameObject)
├── Script: AuthClient

RoomClient (GameObject)
├── Script: RoomClient

SceneBootstrap (GameObject)
├── Script: SceneBootstrap
└── Set Auth Scene Name = "Auth"
└── Set Room Lobby Scene Name = "Lobby"
```

**SceneBootstrap References:**
- Auth Scene Name → "Auth"
- Room Lobby Scene Name → "Lobby"

---

## Setup Steps

### Bước 1: Import các Scripts
✓ Đã tạo sẵn 6 scripts trong `Assets/Scripts/Network/`

### Bước 2: Tạo Bootstrap Scene
1. Tạo scene mới: `Create > Scene > Bootstrap`
2. Thêm GameObject `GameManager` gắn script `GameManager`
3. Thêm GameObject `AuthClient` gắn script `AuthClient`
4. Thêm GameObject `RoomClient` gắn script `RoomClient`
5. Thêm GameObject `SceneBootstrap` gắn script `SceneBootstrap`
6. Đặt Base URL trên AuthClient: `http://localhost:3000`
7. Đặt Base URL trên RoomClient: `http://localhost:3000`
8. Save scene

### Bước 3: Tạo Auth Scene
1. Tạo scene mới: `Create > Scene > Auth`
2. Thêm Canvas (UI)
3. Tạo LoginPanel và RegisterPanel như template ở trên
4. Thêm GameObject `AuthUIManager` gắn script `AuthUIManager`
5. Drag các UI elements vào references của AuthUIManager
6. Save scene

### Bước 4: Tạo Lobby Scene
1. Tạo scene mới: `Create > Scene > Lobby`
2. Thêm Canvas (UI)
3. Tạo CreateRoomPanel, JoinRoomPanel, RoomInfoPanel như template ở trên
4. Thêm GameObject `RoomUIManager` gắn script `RoomUIManager`
5. Drag các UI elements vào references của RoomUIManager
6. Set Gameplay Scene Name = "SampleScene"
7. Save scene

### Bước 5: Cấu hình Build Settings
1. Vào `File > Build Settings`
2. Add Scenes theo thứ tự:
   - 0: Bootstrap
   - 1: Auth
   - 2: Lobby
   - 3: SampleScene (hoặc gameplay scene của bạn)
3. Set "Bootstrap" là Active Scene để test

---

## Testing Locally

### Start Flow:
1. Tại chỗ Backend Server: `npm start`
2. Tại Unity Editor:
   - Open Bootstrap scene
   - Play
   - Server sẽ kiểm tra token:
     - Nếu không có → load Auth scene
     - Nếu có token → load Lobby scene
3. Nếu chưa có account:
   - Bấm "Create Account"
   - Nhập username/password
   - Bấm "Register"
   - Quay lại login
   - Login với username/password vừa tạo
4. Sau login, sẽ vào Lobby:
   - Chọn "Create Room" để host
   - Hoặc "Join Room" với mã phòng
5. Khi vào room:
   - Hiển thị Room Code
   - Hiển thị [HOST] hoặc [PLAYER]
   - Bấm "Start Game" để chuyển sang SampleScene

---

## Testing 2 Players Locally

Mở 2 Unity instances:
1. Instance 1 (Host):
   - Register/Login
   - Create Room
   - Copy room code
   - Thấy room code hiển thị

2. Instance 2 (Player):
   - Register/Login khác tài khoản
   - Join Room
   - Paste room code vừa copy
   - Bấn Join

Tiêu chí hoàn thành:
- ✓ 2 players login riêng được
- ✓ Host create room và có room code
- ✓ Player join room bằng code thành công
- ✓ Cả 2 đều vào Lobby scene cùng lúc
- ✓ Bấm Start Game chuyển sang SampleScene

---

## Lưu ý quan trọng

1. **Backend phải chạy**: `npm start` tại `d:\Project_test_Unity\Server`
2. **Base URL**: Nếu deploy backend thực tế, cần sửa base URL từ `http://localhost:3000` sang domain thực
3. **Scene Names**: Tên scene phải khớp trong SceneBootstrap và RoomUIManager
4. **Relay Setup**: Ngày 5 mới làm Relay + NetworkManager flow

---

## Troubleshooting

**Login báo "Connection failed"**
- Backend không chạy
- Base URL sai (kiểm tra AuthClient.baseUrl)

**Register báo "Username already exists"**
- Bình thường, dùng username khác

**Join Room báo "Room not found"**
- Room code sai hoặc phòng đã đóng
- Backend chưa sẵn sàng

**Scene không chuyển**
- Kiểm tra tên scene trong SceneBootstrap
- Kiểm tra scene đã add vào Build Settings

---

Nếu cần, mình có thể giúp:
1. Hoàn thiện UI styling
2. Thêm heartbeat loop cho host
3. Làm Ngày 5 (Relay + NetworkManager integration)
