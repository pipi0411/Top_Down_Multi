const express = require("express");
const Room = require("../Models/room");
const RoomPlayer = require("../Models/roomPlayer");
const User = require("../Models/user");
const authMiddleware = require("../Middleware/authMiddleware");

const router = express.Router();

const AVAILABLE_CHARACTERS = ["Knight", "Archer", "Mage", "Rogue", "Paladin"];

router.get("/", async (req, res) => {
    try {
        const roomCode = (req.query?.roomCode || "").trim().toUpperCase();

        if (!roomCode) {
            return res.json({
                characters: AVAILABLE_CHARACTERS
            });
        }

        const room = await Room.findOne({ roomCode, status: { $ne: "closed" } });
        if (!room) {
            return res.status(404).json({ message: "Room not found or already closed" });
        }

        const roomPlayers = await RoomPlayer.find({ roomId: room._id }).select("character");
        const takenCharacters = roomPlayers
            .map((player) => player.character)
            .filter(Boolean);

        const availableCharacters = AVAILABLE_CHARACTERS.filter(
            (character) => !takenCharacters.includes(character)
        );

        return res.json({
            characters: availableCharacters,
            takenCharacters
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.post("/users/:userId/character/:characterName", authMiddleware, async (req, res) => {
    try {
        const userId = req.params.userId;
        const characterName = (req.params.characterName || "").trim();

        if (String(req.user.id) !== String(userId)) {
            return res.status(403).json({ message: "Can only set own character" });
        }

        if (!AVAILABLE_CHARACTERS.includes(characterName)) {
            return res.status(400).json({
                message: "Invalid character",
                available: AVAILABLE_CHARACTERS
            });
        }

        const user = await User.findByIdAndUpdate(
            userId,
            { selectedCharacter: characterName },
            { returnDocument: 'after' }
        );

        if (!user) {
            return res.status(404).json({ message: "User not found" });
        }

        return res.json({
            success: true,
            character: user.selectedCharacter
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.get("/users/:userId/character", authMiddleware, async (req, res) => {
    try {
        const userId = req.params.userId;

        if (String(req.user.id) !== String(userId)) {
            return res.status(403).json({ message: "Can only get own character" });
        }

        const user = await User.findById(userId).select("selectedCharacter");

        if (!user) {
            return res.status(404).json({ message: "User not found" });
        }

        return res.json({
            character: user.selectedCharacter
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.get("/users/:userId/profile", authMiddleware, async (req, res) => {
    try {
        const userId = req.params.userId;

        if (String(req.user.id) !== String(userId)) {
            return res.status(403).json({ message: "Can only get own profile" });
        }

        const user = await User.findById(userId).select("username selectedCharacter createdAt");

        if (!user) {
            return res.status(404).json({ message: "User not found" });
        }

        return res.json({
            userId: user._id,
            username: user.username,
            selectedCharacter: user.selectedCharacter,
            createdAt: user.createdAt
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

module.exports = router;
