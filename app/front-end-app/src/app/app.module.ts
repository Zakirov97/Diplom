import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { HeaderComponent } from './header/header.component';
import { RegComponent } from './reg/reg.component';
import { AuthComponent } from './auth/auth.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { HomeComponent } from './home/home.component';

import { RouterModule, Routes } from '@angular/router';
import { FooterComponent } from './footer/footer.component';

import { FormsModule } from '@angular/forms';
import { FlashMessagesModule } from 'angular2-flash-messages';
import { CheckRegistryFormService } from './check-registry-form.service';
import { AuthService } from './auth.service';
import { CodAPIService } from './services/codmwwzAPI/cod-api.service'
import { HttpModule } from '@angular/http';

import { IsLoggedIn } from './isLogged.guard';
import { HttpClientModule } from '@angular/common/http';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { MatTableModule } from '@angular/material/table';
import { LeaderboardComponent } from './leaderboard/leaderboard.component';


const appRoute: Routes = [
  { path: '', component: HomeComponent},
  { path: 'reg', component: RegComponent},
  { path: 'auth', component: AuthComponent},
  { path: 'leaderboard', component: LeaderboardComponent},
  { path: 'dashboard', component: DashboardComponent, canActivate: [IsLoggedIn]}
];

@NgModule({
  declarations: [
    AppComponent,
    HeaderComponent,
    RegComponent,
    AuthComponent,
    DashboardComponent,
    HomeComponent,
    FooterComponent,
    LeaderboardComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    RouterModule.forRoot(appRoute),
    FormsModule,
    FlashMessagesModule.forRoot(),
    HttpModule,
    HttpClientModule,
    BrowserAnimationsModule,
    MatTableModule    
  ],
  providers: [
    CheckRegistryFormService,
    AuthService,
    IsLoggedIn,
    CodAPIService
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
