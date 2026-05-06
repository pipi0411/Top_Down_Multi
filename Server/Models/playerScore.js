const mongoose = require("mongoose");

const playerScoreSchema = new mongoose.Schema(
    {
        gameSessionId: { type: mongoose.Schema.Types.ObjectId, ref: "GameSession", required: true },
        userId: { type: mongoose.Schema.Types.ObjectId, ref: "User", required: true },
        username: { type: String },
        character: { type: String },
        correctCount: { type: Number, default: 0 },
        totalScore: { type: Number, default: 0 },
        roundScores: [
            {
                roundNumber: { type: Number },
                correct: { type: Boolean },
                points: { type: Number, default: 0 }
            }
        ]
    },
    { timestamps: true, collection: "player_scores" }
);

module.exports = mongoose.model("PlayerScore", playerScoreSchema);
