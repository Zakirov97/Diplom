import asyncio
import os

from dotenv import load_dotenv

import callofduty
from callofduty import Mode, Platform, Reaction, Title
from MatchInfo import PlayerInfo




# async def main():
#     load_dotenv()
#     client = await callofduty.Login(
#         "dias.zakirov97@mail.ru", "w0xihu4nshuiji40"
#     )
#
#     player = await client.GetPlayer(Platform.BattleNet, "UAPLAYER#1672")
#     match = (await player.matches(Title.ModernWarfare, Mode.Warzone, limit=1))[0]
#     match = await client.GetFullMatch(Platform.BattleNet, Title.ModernWarfare, Mode.Warzone, match.id)
#     return match
    # players = []
    # for player in match['allPlayers']:
    #     players.append(PlayerInfo(player['player']['username'],
    #                               player['playerStats']['kills'],
    #                               player['player']['rank'],
    #                               player['player']['team']
    #                               ))
    #
    # for player in players:
    #     print("username - " + player.username + ", kills - " + player.kills + ", placement - " + player.placement + ", team name - " + player.teamName + ";")
    #     print("\n")


# asyncio.get_event_loop().run_until_complete(main())
