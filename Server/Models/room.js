const mongoose = require("mongoose");

const roomSchema = new mongoose.Schema(
    {
        roomCode: { type: String, required: true, unique: true, uppercase: true, trim: true },
        name: { type: String, required: true, trim: true },
        ownerId: { type: mongoose.Schema.Types.ObjectId, ref: "User", required: true },
        status: { type: String, enum: ["waiting", "playing", "closed"], default: "waiting" },
        maxPlayers: { type: Number, default: 2, min: 1, max: 8 },
        relayJoinCode: { type: String, default: null, trim: true },
        hostLastHeartbeatAt: { type: Date, default: Date.now },
        closedAt: { type: Date },
        closeReason: { type: String, enum: ["host_closed", "heartbeat_timeout"] }
    },
    { timestamps: true }
);

module.exports = mongoose.model("Room", roomSchema);
