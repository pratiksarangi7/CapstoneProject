import { Component, OnInit, signal } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from "@angular/forms";
import { form, minLength, required, pattern, FormField } from "@angular/forms/signals";
import { RegisterModel } from '../../models/register.model';
import { Department } from '../../models/department.model';
import { AuthService } from '../../services/auth.service';
import { Router, RouterLink } from '@angular/router';
import { DepartmentService } from '../../services/department.service';

@Component({
  selector: 'app-register',
  imports: [FormsModule, ReactiveFormsModule, FormField, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.css',
})
export class Register implements OnInit {
  departments = signal<Department[]>([]);
  registerModel = signal({
    name: "",
    email: "",
    password: "",
    departmentId: ""
  });
  progress = signal(false);

  constructor(private authService: AuthService, private departmentService: DepartmentService, private router: Router) { }

  emailRegex = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
  passwordRegex = /^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d@$#]{8,}$/;

  registerForm = form(this.registerModel, (path) => {
    required(path.name, { message: "Name is required" });
    minLength(path.name, 2, { message: "Name must be at least 2 characters long" });
    required(path.email, { message: "Email is required" });
    pattern(path.email, this.emailRegex, { message: "Please enter a valid email address" });
    required(path.password, { message: "Password is required" });
    minLength(path.password, 8, { message: "Password must be at least 8 characters long" });
    pattern(path.password, this.passwordRegex, { message: "Password must contain at least one letter and one number. Special characters: @, $, # are only allowed" });
    required(path.departmentId, { message: "Department is required" });
  });

  ngOnInit(): void {
    this.departmentService.GetAllDepartments().subscribe({
      next: (data) => {
        this.departments.set(data);
        console.log(this.departments());
      },
      error: (error) => {
        console.error('Failed to load departments', error);
      }
    });
  }

  handleRegisterClick() {
    if (this.registerForm().invalid()) {
      alert("Please fix the errors in the form before submitting.");
      return;
    }

    const formValues = this.registerModel();
    const model: RegisterModel = {
      name: formValues.name,
      email: formValues.email,
      password: formValues.password,
      departmentId: Number(formValues.departmentId)
    };

    if (!model.departmentId || model.departmentId === 0) {
      alert("Please select a valid department.");
      return;
    }

    this.progress.set(true);
    this.authService.RegisterApiCall(model).subscribe({
      next: (response: any) => {
        console.log("Registration successful", response);
        alert("Registration successful! Please login.");
        this.router.navigate(['/login']);
        this.progress.set(false);
      },
      error: (error) => {
        console.error("Registration failed", error);
        alert("Registration failed. Please try again.");
        this.progress.set(false);
      }
    });
  }
}
