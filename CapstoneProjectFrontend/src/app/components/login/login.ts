import { Component, signal } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from "@angular/forms";
import { form, minLength, required, pattern, FormField } from "@angular/forms/signals";
import { LoginModel } from '../../models/login.model';
import { AuthService } from '../../services/auth.service';
import { Router, RouterLink } from '@angular/router';
import { isAdmin } from '../../helpers';

@Component({
  selector: 'app-login',
  imports: [FormsModule, ReactiveFormsModule, FormField, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  loginModel = signal<LoginModel>({ email: "", password: "" });
  progress = signal(false);

  constructor(private authService: AuthService, private router: Router) { }

  emailRegex = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
  passwordRegex = /^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{8,}$/;

  loginForm = form(this.loginModel, (path) => {
    required(path.email, { message: "Email is required" });
    pattern(path.email, this.emailRegex, { message: "Please enter a valid email address" });
    required(path.password, { message: "Password is required" });
    minLength(path.password, 8, { message: "Password must be at least 8 characters long" });
    pattern(path.password, this.passwordRegex, { message: "Password must contain at least one letter and one number" });
  });
  handleLoginClick() {
    if (this.loginForm().invalid()) {
      alert("Please fix the errors in the form before submitting.");
      return;
    }
    this.progress.set(true);
    this.authService.LoginApiCall(this.loginModel()).subscribe({
      next: (response: any) => {
        console.log("Login successful", response);
        localStorage.setItem('token', response.token);
        this.progress.set(false);
        if (isAdmin()) this.router.navigate(['/admin-dashboard']);
        else this.router.navigate(['/user-dashboard'])
      },
      error: (error) => {
        console.error("Login failed", error);
        alert("Login failed. Please try again.");
        this.progress.set(false);
      }

    })
  }
}