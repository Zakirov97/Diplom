import { Component, OnInit } from '@angular/core';
import { HttpClient, HttpParams } from "@angular/common/http";
import { Observable } from 'rxjs';
import { switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})

export class HomeComponent implements OnInit {

  constructor(private http: HttpClient) { }

  ngOnInit(): void {
    

    // this.first("UAPLAYER","1672").pipe(
    //   switchMap(matchID => {
    //     return this.second(matchID['matchID']);
    //   })
    // ).subscribe(
    //   res => {
    //     res['allPlayers'].forEach(element => {
    //           let pl = new PlayerInfo(element['player']['username'], 
    //                                   element['playerStats']['kills'],
    //                                   element['playerStats']['teamPlacement'],
    //                                   element['player']['team'],
    //                                   element['matchID'], 
    //                                   element['duration']
    //                                   );
    //           this.players2.push(pl);
    //         });

    //     this.players2.sort((a,b)=> {
    //       if(a.teamPlacement > b.teamPlacement) return 1;
    //       if(a.teamPlacement < b.teamPlacement) return -1;
    //       return 0
    //     });
    //   }
    // )
  }

  private first(name: string, tag: string): Observable<any> {
    return this.http.get<any>(`https://cod-mw-wz-api.herokuapp.com/getmatchidbyname?obsname=${name}&obstag=${tag}`)
  }
  private second(id: string): Observable<any> {
    return this.http.get<any>(`https://cod-mw-wz-api.herokuapp.com/getmatchbyid?matchid=${id}`);
  }

}


