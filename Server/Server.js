const express = require("express");
const mongoose = require("mongoose");
require("dotenv").config();
const cors = require("cors");
const User = require("./Models/user");
const Room = require("./Models/room");
const RoomPlayer = require("./Models/roomPlayer");
const GameSession = require("./Models/gameSession");
const Round = require("./Models/round");
const PlayerScore = require("./Models/playerScore");

const app = express();
const PORT = process.env.PORT || 3000;
const DATABASE_URL = process.env.DATABASE_URL;
const CORS_ORIGIN = process.env.CORS_ORIGIN || "*";
const HEARTBEAT_TIMEOUT_MS = Number(process.env.HEARTBEAT_TIMEOUT_MS || 30000);
const CLEANUP_INTERVAL_MS = Number(process.env.CLEANUP_INTERVAL_MS || 15000);

app.use(
    cors({
        origin: CORS_ORIGIN === "*" ? true : CORS_ORIGIN
    })
);
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

app.get("/health", async (req, res) => {
    try {
        const dbState = mongoose.connection.readyState;
        if (dbState !== 1) {
            return res.status(503).json({
                status: "degraded",
                database: "disconnected"
            });
        }

        const count = await User.countDocuments();
        return res.json({
            status: "ok",
            database: "connected",
            users: count
        });
    } catch (error) {
        return res.status(500).json({
            status: "error",
            message: error.message
        });
    }
});

app.use("/auth", require("./Routes/auth"));
app.use("/characters", require("./Routes/characters"));
app.use("/rooms", require("./Routes/rooms"));
app.use("/game", require("./Routes/game"));

app.get("/", (req, res) => {
    res.send("API Running");
});

function startRoomCleanupJob() {
    setInterval(async () => {
        try {
            const staleBefore = new Date(Date.now() - HEARTBEAT_TIMEOUT_MS);
            const staleRooms = await Room.find({
                status: { $ne: "closed" },
                hostLastHeartbeatAt: { $lt: staleBefore }
            }).select("_id roomCode");

            if (staleRooms.length === 0) {
                return;
            }

            const roomIds = staleRooms.map((room) => room._id);

            await Room.updateMany(
                { _id: { $in: roomIds } },
                {
                    $set: {
                        status: "closed",
                        closedAt: new Date(),
                        closeReason: "heartbeat_timeout"
                    }
                }
            );

            await RoomPlayer.deleteMany({ roomId: { $in: roomIds } });
            console.log(`Closed ${staleRooms.length} stale room(s) by heartbeat timeout`);
        } catch (error) {
            console.error("Room cleanup error:", error.message);
        }
    }, CLEANUP_INTERVAL_MS);
}

async function startServer() {
    try {
        if (!DATABASE_URL) {
            throw new Error("Missing DATABASE_URL in environment variables");
        }

        if (!process.env.JWT_SECRET) {
            throw new Error("Missing JWT_SECRET in environment variables");
        }

        await mongoose.connect(DATABASE_URL);
        console.log("MongoDB connected");
        startRoomCleanupJob();

        app.listen(PORT, () => {
            console.log(`Server running on port ${PORT}`);
        });
    } catch (error) {
        console.error("Failed to start server:", error.message);
        process.exit(1);
    }
}

startServer();