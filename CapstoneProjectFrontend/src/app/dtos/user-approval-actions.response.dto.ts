import { AuditAction } from "../enums/audit-action.enum";

export interface UserApprovalActionResponseDto {
    id: number;
    performedByUserId: number;
    performedByUserName: string;
    performedByUserEmail: string;
    documentId: number;
    documentTitle: string;
    documentVersionId: number;
    documentVersionNumber: number;
    action: AuditAction;
    details: string | null;
    createdAt: string;
}