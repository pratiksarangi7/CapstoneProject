export interface BulkUploadRowResult {
    rowNumber: number;
    success: boolean;
    email: string | null;
    name: string | null;
    error: string | null;
}

export interface BulkUploadResponseDto {
    totalRows: number;
    successCount: number;
    failureCount: number;
    results: BulkUploadRowResult[];
}
