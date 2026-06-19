export interface UploadDocumentRequestDto {
  title: string;
  description: string;
  targetDepartmentId: number;
  file: File;
}