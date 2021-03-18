const mongoose = require('mongoose');
const config = require('../config/db');

const MatchSchema = mongoose.Schema({
  _id: Number,
  matchId: Number,
  matchDuration: String,
  teams: [{
    teamName: String,
    teamPlacement: Number,
    points: Number,
    teamKills: Number,
    players: [{
      userName: String,
      kills: Number,
      damageDone: Number,
      damageTaken: Number
    }]
  }]
});

const Match = module.exports = mongoose.model('Match', MatchSchema);

module.exports.getMatchById = function (matchID, callback) {
  Match.findById(matchID, callback);
}
module.exports.addMatch = function (newMatch, callback) {
  newMatch.save(callback);
}


