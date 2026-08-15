const jwt = require("jsonwebtoken");
const User = require("../Models/user");

module.exports = async (req, res, next) => {
    const header = req.headers.authorization;
    if (!header) {
        return res.status(401).json({ message: "Missing Authorization header" });
    }

    const [scheme, token] = header.split(" ");
    if (scheme !== "Bearer" || !token) {
        return res.status(401).json({ message: "Invalid Authorization format" });
    }

    try {
        const decoded = jwt.verify(token, process.env.JWT_SECRET);
        const user = await User.findById(decoded.id).select("username activeSessionId activeSessionExpiresAt");
        if (!user) {
            return res.status(401).json({ message: "User not found" });
        }

        if (!decoded.sessionId || user.activeSessionId !== decoded.sessionId) {
            return res.status(401).json({ message: "Session is no longer active. Please login again." });
        }

        if (!user.activeSessionExpiresAt || user.activeSessionExpiresAt <= new Date()) {
            user.activeSessionId = null;
            user.activeSessionExpiresAt = null;
            await user.save();
            return res.status(401).json({ message: "Session expired. Please login again." });
        }

        req.user = {
            id: user._id.toString(),
            username: user.username,
            sessionId: decoded.sessionId
        };
        next();
    } catch (error) {
        if (error.name === "TokenExpiredError") {
            return res.status(401).json({ message: "Token expired" });
        }
        return res.status(403).json({ message: "Invalid token" });
    }
};
