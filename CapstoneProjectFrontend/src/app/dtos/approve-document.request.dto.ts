import { ApproveDocumentAction } from "../enums/approve-type.enum";

export interface ApproveDocumentRequestDto {
    action: ApproveDocumentAction,
    targetUserId?: number
}