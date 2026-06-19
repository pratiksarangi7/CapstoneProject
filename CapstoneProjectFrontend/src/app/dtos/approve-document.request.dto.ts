import { ApproveDocumentAction } from "../enums/approve-type.enum";

export interface ApproveDocumentRequestDto {
    approveDocumentAction: ApproveDocumentAction,
    targetUserId?: number
}