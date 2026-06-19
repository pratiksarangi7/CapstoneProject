import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { baseUrl } from "../../environment";
import { Observable } from "rxjs";
import { OtherDepartmentUsersResponseDto } from "../dtos/other-departments-users.response.dto";

@Injectable({ providedIn: "root" })

export class UserService {
    constructor(private http: HttpClient) {
    }
    public getOtherDeptUsers(): Observable<OtherDepartmentUsersResponseDto[]> {
        const url = `${baseUrl}/User/external`;
        return this.http.get<OtherDepartmentUsersResponseDto[]>(url);
    }
}