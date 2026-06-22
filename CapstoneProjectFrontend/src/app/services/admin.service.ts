import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { baseUrl } from "../../environment";
import { UserDetailsResponseDto, UserDetails } from "../dtos/user-details-response.dto";
import { Observable } from "rxjs";
import { ChangeDepartmentRequestDto } from "../dtos/change-department.request.dto";
import { ChangeLevelRequestDto } from "../dtos/change-level.request.dto";
import { ChangeManagerRequestDto } from "../dtos/change-manager.request.dto";

@Injectable({
    providedIn: "root"
})
export class AdminService {
    constructor(private http: HttpClient) {
    }
    public getUsersApiCall(pageNumber: number = 1, pageSize: number = 10): Observable<UserDetailsResponseDto> {
        const url = `${baseUrl}/admin/users`;
        const params = new HttpParams()
            .set('pageNumber', pageNumber.toString())
            .set('pageSize', pageSize.toString());
        return this.http.get<UserDetailsResponseDto>(url, { params });
    }
    public deactivateUser(userId: number) {
        const url = `${baseUrl}/admin/users/${userId}/deactivate`;
        return this.http.put(url, {});
    }
    public reactivateUser(userId: number) {
        const url = `${baseUrl}/admin/users/${userId}/reactivate`;
        return this.http.put(url, {});
    }
    public changeDepartment(changeDepartmentRequestDto: ChangeDepartmentRequestDto) {
        const url = `${baseUrl}/admin/change-department`;
        return this.http.put(url, changeDepartmentRequestDto);
    }
    public changeLevel(changeLevelRequestDto: ChangeLevelRequestDto) {
        const url = `${baseUrl}/admin/change-level`;
        return this.http.put(url, changeLevelRequestDto);
    }
    public changeManager(changeManagerRequestDto: ChangeManagerRequestDto) {
        const url = `${baseUrl}/admin/change-manager`;
        return this.http.put(url, changeManagerRequestDto);
    }
    public getPotentialManagers(userId: number): Observable<UserDetails[]> {
        const url = `${baseUrl}/admin/users/${userId}/potential-managers`;
        return this.http.get<UserDetails[]>(url);
    }
}