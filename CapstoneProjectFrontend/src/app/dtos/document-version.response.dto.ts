import { ApprovalActionResponseDto } from "./approval-action.response.dto";

export interface DocumentVersionResponseDto {
    id: number;
    documentId: number;
    versionNumber: number;
    originalFileName: string;
    storedFileName: string;
    fileSize: number;
    mimeType: string;
    isCurrentVersion: boolean;
    uploadedByUserId: number;
    uploadedByUserName: string;
    createdAt: string;
    approvalAction: ApprovalActionResponseDto | null;
}