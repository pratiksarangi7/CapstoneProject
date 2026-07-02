import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { baseUrl } from "../../environment";
import { Observable } from "rxjs";
import { MyUploadsApiResponse } from "../dtos/my-uploads.response.dto";
import { DocsPendingApprovalApiResponse } from "../dtos/docs-pending-approval.response.dto";
import { ApproveDocumentRequestDto } from "../dtos/approve-document.request.dto";
import { RejectDocumentRequestDto } from "../dtos/reject-document.request.dto";
import { ReUploadDocumentRequestDto } from "../dtos/reupload-document.request.dto";
import { TransferDocumentRequestDto } from "../dtos/transfer-document.request.dto";
import { DocumentStatus } from "../enums/document-status-filter.enum";

@Injectable({
    providedIn: "root"
})

export class DocumentService {
    constructor(private http: HttpClient) {
    }
    public myUploadsApiCall(pageNumber: number = 1, pageSize: number = 10, search: string = "", documentStatus?: DocumentStatus): Observable<MyUploadsApiResponse> {
        const url = `${baseUrl}/document/my-uploads`;

        let params = new HttpParams()
            .set("pageNumber", pageNumber)
            .set("pageSize", pageSize)
            .set("search", search)
        if (documentStatus != undefined) {
            params = params.set("documentStatus", documentStatus);
        }
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
    public approveDocumentApiCall(documentId: number, action: ApproveDocumentRequestDto) {
        const url = `${baseUrl}/document/${documentId}/approve`;
        return this.http.put(url, action);
    }
    public rejectDocumentApiCall(documentId: number, action: RejectDocumentRequestDto): Observable<any> {
        const url = `${baseUrl}/document/${documentId}/reject`;
        return this.http.put(url, action);
    }
    public reuploadDocumentApiCall(documentId: number, file: File): Observable<any> {
        const url = `${baseUrl}/document/${documentId}/reupload`;
        const formData = new FormData();
        formData.append('file', file);
        return this.http.post(url, formData);
    }
    public transferDocumentApiCall(documentId: number, transferDocumentRequestDto: TransferDocumentRequestDto) {
        const url = `${baseUrl}/document/${documentId}/transfer`;
        return this.http.put(url, transferDocumentRequestDto);
    }

}