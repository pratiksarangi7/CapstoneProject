import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { baseUrl } from "../../environment";
import { UserDetailsResponseDto, UserDetails } from "../dtos/user-details-response.dto";
import { Observable } from "rxjs";
import { ChangeDepartmentRequestDto } from "../dtos/change-department.request.dto";
import { ChangeLevelRequestDto } from "../dtos/change-level.request.dto";
import { ChangeManagerRequestDto } from "../dtos/change-manager.request.dto";
import { DepartmentResponseDto } from "../dtos/department.response.dto";
import { AddDepartmentRequestDto } from "../dtos/add-department.request.dto";
import { UserDocumentResponseDto } from "../dtos/user-document.response.dto";
import { PaginatedResponse } from "../helpers";
import { ReassignDocumentsRequestDto } from "../dtos/reassign-documents.request.dto";
import { RejectAllDocs } from "../dtos/reject-all-docs.request.dto";

@Injectable({
    providedIn: "root"
})
export class AdminService {
    constructor(private http: HttpClient) {
    }
    public getUsersApiCall(pageNumber: number = 1, pageSize: number = 10, search: string = ""): Observable<UserDetailsResponseDto> {
        const url = `${baseUrl}/admin/users`;
        const params = new HttpParams()
            .set('pageNumber', pageNumber.toString())
            .set('pageSize', pageSize.toString())
            .set('search', search);
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
    public getPotentialManagers(userId: number, search: string=""): Observable<UserDetails[]> {
        const url = `${baseUrl}/admin/users/${userId}/potential-managers`;
        const params=new HttpParams().set("search", search);
        return this.http.get<UserDetails[]>(url, {params});
    }
    public getDepartments(): Observable<DepartmentResponseDto[]> {
        const url = `${baseUrl}/Admin/departments`;
        return this.http.get<DepartmentResponseDto[]>(url);
    }
    public createNewDepartments(body: AddDepartmentRequestDto) {
        const url = `${baseUrl}/admin/department`;
        return this.http.post(url, body);
    }
    public getAllDocuments(pageNumber: number = 1, pageSize: number = 10): Observable<PaginatedResponse<UserDocumentResponseDto[]>> {
        const params = new HttpParams();
        params.set("pageNumber", pageNumber);
        params.set("pageSize", pageSize);
        const url = `${baseUrl}/admin/documents/all`;
        return this.http.get<PaginatedResponse<UserDocumentResponseDto[]>>(url, { params });
    }
    public reassignDocuments(body: ReassignDocumentsRequestDto) {
        const url = `${baseUrl}/admin/reassign-documents`;
        return this.http.put(url, body);
    }
    public rejectAllDocs(userId: number, body: RejectAllDocs) {
        const url = `${baseUrl}/admin/users/${userId}/reject-pending-documents`;
        return this.http.put(url, body);
    }
}