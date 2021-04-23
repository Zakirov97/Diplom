import { Injectable } from '@angular/core';
import { HttpClient, HttpParams, HttpHeaders, HttpClientModule } from "@angular/common/http";
//import { Http} from '@angular/http';
import { Observable } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { NumberValueAccessor } from '@angular/forms';
import { map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class CodAPIService {

  teams: TeamInfo[] = [];

  constructor(private http: HttpClient) { }

  getLastMatchIdByName(name: string, tag: number): Observable<any> {
    //return this.http.get<any>(`https://cod-mw-wz-api.herokuapp.com/getmatchidbyname?obsname=${name}&obstag=${tag}`);
    return this.http.get<any>(`https://delovoy.herokuapp.com/getmatchidbyname?obsname=${name}&obstag=${tag}`);
  }

  findMatchId(matchID: any) {
    let headers = new HttpHeaders();
    headers.set('Content-Type', 'application/json;');
    let res = this.http.post<any>(
      'leaderboard/findMatchId',
      //'http://localhost:3000/leaderboard/findMatchId',
      matchID,
      { headers: headers }).pipe(map(res => res));
    console.log(res);
    return res;
  }

  addMatchId(matchID: any) {
    let headers = new HttpHeaders();
    headers.set('Content-Type', 'application/json;');
    //headers.append('Content-Type', 'application/json');
    let res = this.http.post<any>(
      'leaderboard/addMatchId',
      //'http://localhost:3000/leaderboard/addMatchId',
      matchID,
      { headers: headers }).pipe(map(res => res));
    console.log(res);
    return res;
  }


  getFullMatchById(matchID: any): Observable<any> {
    //return this.http.get<any>(`https://cod-mw-wz-api.herokuapp.com/getmatchbyid?matchid=${matchID['matchID']}`);
    return this.http.get<any>(`https://delovoy.herokuapp.com/getmatchbyid?matchid=${matchID['matchID']}`);
  }

  addMatchToDB(match: any){
    let headers = new HttpHeaders();
    headers.set('Content-Type', 'application/json;');
    let res = this.http.post<any>(
      'leaderboard/addMatchToDB',
      //'http://localhost:3000/leaderboard/addMatchToDB',
      match,
      { headers: headers }).pipe(map(res => res));
    console.log(res);
    return res;
  }

  getMatch(matchID: any) {
    let headers = new HttpHeaders();
    headers.set('Content-Type', 'application/json;');
    let res = this.http.post<any>(
      'leaderboard/getMatch',
      //'http://localhost:3000/leaderboard/getMatch',
      matchID,
      { headers: headers }).pipe(map(res => res));
    console.log(res);
    return res;
  }
  



  addFullMatchById(matchID: number) {
    this.getFullMatchById(matchID).pipe(
      switchMap(teams => {
        return this.addFullMatch(teams);
      })
    ).subscribe(
      res => {
        res['allTeams'].forEach(team => {
          let newTeam = new TeamInfo(team['teamName'],
            team['teamPlacement'],
            team['points'],
            team['teamKills'],
            team['matchDuration'],
            matchID,
            team['players'].forEach(player => {
              return new PlayerInfo(player['players']['username'],
                player['players']['kills'],
                player['players']['damageDone'],
                player['players']['damageTaken']);
            })
          );
          this.teams.push(newTeam);
        });
      }
    )

  }



  addMatchIdByName(name: string, tag: number) {
    return this.getLastMatchIdByName(name, tag).pipe(
      switchMap(matchID => {
        return this.addMatchId(matchID['matchID']);
        //let res = this.addMatchId('111111111322222222222');
        //console.log(res);
        //return res;
      })
    )
  }

  private addFullMatch(teams: any) {
    let headers = new HttpHeaders();
    headers.append('Content-Type', 'application/json');
    return this.http.post(
      'leaderboard/lb',
      //'http://localhost:3000/leaderboard/lb',
      teams,
      { headers: headers });
  }
}

class TeamInfo {
  teamName: string;
  teamPlacement: number;
  points: number;
  teamKills: number;
  matchDuration: string;
  matchId: number;
  players: PlayerInfo[];

  constructor(teamName: string, teamPlacement: number, points: number, teamKills: number, matchDuration: string, matchId: number, players: PlayerInfo[]) {
    this.teamName = teamName;
    this.teamPlacement = teamPlacement;
    this.points = points;
    this.teamKills = teamKills;
    this.matchDuration = matchDuration;
    this.matchId = matchId;
    this.players = players;
  }
}

class PlayerInfo {
  username: string;
  kills: number;
  damageDone: number;
  damageTaken: number;

  constructor(username: string, kills: number, damageDone: number, damageTaken: number) {
    this.username = username;
    this.kills = kills;
    this.damageDone = damageDone;
    this.damageTaken = damageTaken;
  }
}
