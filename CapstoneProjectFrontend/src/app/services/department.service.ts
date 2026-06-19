import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { Observable } from "rxjs";
import { Department } from "../models/department.model";
import { baseUrl } from "../../environment";

@Injectable({
    providedIn: "root"
})
export class DepartmentService {
    constructor(private http: HttpClient) {
    }
    public GetAllDepartments(): Observable<Department[]> {
        const url = baseUrl + "/User/departments";
        return this.http.get<Department[]>(url);
    }

}