const mongoose = require("mongoose");

const playerSaveSchema = new mongoose.Schema(
    {
        userId: {
            type: mongoose.Schema.Types.ObjectId,
            ref: "User",
            required: true,
            unique: true,
            index: true
        },
        saveData: {
            type: mongoose.Schema.Types.Mixed,
            required: true
        }
    },
    { timestamps: true }
);

module.exports = mongoose.model("PlayerSave", playerSaveSchema);
