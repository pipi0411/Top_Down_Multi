const express = require("express");
const bcrypt = require("bcryptjs");
const jwt = require("jsonwebtoken");
const User = require("../Models/user");
const authMiddleware = require("../Middleware/authMiddleware");

const router = express.Router();

router.post("/register", async (req, res) => {
    try {
        const body = req.body || {};
        const { username, password } = body;

        if (!username || !password) {
            return res.status(400).json({ message: "username and password are required" });
        }

        const exist = await User.findOne({ username });
        if (exist) {
            return res.status(409).json({ message: "User already exists" });
        }

        const hash = await bcrypt.hash(password, 10);
        const user = new User({ username, password: hash });
        await user.save();

        return res.status(201).json({ message: "Registered" });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.post("/login", async (req, res) => {
    try {
        const body = req.body || {};
        const { username, password } = body;

        if (!username || !password) {
            return res.status(400).json({ message: "username and password are required" });
        }

        const user = await User.findOne({ username });
        if (!user) {
            return res.status(400).json({ message: "User not found" });
        }

        const match = await bcrypt.compare(password, user.password);
        if (!match) {
            return res.status(400).json({ message: "Wrong password" });
        }

        const token = jwt.sign(
            { id: user._id, username: user.username },
            process.env.JWT_SECRET,
            { expiresIn: "1h" }
        );

        return res.json({ token });
    } catch (error) {
        return res.status(500).json({ message: error.message });
    }
});

router.get("/protected", authMiddleware, (req, res) => {
    res.json({ message: "Access granted", user: req.user });
});

module.exports = router;