import { Component, OnInit, signal, output } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { form, required, FormField } from '@angular/forms/signals';
import { Department } from '../../models/department.model';
import { DepartmentService } from '../../services/department.service';
import { DocumentService } from '../../services/document.service';

@Component({
  selector: 'app-upload-doc',
  imports: [FormsModule, ReactiveFormsModule, FormField],
  templateUrl: './upload-doc.html',
  styleUrl: './upload-doc.css',
})
export class UploadDoc implements OnInit {
  // Outputs
  close = output<void>();
  uploaded = output<void>();

  // State signals
  departments = signal<Department[]>([]);
  progress = signal(false);
  fileError = signal<string | null>(null);

  // Form signals model
  uploadModel = signal({
    title: '',
    description: '',
    targetDepartmentId: '',
    file: null as File | null
  });

  // Validation rules using Form Signals
  uploadForm = form(this.uploadModel, (path) => {
    required(path.title, { message: "Title is required" });
    required(path.description, { message: "Description is required" });
    required(path.targetDepartmentId, { message: "Target department is required" });
    required(path.file, { message: "File is required" });
  });

  constructor(
    private departmentService: DepartmentService,
    private documentService: DocumentService
  ) { }

  ngOnInit(): void {
    this.departmentService.GetAllDepartments().subscribe({
      next: (data) => {
        this.departments.set(data);
      },
      error: (error) => {
        console.error('Failed to load departments', error);
      }
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];

      // Validate file size (max 5 MB)
      const maxSize = 5 * 1024 * 1024;
      if (file.size > maxSize) {
        this.fileError.set("File size exceeds 5 MB limit.");
        this.uploadModel.update(m => ({ ...m, file: null }));
        return;
      }

      // Validate file type (image or pdf)
      const allowedTypes = ['application/pdf', 'image/jpeg', 'image/png', 'image/gif', 'image/webp'];
      if (!allowedTypes.includes(file.type)) {
        this.fileError.set("Only PDF and image files (PNG, JPG, GIF, WEBP) are allowed.");
        this.uploadModel.update(m => ({ ...m, file: null }));
        return;
      }

      // Valid file
      this.fileError.set(null);
      this.uploadModel.update(m => ({ ...m, file: file }));
    } else {
      this.uploadModel.update(m => ({ ...m, file: null }));
    }
  }

  handleUploadClick(): void {
    if (this.uploadForm().invalid()) {
      alert("Please fix the errors in the form before submitting.");
      return;
    }

    const formValues = this.uploadModel();
    if (!formValues.file) {
      alert("Please upload a valid file first.");
      return;
    }

    const formData = new FormData();
    formData.append('title', formValues.title);
    formData.append('description', formValues.description);
    formData.append('targetDepartmentId', formValues.targetDepartmentId);
    formData.append('file', formValues.file, formValues.file.name);

    this.progress.set(true);
    this.documentService.uploadDocumentApiCall(formData).subscribe({
      next: (response) => {
        console.log("Upload successful", response);
        alert("Document uploaded successfully!");
        this.progress.set(false);
        this.uploaded.emit();
        this.close.emit();
      },
      error: (error) => {
        console.error("Upload failed", error);
        alert("Upload failed. Please try again.");
        this.progress.set(false);
      }
    });
  }

  handleCloseClick(): void {
    this.close.emit();
  }
}
