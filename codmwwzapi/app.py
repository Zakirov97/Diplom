import asyncio

import flask
from flask import request
from flask_cors import CORS

from MatchInfo import MatchInfo
import callofduty
from callofduty import Mode, Platform, Reaction, Title, Client, Player

app = flask.Flask(__name__)
loop = asyncio.get_event_loop()
CORS(app)

list_points = {"1": 20,
               "2": 14,
               "3": 12,
               "4": 10,
               "5": 8,
               "6": 6,
               "7": 4,
               "8": 3,
               "9": 2,
               "10": 1,
               "11": 0,
               "12": 0,
               "13": 0,
               "14": 0,
               "15": 0,
               "16": 0,
               "17": 0,
               "18": 0,
               "19": 0,
               "20": 0}


@app.route('/')
def hello_world():
    return 'Hello World!'


@app.route('/getmatchidbyname', methods=['GET'])
def get_match_id_by_name():
    obs_name = request.args['obsname']
    obs_tag = request.args['obstag']
    matchID = loop.run_until_complete(get_last_match_id(obs_name + "#" + obs_tag))
    return {"matchID": f"{matchID}"}


@app.route('/getmatchbyid', methods=['GET'])
def get_match_by_id():
    match_id = request.args['matchid']
    resp = loop.run_until_complete(get_full_match(match_id))
    return resp


async def get_last_match_id(obs_name):
    try:
        client = await callofduty.Login("login", "password")
        player = await client.GetPlayer(Platform.BattleNet, obs_name)
        matchID = (await player.matches(Title.ModernWarfare, Mode.Warzone, limit=1))[0]
        return matchID.id
    except:
        return f"Whoops, error with getting match id.({obs_name})"


async def get_full_match(matchID):
    try:
        client = await callofduty.Login("login", "password")
        match = await client.GetFullMatch(Platform.BattleNet, Title.ModernWarfare, Mode.Warzone, matchID)

        all_players = match['allPlayers']
        team_names = []
        for pl in all_players:
            cp = True
            for tn in team_names:
                if pl['player']['team'] == tn:
                    cp = False
                    break
            if cp:
                team_names.append(pl['player']['team'])

        all_teams = []
        matchDuration = 0
        for team_name in team_names:
            team_players = []
            for pl in all_players:
                if pl['player']['team'] == team_name:
                    m: MatchInfo = MatchInfo(pl['player']['username'],
                                             int(pl['playerStats']['kills']),
                                             int(pl['playerStats']['damageDone']),
                                             int(pl['playerStats']['damageTaken']),
                                             int(pl['playerStats']['teamPlacement']),
                                             pl['player']['team'],
                                             pl['matchID'],
                                             pl['duration'])
                    team_players.append(m)

            team_kills = 0
            new_team_players = []
            for player in team_players:
                team_kills += player.kills
                new_team_players.append({"username": player.username,
                                         "kills": player.kills,
                                         "damageDone": player.damage_done,
                                         "damageTaken": player.damage_taken})
            matchDuration = team_players[0].duration
            all_teams.append({"teamName": team_name,
                              "teamPlacement": team_players[0].placement,
                              "points": list_points[
                                            f'{team_players[0].placement if team_players[0].placement <= 20 else 20}'] + team_kills,
                              # "points": list_points[int(team_players[0].placement)],
                              "teamKills": team_kills,
                              "players": new_team_players
                              })

        all_teams = sorted(all_teams, key=lambda i: i['teamPlacement'])
        return {"match": {
            "matchID": matchID,
            "matchDuration": matchDuration,
            "allTeams": all_teams
        }}
    except:
        return f"Whoops, error with getting match info by id.({matchID})"
