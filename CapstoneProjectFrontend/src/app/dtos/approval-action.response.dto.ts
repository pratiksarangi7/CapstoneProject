import { ApprovalActionType } from "../enums/approval-action-type.enum";

export interface ApprovalActionResponseDto {
  id: number;
  approverUserId: number;
  approverUserName: string;
  action: ApprovalActionType | string; 
  comments: string | null;             
  createdAt: string;                   
}