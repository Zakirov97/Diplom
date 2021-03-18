const express = require('express');
const router = express.Router();
const Match = require('../models/match');
const MatchID = require('../models/matchid');
const config = require('../config/db');
var bodyParser = require('body-parser');

router.use(bodyParser.json());
router.use(bodyParser.urlencoded({ extended: true }));

router.get('/lb', (req, res) => {
  res.send('My Leaderboard');
});

router.post('/findMatchId', (req, res) => {
  let matchID = req.body['matchID'];

  MatchID.getMatchID(matchID, (err, matchid) => {
    if (err) {
      return res.json({ success: false, msg: "Can't get response from db, got error.", flag: 0 });
    }
    if (!matchid) {
      return res.json({ success: false, msg: "This Match ID doesn't exist.", flag: 2, matchID: matchID })
    }
    res.json({ success: true, msg: "This Match ID exist.", flag: 1, matchID: matchID });
  })
});

router.post('/addMatchId', (req, res) => {
  let matchID = new MatchID({ matchID: req.body['matchID'] });

  MatchID.addMatchID(matchID, (err, matchid) => {
    if (err)
      return res.json({ success: false, msg: "Can't get response from db, got error.", flag: 0 });
    else
      return res.json({ success: true, msg: "Match ID was added", flag: 1 });
  })
})

router.post('/getMatch', (req, res) => {
  let matchID = req.body['matchID'];

  Match.getMatchById(matchID, (err, match) => {
    if (err) {
      return res.json({ success: false, msg: "Can't get response from db, got error.", flag: 0 });
    }
    if (!match) {
      return res.json({ success: false, msg: "This Match doesn't exist.", flag: 2, matchID: matchID })
    }
    res.json({ success: true, msg: "This Match exist.", flag: 1, match: match });
  })
})

router.post('/addMatchToDB', (req, res) => {
  let reqMatch = req.body['match'];
  let matchID = reqMatch['matchID'];
  let matchDuration = reqMatch['matchDuration'];
  let teams = reqMatch['allTeams'];
  let teamList = [];
  teams.forEach(team => {
    let playersList = [];
    team['players'].forEach(player => {
      let pl = {
        userName: player['username'],
        kills: player['kills'],
        damageDone: player['damageDone'],
        damageTaken: player['damageTaken']
      }
      playersList.push(pl);
    });

    let tm = {
      teamName: team['teamName'],
      teamPlacement: team['teamPlacement'],
      points: team['points'],
      teamKills: team['teamKills'],
      players: playersList
    }
    teamList.push(tm);
  });

  let match = new Match({
    _id: matchID,
    matchID: matchID,
    matchDuration: matchDuration,
    teams: teamList
  });
  console.log(teamList);

  Match.addMatch(match, (err, matchid) => {
    if (err)
      return res.json({ success: false, msg: "Can't get response from db, got error. " + err, flag: 0 });
    else
      return res.json({ success: true, msg: "Match was added", flag: 1 });
  });
  //res.json({ success: true, msg: "TEST ZATICHKA", flag: 1 });
})

module.exports = router;
