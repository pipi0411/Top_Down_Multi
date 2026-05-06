const mongoose = require("mongoose");

const gameSessionSchema = new mongoose.Schema(
    {
        roomId: { type: mongoose.Schema.Types.ObjectId, ref: "Room", required: true },
        status: { type: String, enum: ["waiting", "in_progress", "completed"], default: "waiting" },
        currentRound: { type: Number, default: 0 },
        totalRounds: { type: Number, default: 3 },
        startedAt: { type: Date },
        endedAt: { type: Date }
    },
    { timestamps: true, collection: "game_sessions" }
);

module.exports = mongoose.model("GameSession", gameSessionSchema);
