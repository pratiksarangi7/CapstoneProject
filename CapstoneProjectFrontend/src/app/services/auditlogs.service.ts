import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { PaginatedResponse } from "../helpers";
import { AuditLogResponseDto } from "../dtos/audit-logs.response.dto";
import { Observable } from "rxjs";
import { AuditLogParams } from "../models/auditlogparams.model";
import { baseUrl } from "../../environment";

@Injectable({
    providedIn: "root"
})
export class AuditlogService {
    constructor(private http: HttpClient) {
    }
    getAuditLogs(params: AuditLogParams = {}): Observable<PaginatedResponse<AuditLogResponseDto>> {
        const url = `${baseUrl}/auditlog`;
        let httpParams = new HttpParams();

        if (params.action) {
            httpParams = httpParams.set('action', params.action);
        }
        if (params.userId !== undefined && params.userId !== null) {
            httpParams = httpParams.set('userId', params.userId.toString());
        }
        if (params.pageNumber) {
            httpParams = httpParams.set('pageNumber', params.pageNumber.toString());
        }
        if (params.pageSize) {
            httpParams = httpParams.set('pageSize', params.pageSize.toString());
        }

        return this.http.get<PaginatedResponse<AuditLogResponseDto>>(url, { params: httpParams });
    }
}