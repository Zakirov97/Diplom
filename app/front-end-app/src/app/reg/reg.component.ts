import { Component, OnInit } from '@angular/core';
import { CheckRegistryFormService } from '../check-registry-form.service';
import { AuthService } from '../auth.service';
import { FlashMessagesService } from 'angular2-flash-messages';
import { Router } from '@angular/router'

@Component({
  selector: 'app-reg',
  templateUrl: './reg.component.html',
  styleUrls: ['./reg.component.css']
})
export class RegComponent implements OnInit {
  name:String;
  login:String;
  email:String;
  password:String;

  constructor(
    private checkRegistryForm: CheckRegistryFormService,
    private flashMessages: FlashMessagesService,
    private router: Router,
    private authService: AuthService
  ) { }

  ngOnInit(): void {
  }

  userRegisterClick() {
    const user = {
      name: this.name,
      login: this.login,
      email: this.email,
      password: this.password
    }

    if(!this.checkRegistryForm.checkName(user.name)){
      this.flashMessages.show("Name is empty", {
        cssClass: 'alert-danger',
        timeout: 3000
      });
      return false;
    } else if(!this.checkRegistryForm.checkLogin(user.login)){
      this.flashMessages.show("Login is empty", {
        cssClass: 'alert-danger',
        timeout: 3000
      });
      return false;
    } else if(!this.checkRegistryForm.checkEmail(user.email)){
      this.flashMessages.show("Email is empty", {
        cssClass: 'alert-danger',
        timeout: 3000
      });
      return false;
    } else if(!this.checkRegistryForm.checkPassword(user.password)){
      this.flashMessages.show("Password is empty", {
        cssClass: 'alert-danger',
        timeout: 3000
      });
      return false;
    }

    this.authService.registrUser(user).subscribe(data => {
      if(!data.success){
        this.flashMessages.show(data.msg, {
          cssClass: 'alert-danger',
          timeout: 4000
        });
        this.router.navigate(['/reg']);
      } else {
        this.flashMessages.show(data.msg, {
          cssClass: 'alert-success',
          timeout: 2000
        });
        this.router.navigate(['/auth']);
      }
    });
  }
}
