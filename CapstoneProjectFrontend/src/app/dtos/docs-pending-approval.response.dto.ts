import { PaginatedResponse } from "../helpers";
import { UserDocumentResponseDto } from "./user-document.response.dto";

export type DocsPendingApprovalApiResponse = PaginatedResponse<UserDocumentResponseDto>;