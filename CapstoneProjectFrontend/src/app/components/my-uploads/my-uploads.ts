import { Component, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { DocumentService } from '../../services/document.service';
import { MyUploadsApiResponse } from '../../dtos/my-uploads.response.dto';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { DocumentVersionResponseDto } from '../../dtos/document-version.response.dto';
import { UploadDoc } from '../upload-doc/upload-doc';

@Component({
  selector: 'app-my-uploads',
  imports: [UploadDoc],
  templateUrl: './my-uploads.html',
  styleUrl: './my-uploads.css',
})
export class MyUploads implements OnInit, OnDestroy {
  currentPage = signal(1);
  pageSize = signal(10);

  myDocuments = signal<MyUploadsApiResponse>({
    items: [],
    pageNumber: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  });
  isLoading = signal(false);

  expandedDocIds = signal<Set<number>>(new Set());

  // Modal Signals
  isModalOpen = signal(false);
  isModalLoading = signal(false);
  modalFileUrl = signal<SafeResourceUrl | null>(null);
  modalFileType = signal<'image' | 'pdf' | 'other' | null>(null);
  modalFileName = signal<string>('');
  modalError = signal<string | null>(null);

  rawFileUrl: string | null = null;

  // Upload Signals
  isUploadOpen = signal(false);

  // Re-upload Signals
  isReuploadOpen = signal(false);
  reuploadDocId = signal<number | null>(null);
  reuploadFile = signal<File | null>(null);
  reuploadError = signal<string | null>(null);
  reuploadLoading = signal(false);
  reuploadSuccess = signal(false);

  constructor(
    private documentService: DocumentService,
    private sanitizer: DomSanitizer
  ) { }

  ngOnInit(): void {
    this.loadPage();
  }

  loadPage(): void {
    this.isLoading.set(true);
    this.documentService
      .myUploadsApiCall(this.currentPage(), this.pageSize())
      .subscribe({
        next: (data) => {
          this.myDocuments.set(data);
          this.expandedDocIds.set(new Set());
          this.isLoading.set(false);
        },
        error: (error) => {
          console.error('Failed to load documents', error);
          this.isLoading.set(false);
        },
      });
  }

  goToPage(page: number): void {
    const total = this.myDocuments().totalPages;
    if (page < 1 || page > total) return;
    this.currentPage.set(page);
    this.loadPage();
  }

  toggleExpand(docId: number): void {
    const current = new Set(this.expandedDocIds());
    if (current.has(docId)) {
      current.delete(docId);
    } else {
      current.add(docId);
    }
    this.expandedDocIds.set(current);
  }

  isExpanded(docId: number): boolean {
    return this.expandedDocIds().has(docId);
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString('en-IN', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Approved': return 'badge-approved';
      case 'Rejected': return 'badge-rejected';
      case 'PendingApproval': return 'badge-pending';
      default: return 'badge-default';
    }
  }

  actionClass(action: string): string {
    switch (action) {
      case 'Approved': return 'action-approved';
      case 'Rejected': return 'action-rejected';
      case 'Forwarded': return 'action-forwarded';
      default: return '';
    }
  }

  get pageNumbers(): number[] {
    const total = this.myDocuments().totalPages;
    return Array.from({ length: total }, (_, i) => i + 1);
  }

  viewDocument(documentId: number, version: DocumentVersionResponseDto): void {
    this.modalFileName.set(version.originalFileName);
    this.isModalLoading.set(true);
    this.modalError.set(null);
    this.isModalOpen.set(true);

    this.cleanupObjectURL();

    this.documentService.viewDocumentApiCall(documentId, version.id).subscribe({
      next: (blob: Blob) => {
        const mimeType = version.mimeType || blob.type;
        
        if (mimeType.startsWith('image/')) {
          this.modalFileType.set('image');
        } else if (mimeType === 'application/pdf') {
          this.modalFileType.set('pdf');
        } else {
          this.modalFileType.set('other');
        }

        this.rawFileUrl = URL.createObjectURL(blob);
        this.modalFileUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.rawFileUrl));
        this.isModalLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load document version file', err);
        this.modalError.set('Could not load document preview. Please check your internet connection or try again later.');
        this.isModalLoading.set(false);
      }
    });
  }

  cleanupObjectURL(): void {
    if (this.rawFileUrl) {
      URL.revokeObjectURL(this.rawFileUrl);
      this.rawFileUrl = null;
    }
  }

  closeModal(): void {
    this.isModalOpen.set(false);
    this.cleanupObjectURL();
    this.modalFileUrl.set(null);
    this.modalFileType.set(null);
    this.modalFileName.set('');
    this.modalError.set(null);
  }

  openUploadModal(): void {
    this.isUploadOpen.set(true);
  }

  closeUploadModal(): void {
    this.isUploadOpen.set(false);
  }

  onUploadSuccess(): void {
    this.isUploadOpen.set(false);
    this.currentPage.set(1);
    this.loadPage();
  }

  openReuploadModal(documentId: number): void {
    this.reuploadDocId.set(documentId);
    this.reuploadFile.set(null);
    this.reuploadError.set(null);
    this.reuploadLoading.set(false);
    this.reuploadSuccess.set(false);
    this.isReuploadOpen.set(true);
  }

  closeReuploadModal(): void {
    this.isReuploadOpen.set(false);
    this.reuploadDocId.set(null);
    this.reuploadFile.set(null);
    this.reuploadError.set(null);
    this.reuploadLoading.set(false);
    this.reuploadSuccess.set(false);
  }

  onReuploadFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.reuploadError.set(null);
    if (!input.files || input.files.length === 0) {
      this.reuploadFile.set(null);
      return;
    }
    const file = input.files[0];
    const allowedTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/svg+xml', 'application/pdf'];
    if (!allowedTypes.includes(file.type)) {
      this.reuploadError.set('Only image files (JPEG, PNG, GIF, WebP, SVG) and PDF files are allowed.');
      this.reuploadFile.set(null);
      input.value = '';
      return;
    }
    const maxSize = 5 * 1024 * 1024; // 5 MB
    if (file.size > maxSize) {
      this.reuploadError.set('File size must not exceed 5 MB.');
      this.reuploadFile.set(null);
      input.value = '';
      return;
    }
    this.reuploadFile.set(file);
  }

  submitReupload(): void {
    const docId = this.reuploadDocId();
    const file = this.reuploadFile();
    if (!docId || !file) {
      this.reuploadError.set('Please select a valid file before submitting.');
      return;
    }
    this.reuploadLoading.set(true);
    this.reuploadError.set(null);
    this.documentService.reuploadDocumentApiCall(docId, file).subscribe({
      next: () => {
        this.reuploadLoading.set(false);
        this.reuploadSuccess.set(true);
        setTimeout(() => {
          this.closeReuploadModal();
          this.currentPage.set(1);
          this.loadPage();
        }, 1500);
      },
      error: (err) => {
        console.error('Re-upload failed', err);
        this.reuploadError.set('Re-upload failed. Please try again.');
        this.reuploadLoading.set(false);
      }
    });
  }

  ngOnDestroy(): void {
    this.cleanupObjectURL();
  }
}
