import { Component, OnInit } from '@angular/core';
import { CodAPIService } from '../services/codmwwzAPI/cod-api.service';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { FlashMessagesService } from 'angular2-flash-messages';
import { map } from 'rxjs/operators';
import { stringify } from '@angular/compiler/src/util';

export interface PeriodicElement {
  name: string;
  position: number;
  weight: number;
  symbol: string;
  description: string;
}
export interface TeamsInfo {
  teamName: string,
  teamPlacement: number,
  points: number,
  teamKills: number,
  players: [{
    userName: string,
    kills: number,
    damageDone: number,
    damageTaken: number
  }]
}

const ELEMENT_DATA: PeriodicElement[] = [
  {
    position: 1,
    name: 'Hydrogen',
    weight: 1.0079,
    symbol: 'H',
    description: `Hydrogen is a chemical element with symbol H and atomic number 1. With a standard
        atomic weight of 1.008, hydrogen is the lightest element on the periodic table.`
  }, {
    position: 2,
    name: 'Helium',
    weight: 4.0026,
    symbol: 'He',
    description: `Helium is a chemical element with symbol He and atomic number 2. It is a
        colorless, odorless, tasteless, non-toxic, inert, monatomic gas, the first in the noble gas
        group in the periodic table. Its boiling point is the lowest among all the elements.`
  }, {
    position: 3,
    name: 'Lithium',
    weight: 6.941,
    symbol: 'Li',
    description: `Lithium is a chemical element with symbol Li and atomic number 3. It is a soft,
        silvery-white alkali metal. Under standard conditions, it is the lightest metal and the
        lightest solid element.`
  }, {
    position: 4,
    name: 'Beryllium',
    weight: 9.0122,
    symbol: 'Be',
    description: `Beryllium is a chemical element with symbol Be and atomic number 4. It is a
        relatively rare element in the universe, usually occurring as a product of the spallation of
        larger atomic nuclei that have collided with cosmic rays.`
  }, {
    position: 5,
    name: 'Boron',
    weight: 10.811,
    symbol: 'B',
    description: `Boron is a chemical element with symbol B and atomic number 5. Produced entirely
        by cosmic ray spallation and supernovae and not by stellar nucleosynthesis, it is a
        low-abundance element in the Solar system and in the Earth's crust.`
  }, {
    position: 6,
    name: 'Carbon',
    weight: 12.0107,
    symbol: 'C',
    description: `Carbon is a chemical element with symbol C and atomic number 6. It is nonmetallic
        and tetravalent—making four electrons available to form covalent chemical bonds. It belongs
        to group 14 of the periodic table.`
  }, {
    position: 7,
    name: 'Nitrogen',
    weight: 14.0067,
    symbol: 'N',
    description: `Nitrogen is a chemical element with symbol N and atomic number 7. It was first
        discovered and isolated by Scottish physician Daniel Rutherford in 1772.`
  }, {
    position: 8,
    name: 'Oxygen',
    weight: 15.9994,
    symbol: 'O',
    description: `Oxygen is a chemical element with symbol O and atomic number 8. It is a member of
         the chalcogen group on the periodic table, a highly reactive nonmetal, and an oxidizing
         agent that readily forms oxides with most elements as well as with other compounds.`
  }, {
    position: 9,
    name: 'Fluorine',
    weight: 18.9984,
    symbol: 'F',
    description: `Fluorine is a chemical element with symbol F and atomic number 9. It is the
        lightest halogen and exists as a highly toxic pale yellow diatomic gas at standard
        conditions.`
  }, {
    position: 10,
    name: 'Neon',
    weight: 20.1797,
    symbol: 'Ne',
    description: `Neon is a chemical element with symbol Ne and atomic number 10. It is a noble gas.
        Neon is a colorless, odorless, inert monatomic gas under standard conditions, with about
        two-thirds the density of air.`
  },
];


@Component({
  selector: 'app-leaderboard',
  templateUrl: './leaderboard.component.html',
  styleUrls: ['./leaderboard.component.css'],
  animations: [
    trigger('detailExpand', [
      state('collapsed', style({ height: '0px', minHeight: '0' })),
      state('expanded', style({ height: '*' })),
      transition('expanded <=> collapsed', animate('225ms cubic-bezier(0.4, 0.0, 0.2, 1)')),
    ]),
  ]
})

export class LeaderboardComponent implements OnInit {
  obsName = [
    //{ "name": 'chitozu', "tag": 2110 },
    //{ "name": 'UAPLAYER', "tag": 1672 },
    { "name": 'ShleporezKa', "tag": 2324 }
  ]

  dataSource = ELEMENT_DATA;
  //columnsToDisplay = ['Placement', 'Team Name', 'Points', 'teamKills'];
  columnsToDisplay = [ 
    {
      prop: "teamPlacement",
      name: "Placement"
    },
    {
      prop: "teamName",
      name: "Team Name"
    },
    {
      prop: "points",
      name: "Points"
    },
    {
      prop: "teamKills",
      name: "teamKills"
    }
  ]
  displayedColumns: any[] = this.columnsToDisplay.map(col => col.prop);

  expandedElement: TeamsInfo | null;
  matchData: TeamsInfo[] = [];

  constructor(
    private codAPI: CodAPIService,
    private flashMessages: FlashMessagesService
  ) { }

  ngOnInit(): void {
    for (let i = 0; i < this.obsName.length; i++) {
      this.codAPI.getLastMatchIdByName(this.obsName[i]['name'], this.obsName[i]['tag'])
        .subscribe(matchID => {
          this.codAPI.findMatchId(matchID).subscribe(resFindMatchId => {
            if (resFindMatchId.flag == 0) {
              console.log(resFindMatchId.msg);
            }
            else if (resFindMatchId.flag == 1) {
              //Создаём обращение в базу данных
              this.codAPI.getMatch(matchID)
                .subscribe(match => {
                  console.log(match);
                  this.matchData = match['match']['teams'].map(team => {
                    let players: TeamsInfo["players"] = team['players'].map(player => {
                      return {
                        userName: player['userName'].toString(),
                        kills: Number.parseInt(player['kills']),
                        damageDone: Number.parseInt(player['damageDone']),
                        damageTaken: Number.parseInt(player['damageTaken'])
                      }
                    });

                    return {
                      teamName: team['teamName'],
                      teamPlacement: team['teamPlacement'],
                      teamKills: team['teamKills'],
                      points: team['points'],
                      players: players
                    }
                  });

                  
                })
            }
            else if (resFindMatchId.flag == 2) {
              this.codAPI.addMatchId(matchID)
                .subscribe(data2 => {
                  if (!data2.success) {
                    console.log(data2.msg);
                  } else {
                    console.log(data2.msg);
                  }
                })
              this.codAPI.getFullMatchById(matchID).subscribe(match => {
                this.codAPI.addMatchToDB(match).subscribe(resAddMatch => {
                  if (resAddMatch.flag == 0) {
                    console.log(resAddMatch.msg);
                  }
                  else if (resAddMatch.flag == 1) {
                    console.log(resAddMatch.msg);
                  }
                });

              });
            }
          });

        })
    }
  }
}
