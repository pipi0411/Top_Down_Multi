const mongoose = require("mongoose");

const userSchema = new mongoose.Schema(
    {
        username: { type: String, unique: true },
        password: String,
        selectedCharacter: { type: String, default: null }
    },
    { timestamps: true }
);

module.exports = mongoose.model("User", userSchema);