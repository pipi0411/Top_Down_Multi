const express = require("express");
const authMiddleware = require("../Middleware/authMiddleware");
const PlayerSave = require("../Models/playerSave");

const router = express.Router();

router.get("/me", authMiddleware, async (req, res) => {
    try {
        const save = await PlayerSave.findOne({ userId: req.user.id }).lean();
        if (!save) {
            return res.json({
                success: true,
                hasSave: false,
                saveData: null
            });
        }

        return res.json({
            success: true,
            hasSave: true,
            saveData: save.saveData,
            updatedAt: save.updatedAt
        });
    } catch (error) {
        return res.status(500).json({
            success: false,
            message: error.message
        });
    }
});

router.post("/me", authMiddleware, async (req, res) => {
    try {
        const saveData = req.body?.saveData || req.body;
        if (!saveData || typeof saveData !== "object" || Array.isArray(saveData)) {
            return res.status(400).json({
                success: false,
                message: "Invalid save data"
            });
        }

        const save = await PlayerSave.findOneAndUpdate(
            { userId: req.user.id },
            { userId: req.user.id, saveData },
            { upsert: true, new: true, setDefaultsOnInsert: true }
        ).lean();

        return res.json({
            success: true,
            hasSave: true,
            saveData: save.saveData,
            updatedAt: save.updatedAt
        });
    } catch (error) {
        return res.status(500).json({
            success: false,
            message: error.message
        });
    }
});

router.delete("/me", authMiddleware, async (req, res) => {
    try {
        await PlayerSave.deleteOne({ userId: req.user.id });
        return res.json({
            success: true,
            hasSave: false,
            message: "Save deleted"
        });
    } catch (error) {
        return res.status(500).json({
            success: false,
            message: error.message
        });
    }
});

module.exports = router;
