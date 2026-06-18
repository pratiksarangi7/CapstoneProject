import { Component, OnInit, signal, computed } from '@angular/core';
import { DocumentService } from '../../services/document.service';
import { MyUploadsApiResponse } from '../../dtos/my-uploads.response.dto';

@Component({
  selector: 'app-my-uploads',
  imports: [],
  templateUrl: './my-uploads.html',
  styleUrl: './my-uploads.css',
})
export class MyUploads implements OnInit {
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

  constructor(private documentService: DocumentService) { }

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
}
