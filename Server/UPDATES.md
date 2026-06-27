# 📝 Tổng Hợp Cập Nhật Hệ Thống

**Cập nhật lần cuối:** 2026-06-20

---

## ✅ Đã Hoàn Thành

### 1. ✨ **Chặn Chọn Cùng Nhân Vật Trong Phòng** 
**Ngày:** 2026-06-19

#### Mục đích
Tránh tình huống 2 người chơi trong cùng 1 phòng chọn cùng nhân vật (VD: cả 2 chọn "Knight").

#### Những gì đã sửa

##### a) [Models/roomPlayer.js](Models/roomPlayer.js) - Schema Layer
```javascript
// Thêm unique index trên roomId + character
roomPlayerSchema.index(
    { roomId: 1, character: 1 },
    {
        unique: true,
        partialFilterExpression: { character: { $type: "string" } }
    }
);
```
**Tác dụng:**
- Đảm bảo tại database level: mỗi nhân vật chỉ xuất hiện 1 lần trong 1 phòng
- Bỏ qua những record có `character = null` (chưa chọn)
- Chặn race condition khi 2 request cạnh tranh

##### b) [Routes/rooms.js](Routes/rooms.js#L295-L340) - Logic Check
```javascript
// Kiểm tra trước khi cập nhật
const conflictingPlayer = await RoomPlayer.findOne({
    roomId: room._id,
    character,
    userId: { $ne: userId }
}).populate("userId", "username");

if (conflictingPlayer) {
    return res.status(409).json({
        message: "Character already taken",
        character,
        takenBy: conflictingPlayer.userId?._id,
        username: conflictingPlayer.userId?.username
    });
}
```
**Tác dụng:**
- Kiểm tra ngay trước khi cập nhật
- Trả về **HTTP 409** (Conflict) nếu nhân vật đã được ai khác chọn
- Thông báo chi tiết: ai đang sử dụng nhân vật đó

##### c) [Routes/rooms.js](Routes/rooms.js#L63-L65) - Race Condition Handler
```javascript
// Bắt lỗi nếu 2 request vượt qua check cùng lúc
if (error?.code === 11000) {
    return res.status(409).json({ message: "Character already taken" });
}
```
**Tác dụng:**
- Nếu MongoDB index phát hiện trùng → trả **409** thay vì **500 Internal Error**

---

#### API Endpoint Bị Ảnh Hưởng
```
POST /rooms/{roomCode}/players/{userId}/character
```

**Request:**
```json
{
    "character": "Mage"
}
```

**Response (409 - Mới Thêm):**
```json
{
    "message": "Character already taken",
    "character": "Mage",
    "takenBy": "USER_ID_PERSON_USING_IT",
    "username": "otherplayer"
}
```

---

## 📊 Database Changes

- **Mô hình:** RoomPlayer
- **Index mới:** `{ roomId: 1, character: 1 }` (unique, partial)
- **Migration:** Không cần (partial index bỏ qua `null`)

---

## 🧪 Cách Test

```bash
# 1. Tạo phòng
POST /rooms/create
Header: Authorization: Bearer {token_player1}

# 2. Player 1 chọn Knight
POST /rooms/{roomCode}/players/{userId1}/character
Body: { "character": "Knight" }

# 3. Player 2 cố chọn Knight (sẽ thất bại)
POST /rooms/{roomCode}/players/{userId2}/character
Body: { "character": "Knight" }

# Response: 409 Character already taken
```

---

## 📋 Tình Trạng Hệ Thống

| Module | Trạng Thái | Ghi Chú |
|--------|-----------|--------|
| ✅ Authentication | Hoàn thành | Register, Login, JWT |
| ✅ Character System | Hoàn thành | Chọn nhân vật toàn bộ |
| ✅ Room Management | Cập nhật | **+** Chặn trùng nhân vật |
| ✅ Game System | Hoàn thành | Câu hỏi, điểm, bảng xếp hạng |
| ✅ Player Score | Hoàn thành | Lưu trữ điểm |

---

## 📦 Các Model Có Liên Quan

- `User` - Nhân vật toàn bộ của người dùng
- **`RoomPlayer`** *(được sửa)* - Nhân vật trong phòng
- `Room` - Thông tin phòng
- `GameSession` - Phiên chơi
- `Round` - Vòng chơi

---

## 🚀 Tiếp Theo (Nếu Cần)

- [ ] Thêm migration script dọn dữ liệu trùng nếu database cũ có
- [ ] Test load với nhiều người đồng thời chọn cùng nhân vật
- [ ] Thêm logging để track những lần chọn nhân vật thất bại

---

### 2. 🔄 **Nhân Vật Tự Quay Lại Có Thể Chọn Khi Player Rời Phòng**
**Ngày:** 2026-06-20

#### Mục đích
Khi player thoát game hoặc không còn trong phòng, nhân vật đang giữ phải được trả về danh sách có thể chọn lại.

#### Những gì đã sửa

##### a) [Routes/characters.js](Routes/characters.js) - Danh Sách Nhân Vật Theo Phòng
- `GET /characters` hỗ trợ query `roomCode`
- Nếu có `roomCode`, API sẽ lấy toàn bộ `RoomPlayer.character` đang dùng trong phòng đó
- Trả về danh sách nhân vật còn trống trong phòng thay vì danh sách tĩnh

**Ví dụ response khi có `roomCode`:**
```json
{
    "characters": ["Knight", "Archer"],
    "takenCharacters": ["Mage", "Rogue", "Paladin"]
}
```

##### b) [API_ENDPOINTS.md](API_ENDPOINTS.md) - Cập Nhật Tài Liệu API
- Ghi rõ `roomCode` là query param tùy chọn cho `/characters`
- Thêm ví dụ response khi danh sách được lọc theo phòng

#### Tác dụng
- Player rời phòng thì `RoomPlayer` của họ bị xóa
- Khi gọi lại danh sách nhân vật theo phòng, nhân vật đó sẽ xuất hiện lại để người khác chọn
- UI có thể đồng bộ đúng trạng thái chọn nhân vật theo phòng hiện tại

### 3. 🧩 **Tách Single Player Character Khỏi Multiplayer Room**
**Ngày:** 2026-06-20

#### Mục đích
Không cho lựa chọn nhân vật ở chế độ single player ảnh hưởng đến nhân vật trong room multiplayer.

#### Những gì đã sửa

##### a) [Routes/rooms.js](Routes/rooms.js) - Room Tạo Ra Với Character Rỗng
- Khi tạo room, host vẫn được thêm vào `RoomPlayer`
- `character` của host trong room được set về `null`
- Không còn copy `User.selectedCharacter` sang phòng nữa

#### Tác dụng
- Single player vẫn dùng `selectedCharacter` ở mức tài khoản như cũ
- Multiplayer room dùng lựa chọn riêng trong `RoomPlayer`
- Host vào room sẽ chọn nhân vật độc lập với lựa chọn global

