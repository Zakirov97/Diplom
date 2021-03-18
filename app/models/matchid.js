const mongoose = require('mongoose');
const config = require('../config/db');

const MatchIDSchema = mongoose.Schema({
    matchID: {
        type: String,
        required: true
    }
})

const MatchID = module.exports = mongoose.model('MatchID', MatchIDSchema);

module.exports.getMatchID = function (matchID, callback) {
    const query = { matchID: matchID };
    MatchID.findOne(query, callback);
}

module.exports.addMatchID = function (newMatchID, callback) {
    newMatchID.save(callback);
}