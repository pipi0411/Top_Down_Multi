const mongoose = require("mongoose");

const userSchema = new mongoose.Schema(
    {
        username: { type: String, unique: true },
        password: String,
        selectedCharacter: { type: String, default: null },
        activeSessionId: { type: String, default: null },
        activeSessionExpiresAt: { type: Date, default: null },
        lastLoginAt: { type: Date, default: null },
        lastLogoutAt: { type: Date, default: null }
    },
    { timestamps: true }
);

module.exports = mongoose.model("User", userSchema);
