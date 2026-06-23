import { AuditAction } from "../enums/audit-action.enum";

export interface AuditLogResponseDto {
    id: number;
    performedByUserId: number;
    performedByUserName: string;
    performedByUserEmail: string;
    documentId: number | null;
    documentTitle: string | null;
    documentVersionId: number | null;
    documentVersionNumber: number | null;
    action: AuditAction;
    details: string | null;
    createdAt: string;
}