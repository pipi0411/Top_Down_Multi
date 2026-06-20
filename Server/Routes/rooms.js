const express = require("express");
const Room = require("../Models/room");
const RoomPlayer = require("../Models/roomPlayer");
const authMiddleware = require("../Middleware/authMiddleware");

const router = express.Router();
const ROOM_CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

function generateRoomCode(length = 6) {
    let result = "";
    for (let i = 0; i < length; i += 1) {
        const index = Math.floor(Math.random() * ROOM_CODE_CHARS.length);
        result += ROOM_CODE_CHARS[index];
    }
    return result;
}

async function createUniqueRoomCode() {
    for (let i = 0; i < 10; i += 1) {
        const roomCode = generateRoomCode(6);
        const existing = await Room.exists({ roomCode });
        if (!existing) {
            return roomCode;
        }
    }
    throw new Error("Could not generate unique room code");
}

router.post("/create", authMiddleware, async (req, res) => {
    try {
        const roomName = (req.body?.name || "New Room").trim();
        const roomCode = await createUniqueRoomCode();

        const room = await Room.create({
            roomCode,
            name: roomName,
            ownerId: req.user.id,
            hostLastHeartbeatAt: new Date(),
            status: "waiting"
        });

        await RoomPlayer.create({
            roomId: room._id,
            userId: req.user.id,
            character: null,
            role: "owner"
        });

        return res.status(201).json({
            message: "Room created",
            room: {
                id: room._id,
                roomCode: room.roomCode,
                name: room.name,
                status: room.status,
                ownerId: room.ownerId
            }
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.post("/join", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.body?.roomCode || "").trim().toUpperCase();
        if (!roomCode) {
            return res.status(400).json({ message: "roomCode is required" });
        }

        const room = await Room.findOne({ roomCode, status: { $ne: "closed" } });
        if (!room) {
            return res.status(404).json({ message: "Room not found or already closed" });
        }

        const existingMember = await RoomPlayer.findOne({
            roomId: room._id,
            userId: req.user.id
        });
        if (existingMember) {
            return res.json({
                message: "Already in room",
                room: {
                    id: room._id,
                    roomCode: room.roomCode,
                    name: room.name,
                    status: room.status
                }
            });
        }

        await RoomPlayer.create({
            roomId: room._id,
            userId: req.user.id,
            role: "player"
        });

        return res.json({
            message: "Joined room",
            room: {
                id: room._id,
                roomCode: room.roomCode,
                name: room.name,
                status: room.status
            }
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.post("/leave", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.body?.roomCode || "").trim().toUpperCase();
        if (!roomCode) {
            return res.status(400).json({ message: "roomCode is required" });
        }

        const room = await Room.findOne({ roomCode, status: { $ne: "closed" } });
        if (!room) {
            return res.status(404).json({ message: "Room not found or already closed" });
        }

        if (String(room.ownerId) === String(req.user.id)) {
            return res.status(400).json({ message: "Host cannot leave. Use close room instead" });
        }

        const result = await RoomPlayer.deleteOne({
            roomId: room._id,
            userId: req.user.id
        });

        if (result.deletedCount === 0) {
            return res.status(400).json({ message: "You are not in this room" });
        }

        return res.json({ message: "Left room", roomCode: room.roomCode });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.post("/close", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.body?.roomCode || "").trim().toUpperCase();
        if (!roomCode) {
            return res.status(400).json({ message: "roomCode is required" });
        }

        const room = await Room.findOne({ roomCode, status: { $ne: "closed" } });
        if (!room) {
            return res.status(404).json({ message: "Room not found or already closed" });
        }

        if (String(room.ownerId) !== String(req.user.id)) {
            return res.status(403).json({ message: "Only host can close room" });
        }

        await RoomPlayer.deleteMany({ roomId: room._id });
        await Room.deleteOne({ _id: room._id });

        return res.json({ message: "Room closed", roomCode: room.roomCode });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.post("/heartbeat", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.body?.roomCode || "").trim().toUpperCase();
        if (!roomCode) {
            return res.status(400).json({ message: "roomCode is required" });
        }

        const room = await Room.findOne({ roomCode, status: { $ne: "closed" } });
        if (!room) {
            return res.status(404).json({ message: "Room not found or already closed" });
        }

        if (String(room.ownerId) !== String(req.user.id)) {
            return res.status(403).json({ message: "Only host can send heartbeat" });
        }

        room.hostLastHeartbeatAt = new Date();
        await room.save();

        return res.json({ message: "Heartbeat received", roomCode: room.roomCode });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.get("/:roomCode", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();
        const room = await Room.findOne({ roomCode });

        if (!room) {
            return res.status(404).json({ message: "Room not found" });
        }

        const players = await RoomPlayer.find({ roomId: room._id }).populate("userId", "username");

        return res.json({
            room: {
                id: room._id,
                roomCode: room.roomCode,
                name: room.name,
                status: room.status,
                ownerId: room.ownerId,
                hostLastHeartbeatAt: room.hostLastHeartbeatAt,
                closeReason: room.closeReason || null
            },
            players: players.map((p) => ({
                userId: p.userId?._id || p.userId,
                username: p.userId?.username,
                character: p.character,
                isReady: p.isReady,
                role: p.role
            }))
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.get("/:roomCode/players", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();
        const room = await Room.findOne({ roomCode });

        if (!room) {
            return res.status(404).json({ message: "Room not found" });
        }

        const players = await RoomPlayer.find({ roomId: room._id }).populate("userId", "username");

        return res.json({
            roomCode: room.roomCode,
            players: players.map((p) => ({
                userId: p.userId?._id || p.userId,
                username: p.userId?.username,
                character: p.character,
                isReady: p.isReady,
                role: p.role
            }))
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.post("/:roomCode/players/:userId/status", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();
        const userId = req.params.userId;
        const isReady = req.body?.isReady;

        if (typeof isReady !== "boolean") {
            return res.status(400).json({ message: "isReady must be boolean" });
        }

        const room = await Room.findOne({ roomCode, status: { $ne: "closed" } });
        if (!room) {
            return res.status(404).json({ message: "Room not found or already closed" });
        }

        const roomPlayer = await RoomPlayer.findOneAndUpdate(
            { roomId: room._id, userId },
            { isReady },
            { returnDocument: 'after' }
        ).populate("userId", "username");

        if (!roomPlayer) {
            return res.status(404).json({ message: "Player not in room" });
        }

        return res.json({
            success: true,
            isReady: roomPlayer.isReady,
            userId: roomPlayer.userId?._id,
            username: roomPlayer.userId?.username
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.post("/:roomCode/players/:userId/character", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();
        const userId = req.params.userId;
        const character = (req.body?.character || "").trim();

        if (!character) {
            return res.status(400).json({ message: "character is required" });
        }

        const room = await Room.findOne({ roomCode, status: { $ne: "closed" } });
        if (!room) {
            return res.status(404).json({ message: "Room not found or already closed" });
        }

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

        const roomPlayer = await RoomPlayer.findOneAndUpdate(
            { roomId: room._id, userId },
            { character },
            { returnDocument: 'after' }
        ).populate("userId", "username");

        if (!roomPlayer) {
            return res.status(404).json({ message: "Player not in room" });
        }

        return res.json({
            success: true,
            character: roomPlayer.character,
            userId: roomPlayer.userId?._id,
            username: roomPlayer.userId?.username
        });
    } catch (error) {
        if (error?.code === 11000) {
            return res.status(409).json({ message: "Character already taken" });
        }
        return res.status(500).json({ message: error.message });
    }
});

router.post("/:roomCode/start", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();

        const room = await Room.findOne({ roomCode, status: { $ne: "closed" } });
        if (!room) {
            return res.status(404).json({ message: "Room not found or already closed" });
        }

        if (String(room.ownerId) !== String(req.user.id)) {
            return res.status(403).json({ message: "Only host can start game" });
        }

        if (room.status === "playing") {
            return res.status(400).json({ message: "Game already started" });
        }

        const players = await RoomPlayer.find({ roomId: room._id });
        const allReady = players.every((p) => p.isReady);

        if (!allReady) {
            return res.status(400).json({
                message: "Not all players are ready",
                readyCount: players.filter((p) => p.isReady).length,
                totalCount: players.length
            });
        }

        room.status = "playing";
        await room.save();

        return res.json({
            success: true,
            message: "Game started",
            roomCode: room.roomCode,
            status: room.status
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

module.exports = router;
