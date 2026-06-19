import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { LoginModel } from "../models/login.model";
import { baseUrl } from "../../environment";
import { RegisterModel } from "../models/register.model";
import { Department } from "../models/department.model";
import { Observable } from "rxjs";

@Injectable({
    providedIn: "root"
})
export class AuthService {
    constructor(private http: HttpClient) {
    }
    public LoginApiCall(loginModel: LoginModel) {
        const url = baseUrl + "/Auth/login";
        return this.http.post(url, loginModel)
    }
    public RegisterApiCall(registerModel: RegisterModel) {
        const url = baseUrl + "/Auth/register";
        return this.http.post(url, registerModel);
    }
}