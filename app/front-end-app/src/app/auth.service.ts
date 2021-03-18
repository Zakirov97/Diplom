import { Injectable } from '@angular/core';
import { Http, Headers } from '@angular/http';
//import 'rxjs/add/operator/map';
import { from } from 'rxjs';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { tokenNotExpired } from 'angular2-jwt';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  token: any;
  user: any;
  constructor(
    private http: Http
  ) { }

  registrUser(user) {
    let headers = new Headers();
    headers.append('Content-Type', 'application/json');
    return this.http.post(
      //'account/reg',
      'http://localhost:3000/account/reg',
      user,
      { headers: headers }).pipe(map(res => res.json()));
  }

  authUser(user) {
    let headers = new Headers();
    headers.append('Content-Type', 'application/json');
    return this.http.post(
      //'account/auth',
      'http://localhost:3000/account/auth',
      user,
      { headers: headers }).pipe(map(res => res.json()));
  }

  storeUser(token, user) {
    localStorage.setItem('token', token);
    localStorage.setItem('user', JSON.stringify(user));
    this.token = token;
    this.user = user;
  }

  logout() {
    this.token = null;
    this.user = null;
    localStorage.clear();
  }

  isLoggedIn() {
    return tokenNotExpired();
  }
}
