const mongoose = require("mongoose");

const roomPlayerSchema = new mongoose.Schema(
    {
        roomId: { type: mongoose.Schema.Types.ObjectId, ref: "Room", required: true },
        userId: { type: mongoose.Schema.Types.ObjectId, ref: "User", required: true },
        character: { type: String, default: null },
        isReady: { type: Boolean, default: false },
        role: { type: String, enum: ["owner", "player"], default: "player" }
    },
    { timestamps: true, collection: "room_players" }
);

roomPlayerSchema.index({ roomId: 1, userId: 1 }, { unique: true });

module.exports = mongoose.model("RoomPlayer", roomPlayerSchema);
