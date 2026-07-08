import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { baseUrl } from "../../environment";
import { Observable } from "rxjs";
import { OtherDepartmentUsersResponseDto } from "../dtos/other-departments-users.response.dto";
import { UserProfileResponseDto } from "../dtos/user-profile.response.dto";
import { ChangePasswordRequestDto } from "../dtos/change-password.request.dto";
import { UserApprovalActionResponseDto } from "../dtos/user-approval-actions.response.dto";
import { PaginatedResponse } from "../helpers";

@Injectable({ providedIn: "root" })

export class UserService {
    constructor(private http: HttpClient) {
    }
    public getOtherDeptUsers(search: string = ""): Observable<OtherDepartmentUsersResponseDto[]> {
        const url = `${baseUrl}/User/external`;
        const params = new HttpParams().set('search', search);
        return this.http.get<OtherDepartmentUsersResponseDto[]>(url, { params });
    }
    public getProfileDetails(): Observable<UserProfileResponseDto> {
        const url = `${baseUrl}/User/me`;
        return this.http.get<UserProfileResponseDto>(url);
    }
    public changePassword(body: ChangePasswordRequestDto) {
        const url = `${baseUrl}/User/me/change-password`;
        return this.http.put(url, body);
    }
    public getMyPastApprovalActions(pageNumber: number = 1, pageSize: number = 10): Observable<PaginatedResponse<UserApprovalActionResponseDto>> {
        const url = `${baseUrl}/user/me/actions`;
        const params = new HttpParams()
            .set('pageNumber', pageNumber.toString())
            .set('pageSize', pageSize.toString());
        return this.http.get<PaginatedResponse<UserApprovalActionResponseDto>>(url, { params });
    }


}
