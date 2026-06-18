import { HttpClient, HttpHeaders, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { baseUrl } from "../../environment";
import { Observable } from "rxjs";
import { MyUploadsApiResponse } from "../dtos/my-uploads.response.dto";

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
    public viewDocumentApiCall(documentId: number, versionId: number) {
        const url = `${baseUrl}/document/${documentId}`;
        const params = new HttpParams().set('versionId', versionId);
        return this.http.get(url, { params });
    }
}