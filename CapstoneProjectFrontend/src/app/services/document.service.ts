import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { baseUrl } from "../../environment";
import { Observable } from "rxjs";
import { MyUploadsApiResponse } from "../dtos/my-uploads.response.dto";
import { DocsPendingApprovalApiResponse } from "../dtos/docs-pending-approval.response.dto";

@Injectable({
    providedIn: "root"
})

export class DocumentService {
    constructor(private http: HttpClient) {
    }
    public myUploadsApiCall(pageNumber: number = 1, pageSize: number = 10): Observable<MyUploadsApiResponse> {
        const url = `${baseUrl}/document/my-uploads`;

        const params = new HttpParams()
            .set('pageNumber', pageNumber.toString())
            .set('pageSize', pageSize.toString());

        return this.http.get<MyUploadsApiResponse>(url, { params });
    }
    public viewDocumentApiCall(documentId: number, versionId: number): Observable<Blob> {
        const url = `${baseUrl}/document/${documentId}/file`;
        const params = new HttpParams().set('versionId', versionId.toString());
        return this.http.get(url, { params, responseType: 'blob' });
    }
    public uploadDocumentApiCall(formData: FormData): Observable<any> {
        const url = `${baseUrl}/document/upload`;
        return this.http.post(url, formData);
    }
    public getDocsPendingApproval(pageNumber: number = 1, pageSize: number = 10) {
        const url = `${baseUrl}/document/pending-approvals`;
        const params = new HttpParams()
            .set('pageNumber', pageNumber.toString())
            .set('pageSize', pageSize.toString());
        return this.http.get<DocsPendingApprovalApiResponse>(url, { params })
    }
    public approveDocumentApiCall(documentId: number){
        const url = `${baseUrl}/document/${documentId}/approve`;
    }
    
}