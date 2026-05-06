const mongoose = require("mongoose");

const roundSchema = new mongoose.Schema(
    {
        gameSessionId: { type: mongoose.Schema.Types.ObjectId, ref: "GameSession", required: true },
        roundNumber: { type: Number, required: true },
        question: { type: String, required: true },
        answers: [
            {
                userId: { type: mongoose.Schema.Types.ObjectId, ref: "User", required: true },
                answer: { type: String },
                isCorrect: { type: Boolean, default: false },
                submittedAt: { type: Date }
            }
        ],
        correctAnswer: { type: String },
        status: { type: String, enum: ["pending", "active", "completed"], default: "pending" },
        startedAt: { type: Date },
        endedAt: { type: Date },
        timeLimit: { type: Number, default: 30 } // seconds
    },
    { timestamps: true, collection: "rounds" }
);

module.exports = mongoose.model("Round", roundSchema);
