import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../services/admin.service';
import { UserDocumentResponseDto } from '../../dtos/user-document.response.dto';
import { DocumentVersionResponseDto } from '../../dtos/document-version.response.dto';
import { PaginatedResponse } from '../../helpers';
import { DocumentService } from '../../services/document.service';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { DocumentStatus } from '../../enums/document-status-filter.enum';

@Component({
  selector: 'app-admin-documents',
  imports: [FormsModule],
  templateUrl: './admin-documents.html',
  styleUrl: './admin-documents.css',
})
export class AdminDocuments implements OnInit, OnDestroy {
  currentPage = signal(1);
  pageSize = signal(10);
  mainSearchQuery = signal('');
  statusFilter = signal<DocumentStatus | undefined>(undefined);
  private searchTimeout: any;

  docsResponse = signal<PaginatedResponse<UserDocumentResponseDto>>({
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

  isModalOpen = signal(false);
  isModalLoading = signal(false);
  modalFileUrl = signal<SafeResourceUrl | null>(null);
  modalFileType = signal<'image' | 'pdf' | 'other' | null>(null);
  modalFileName = signal<string>('');
  modalError = signal<string | null>(null);
  rawFileUrl: string | null = null;

  constructor(
    private adminService: AdminService,
    private documentService: DocumentService,
    private sanitizer: DomSanitizer
  ) { }

  ngOnInit(): void {
    this.loadPage();
  }

  loadPage(): void {
    this.isLoading.set(true);
    this.expandedDocIds.set(new Set());
    this.adminService.getAllDocuments(this.currentPage(), this.pageSize(), this.mainSearchQuery(), this.statusFilter()).subscribe({
      next: (data) => {
        const normalized: PaginatedResponse<UserDocumentResponseDto> = {
          ...data,
          items: Array.isArray(data.items[0]) ? (data.items as any).flat() : data.items,
        };
        this.docsResponse.set(normalized);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load documents', err);
        this.isLoading.set(false);
      },
    });
  }

  onSearchInput(query: string): void {
    this.mainSearchQuery.set(query);
    this.currentPage.set(1);
    if (this.searchTimeout) {
      clearTimeout(this.searchTimeout);
    }
    this.searchTimeout = setTimeout(() => {
      this.loadPage();
    }, 500);
  }

  onStatusChange(status: string): void {
    const newStatus = status === 'ALL' || !status ? undefined : status as DocumentStatus;
    this.statusFilter.set(newStatus);
    this.currentPage.set(1);
    this.loadPage();
  }

  goToPage(page: number): void {
    const total = this.docsResponse().totalPages;
    if (page < 1 || page > total) return;
    this.currentPage.set(page);
    this.loadPage();
  }

  get pageNumbers(): number[] {
    const total = this.docsResponse().totalPages;
    const current = this.currentPage();
    const maxVisible = 7;
    if (total <= maxVisible) return Array.from({ length: total }, (_, i) => i + 1);
    let start = Math.max(1, current - 3);
    let end = Math.min(total, start + maxVisible - 1);
    if (end - start < maxVisible - 1) start = Math.max(1, end - maxVisible + 1);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
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
        console.error('Failed to load document preview', err);
        this.modalError.set('Could not load document preview. Please try again.');
        this.isModalLoading.set(false);
      },
    });
  }

  closeModal(): void {
    this.isModalOpen.set(false);
    this.cleanupObjectURL();
    this.modalFileUrl.set(null);
    this.modalFileType.set(null);
    this.modalFileName.set('');
    this.modalError.set(null);
  }

  cleanupObjectURL(): void {
    if (this.rawFileUrl) {
      URL.revokeObjectURL(this.rawFileUrl);
      this.rawFileUrl = null;
    }
  }

  ngOnDestroy(): void {
    this.cleanupObjectURL();
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
      case 'Withdrawn': return 'badge-withdrawn';
      default: return 'badge-default';
    }
  }

  statusLabel(status: string): string {
    if (status === 'PendingApproval') return 'Pending';
    return status;
  }

  actionClass(action: string): string {
    switch (action) {
      case 'Approved': return 'action-approved';
      case 'Rejected': return 'action-rejected';
      case 'ForwardedToDepartment': return 'action-forwarded';
      default: return '';
    }
  }

  getInitials(name: string): string {
    return name.split(' ').map(n => n.charAt(0)).join('').toUpperCase().slice(0, 2);
  }

  currentVersion(doc: UserDocumentResponseDto): DocumentVersionResponseDto | null {
    return doc.versions.find(v => v.isCurrentVersion) ?? doc.versions[0] ?? null;
  }
}
