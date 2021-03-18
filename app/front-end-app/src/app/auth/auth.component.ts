import { Component, OnInit } from '@angular/core';
import { AuthService } from '../auth.service';
import { FlashMessagesService } from 'angular2-flash-messages';
import { Router } from '@angular/router'

@Component({
  selector: 'app-auth',
  templateUrl: './auth.component.html',
  styleUrls: ['./auth.component.css']
})
export class AuthComponent implements OnInit {

  login: String;
  password: String;

  constructor(
      private flashMessages: FlashMessagesService,
      private router: Router,
      private authService: AuthService
    ) { }

  ngOnInit(): void {
  }

  userAuthClick(){
    const user = {
      login: this.login,
      password: this.password
    }

    if (user.password == undefined) {
      this.flashMessages.show("Input password pls", {
        cssClass: 'alert-danger',
        timeout: 4000
      });
      return false;
    }

    this.authService.authUser(user).subscribe(data => {
      if (!data.success) {
        this.flashMessages.show(data.msg, {
          cssClass: 'alert-danger',
          timeout: 4000
        });
      } else {
        this.flashMessages.show("Signed in", {
          cssClass: 'alert-success',
          timeout: 4000
        });
        this.router.navigate(['dashboard']);
        this.authService.storeUser(data.token, data.user);
      }
    });
  }
}
