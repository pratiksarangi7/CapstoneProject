import { AuditAction } from "../enums/audit-action.enum";

export interface AuditLogParams {
    action?: AuditAction; 
    userId?: number;
    pageNumber?: number;
    pageSize?: number;
}