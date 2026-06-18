import { DocumentVersionResponseDto } from "./document-version.response.dto";

export interface UserDocumentResponseDto {
    id: number;
    title: string;
    description: string;
    documentStatus: string;
    createdAt: string;

    targetDepartmentId: number;
    targetDepartmentName: string;

    currentApproverUserId: number | null;
    currentApproverName: string | null;
    currentApproverEmail: string | null;
    currentApproverDepartmentName: string | null;

    createdByUserId: number;
    createdByUserName: string;
    createdByUserEmail: string;

    versions: DocumentVersionResponseDto[];
}