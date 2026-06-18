import { PaginatedResponse } from "../helpers";
import { UserDocumentResponseDto } from "./user-document.response.dto";

export type MyUploadsApiResponse = PaginatedResponse<UserDocumentResponseDto>;