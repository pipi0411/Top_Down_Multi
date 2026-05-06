const express = require("express");
const Room = require("../Models/room");
const RoomPlayer = require("../Models/roomPlayer");
const GameSession = require("../Models/gameSession");
const Round = require("../Models/round");
const PlayerScore = require("../Models/playerScore");
const authMiddleware = require("../Middleware/authMiddleware");

const router = express.Router();

// Mock questions database
const QUESTIONS = [
    {
        id: 1,
        text: "What is 2 + 2?",
        correctAnswer: "4"
    },
    {
        id: 2,
        text: "What is the capital of France?",
        correctAnswer: "Paris"
    },
    {
        id: 3,
        text: "What is the largest planet in our solar system?",
        correctAnswer: "Jupiter"
    },
    {
        id: 4,
        text: "Who painted the Mona Lisa?",
        correctAnswer: "Leonardo da Vinci"
    },
    {
        id: 5,
        text: "What is the smallest country in the world?",
        correctAnswer: "Vatican City"
    }
];

function getRandomQuestion(excludeIds = []) {
    const available = QUESTIONS.filter(q => !excludeIds.includes(q.id));
    if (available.length === 0) return QUESTIONS[Math.floor(Math.random() * QUESTIONS.length)];
    return available[Math.floor(Math.random() * available.length)];
}

router.post("/:roomCode/game/start", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();

        const room = await Room.findOne({ roomCode, status: "playing" });
        if (!room) {
            return res.status(404).json({ message: "Room not found or game not started" });
        }

        if (String(room.ownerId) !== String(req.user.id)) {
            return res.status(403).json({ message: "Only host can start game session" });
        }

        // Check if session already exists
        let gameSession = await GameSession.findOne({ roomId: room._id });
        if (gameSession) {
            return res.status(400).json({ message: "Game session already exists" });
        }

        // Create game session
        gameSession = await GameSession.create({
            roomId: room._id,
            status: "in_progress",
            startedAt: new Date()
        });

        // Initialize player scores
        const roomPlayers = await RoomPlayer.find({ roomId: room._id }).populate("userId", "username");
        for (const player of roomPlayers) {
            await PlayerScore.create({
                gameSessionId: gameSession._id,
                userId: player.userId._id,
                username: player.userId.username,
                character: player.character
            });
        }

        // Create first round with question
        const question = getRandomQuestion();
        const round = await Round.create({
            gameSessionId: gameSession._id,
            roundNumber: 1,
            question: question.text,
            correctAnswer: question.correctAnswer,
            status: "active",
            startedAt: new Date()
        });

        return res.json({
            success: true,
            gameSessionId: gameSession._id,
            round: {
                roundNumber: round.roundNumber,
                question: round.question,
                timeLimit: round.timeLimit
            }
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.get("/:roomCode/game/session", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();

        const room = await Room.findOne({ roomCode });
        if (!room) {
            return res.status(404).json({ message: "Room not found" });
        }

        const gameSession = await GameSession.findOne({ roomId: room._id });
        if (!gameSession) {
            return res.status(404).json({ message: "No active game session" });
        }

        const currentRound = await Round.findOne({
            gameSessionId: gameSession._id,
            roundNumber: gameSession.currentRound || 1
        }).select("roundNumber question timeLimit status");

        return res.json({
            sessionId: gameSession._id,
            status: gameSession.status,
            currentRound: gameSession.currentRound || 1,
            totalRounds: gameSession.totalRounds,
            round: currentRound
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.get("/:roomCode/game/question", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();

        const room = await Room.findOne({ roomCode });
        if (!room) {
            return res.status(404).json({ message: "Room not found" });
        }

        const gameSession = await GameSession.findOne({ roomId: room._id });
        if (!gameSession) {
            return res.status(404).json({ message: "No active game session" });
        }

        const round = await Round.findOne({
            gameSessionId: gameSession._id,
            status: "active"
        }).select("roundNumber question timeLimit startedAt");

        if (!round) {
            return res.status(404).json({ message: "No active round" });
        }

        const elapsedTime = Math.floor((Date.now() - new Date(round.startedAt).getTime()) / 1000);
        const timeRemaining = Math.max(0, round.timeLimit - elapsedTime);

        return res.json({
            roundNumber: round.roundNumber,
            question: round.question,
            timeRemaining,
            timeLimit: round.timeLimit
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.post("/:roomCode/game/answer", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();
        const answer = (req.body?.answer || "").trim();

        if (!answer) {
            return res.status(400).json({ message: "Answer is required" });
        }

        const room = await Room.findOne({ roomCode });
        if (!room) {
            return res.status(404).json({ message: "Room not found" });
        }

        const gameSession = await GameSession.findOne({ roomId: room._id });
        if (!gameSession) {
            return res.status(404).json({ message: "No active game session" });
        }

        const round = await Round.findOne({
            gameSessionId: gameSession._id,
            status: "active"
        });

        if (!round) {
            return res.status(404).json({ message: "No active round" });
        }

        // Check if player already answered
        const existingAnswer = round.answers.find(a => String(a.userId) === String(req.user.id));
        if (existingAnswer) {
            return res.status(400).json({ message: "You already submitted an answer for this round" });
        }

        // Check if answer is correct (case-insensitive)
        const isCorrect = answer.toLowerCase() === round.correctAnswer.toLowerCase();

        // Add answer to round
        round.answers.push({
            userId: req.user.id,
            answer,
            isCorrect,
            submittedAt: new Date()
        });
        await round.save();

        // Update player score
        const playerScore = await PlayerScore.findOne({
            gameSessionId: gameSession._id,
            userId: req.user.id
        });

        if (playerScore) {
            if (isCorrect) {
                playerScore.correctCount += 1;
                playerScore.totalScore += 10;
                playerScore.roundScores.push({
                    roundNumber: round.roundNumber,
                    correct: true,
                    points: 10
                });
            } else {
                playerScore.roundScores.push({
                    roundNumber: round.roundNumber,
                    correct: false,
                    points: 0
                });
            }
            await playerScore.save();
        }

        return res.json({
            success: true,
            isCorrect,
            message: isCorrect ? "Correct!" : `Wrong! The answer was: ${round.correctAnswer}`
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.post("/:roomCode/game/round/complete", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();

        const room = await Room.findOne({ roomCode });
        if (!room) {
            return res.status(404).json({ message: "Room not found" });
        }

        if (String(room.ownerId) !== String(req.user.id)) {
            return res.status(403).json({ message: "Only host can complete round" });
        }

        const gameSession = await GameSession.findOne({ roomId: room._id });
        if (!gameSession) {
            return res.status(404).json({ message: "No active game session" });
        }

        const currentRound = await Round.findOne({
            gameSessionId: gameSession._id,
            roundNumber: gameSession.currentRound || 1
        });

        if (!currentRound) {
            return res.status(404).json({ message: "Current round not found" });
        }

        // Complete current round
        currentRound.status = "completed";
        currentRound.endedAt = new Date();
        await currentRound.save();

        // Check if there are more rounds
        const nextRoundNumber = (gameSession.currentRound || 1) + 1;
        if (nextRoundNumber <= gameSession.totalRounds) {
            // Create next round
            gameSession.currentRound = nextRoundNumber;
            await gameSession.save();

            const question = getRandomQuestion();
            const nextRound = await Round.create({
                gameSessionId: gameSession._id,
                roundNumber: nextRoundNumber,
                question: question.text,
                correctAnswer: question.correctAnswer,
                status: "active",
                startedAt: new Date()
            });

            return res.json({
                success: true,
                message: "Round completed, next round starting",
                nextRound: {
                    roundNumber: nextRound.roundNumber,
                    question: nextRound.question,
                    timeLimit: nextRound.timeLimit
                }
            });
        } else {
            // Game completed
            gameSession.status = "completed";
            gameSession.endedAt = new Date();
            await gameSession.save();

            return res.json({
                success: true,
                message: "Game completed",
                gameEnded: true
            });
        }
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.get("/:roomCode/game/results", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();

        const room = await Room.findOne({ roomCode });
        if (!room) {
            return res.status(404).json({ message: "Room not found" });
        }

        const gameSession = await GameSession.findOne({ roomId: room._id });
        if (!gameSession) {
            return res.status(404).json({ message: "No active game session" });
        }

        // Get all player scores sorted by total score (descending)
        const playerScores = await PlayerScore.find({ gameSessionId: gameSession._id })
            .sort({ totalScore: -1 });

        return res.json({
            gameSessionId: gameSession._id,
            status: gameSession.status,
            completedRounds: gameSession.currentRound || 1,
            totalRounds: gameSession.totalRounds,
            leaderboard: playerScores.map((ps, index) => ({
                rank: index + 1,
                userId: ps.userId,
                username: ps.username,
                character: ps.character,
                correctCount: ps.correctCount,
                totalScore: ps.totalScore,
                roundScores: ps.roundScores
            }))
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.get("/:roomCode/game/round/results", authMiddleware, async (req, res) => {
    try {
        const roomCode = (req.params.roomCode || "").trim().toUpperCase();
        const roundNumber = req.query.round || 1;

        const room = await Room.findOne({ roomCode });
        if (!room) {
            return res.status(404).json({ message: "Room not found" });
        }

        const gameSession = await GameSession.findOne({ roomId: room._id });
        if (!gameSession) {
            return res.status(404).json({ message: "No active game session" });
        }

        const round = await Round.findOne({
            gameSessionId: gameSession._id,
            roundNumber: Number(roundNumber)
        }).populate("answers.userId", "username");

        if (!round) {
            return res.status(404).json({ message: "Round not found" });
        }

        return res.json({
            roundNumber: round.roundNumber,
            question: round.question,
            correctAnswer: round.correctAnswer,
            answers: round.answers.map(a => ({
                username: a.userId.username,
                answer: a.answer,
                isCorrect: a.isCorrect,
                submittedAt: a.submittedAt
            }))
        });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

module.exports = router;
