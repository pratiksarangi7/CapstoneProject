import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DocumentService } from '../../services/document.service';
import { UserService } from '../../services/user.service';
import { DocsPendingApprovalApiResponse } from '../../dtos/docs-pending-approval.response.dto';
import { UserDocumentResponseDto } from '../../dtos/user-document.response.dto';
import { DocumentVersionResponseDto } from '../../dtos/document-version.response.dto';
import { OtherDepartmentUsersResponseDto } from '../../dtos/other-departments-users.response.dto';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ApproveDocumentAction } from '../../enums/approve-type.enum';
import { ApproveDocumentRequestDto } from '../../dtos/approve-document.request.dto';
import { RejectDocumentRequestDto } from '../../dtos/reject-document.request.dto';
import { TransferDocumentRequestDto } from '../../dtos/transfer-document.request.dto';

type PendingActionType = 'Approve Completely' | 'Approve & Forward' | 'Approve & Transfer' | 'Transfer' | 'Reject' | null;

@Component({
  selector: 'app-to-approve',
  imports: [FormsModule, DatePipe],
  templateUrl: './to-approve.html',
  styleUrl: './to-approve.css',
})
export class ToApprove implements OnInit, OnDestroy {
  currentPage = signal(1);
  pageSize = signal(10);
  isLoading = signal(false);
  searchQuery = signal<string>('');
  searchTimeout: any;
  userSearchTimeout: any;

  pendingDocuments = signal<DocsPendingApprovalApiResponse>({
    items: [],
    pageNumber: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  });

  expandedDocIds = signal<Set<number>>(new Set());

  isActionModalOpen = signal(false);
  selectedDocument = signal<UserDocumentResponseDto | null>(null);
  selectedVersion = signal<DocumentVersionResponseDto | null>(null);
  actionComments = '';
  isApproveDropdownOpen = signal(false);

  pendingActionType = signal<PendingActionType>(null);

  isUserSearchModalOpen = signal(false);
  allOtherDeptUsers = signal<OtherDepartmentUsersResponseDto[]>([]);
  isUsersLoading = signal(false);
  userSearchQuery = '';
  selectedTargetUser = signal<OtherDepartmentUsersResponseDto | null>(null);

  isPreviewOpen = signal(false);
  isPreviewLoading = signal(false);
  previewFileUrl = signal<SafeResourceUrl | null>(null);
  previewFileType = signal<'image' | 'pdf' | 'other' | null>(null);
  previewFileName = signal<string>('');
  previewError = signal<string | null>(null);

  private rawFileUrl: string | null = null;

  constructor(
    private documentService: DocumentService,
    private userService: UserService,
    private sanitizer: DomSanitizer
  ) { }

  ngOnInit(): void {
    this.loadPage();
  }

  loadPage(): void {
    this.isLoading.set(true);
    this.documentService
      .getDocsPendingApproval(this.currentPage(), this.pageSize(), this.searchQuery())
      .subscribe({
        next: (data) => {
          this.pendingDocuments.set(data);
          this.expandedDocIds.set(new Set());
          this.isLoading.set(false);
        },
        error: (error) => {
          console.error('Failed to load pending documents', error);
          this.isLoading.set(false);
        },
      });
  }

  onSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchQuery.set(input.value);
    this.currentPage.set(1);
    if (this.searchTimeout) {
      clearTimeout(this.searchTimeout);
    }
    this.searchTimeout = setTimeout(() => {
      this.loadPage();
    }, 500);
  }

  goToPage(page: number): void {
    const total = this.pendingDocuments().totalPages;
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

  isExpiringSoon(dateStr: string | undefined): boolean {
    if (!dateStr) return false;
    const msInDay = 24 * 60 * 60 * 1000;
    const daysLeft = (new Date(dateStr).getTime() - Date.now()) / msInDay;
    return daysLeft >= 0 && daysLeft < 7;
  }

  daysUntilExpiry(dateStr: string): number {
    const msInDay = 24 * 60 * 60 * 1000;
    return Math.ceil((new Date(dateStr).getTime() - Date.now()) / msInDay);
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  get pageNumbers(): number[] {
    const total = this.pendingDocuments().totalPages;
    return Array.from({ length: total }, (_, i) => i + 1);
  }

  openActionModal(doc: UserDocumentResponseDto, ver: DocumentVersionResponseDto): void {
    this.selectedDocument.set(doc);
    this.selectedVersion.set(ver);
    this.actionComments = '';
    this.isApproveDropdownOpen.set(false);
    this.pendingActionType.set(null);
    this.selectedTargetUser.set(null);
    this.isActionModalOpen.set(true);
  }

  closeActionModal(): void {
    this.isActionModalOpen.set(false);
    this.selectedDocument.set(null);
    this.selectedVersion.set(null);
    this.actionComments = '';
    this.isApproveDropdownOpen.set(false);
    this.pendingActionType.set(null);
    this.selectedTargetUser.set(null);
  }

  toggleApproveDropdown(): void {
    this.isApproveDropdownOpen.update(v => !v);
  }

  openUserSearchModal(actionType: PendingActionType): void {
    this.pendingActionType.set(actionType);
    this.isApproveDropdownOpen.set(false);
    this.userSearchQuery = '';
    this.selectedTargetUser.set(null);
    this.allOtherDeptUsers.set([]);
    this.isUsersLoading.set(true);
    this.isUserSearchModalOpen.set(true);
    this.loadOtherDeptUsers();
  }

  closeUserSearchModal(): void {
    this.isUserSearchModalOpen.set(false);
    this.pendingActionType.set(null);
    this.userSearchQuery = '';
    this.selectedTargetUser.set(null);
    if (this.userSearchTimeout) {
      clearTimeout(this.userSearchTimeout);
    }
  }

  onUserSearchChange(): void {
    if (this.userSearchTimeout) {
      clearTimeout(this.userSearchTimeout);
    }
    this.userSearchTimeout = setTimeout(() => {
      this.loadOtherDeptUsers();
    }, 500);
  }

  clearUserSearch(): void {
    this.userSearchQuery = '';
    if (this.userSearchTimeout) {
      clearTimeout(this.userSearchTimeout);
    }
    this.loadOtherDeptUsers();
  }

  loadOtherDeptUsers(): void {
    const q = this.userSearchQuery.trim();
    this.isUsersLoading.set(true);
    this.userService.getOtherDeptUsers(q).subscribe({
      next: (users) => {
        this.allOtherDeptUsers.set(users);
        this.isUsersLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load other department users', err);
        this.isUsersLoading.set(false);
      }
    });
  }

  get filteredUsers(): OtherDepartmentUsersResponseDto[] {
    return this.allOtherDeptUsers();
  }

  selectTargetUser(user: OtherDepartmentUsersResponseDto): void {
    this.selectedTargetUser.set(user);
  }

  confirmTransferAction(): void {
    const target = this.selectedTargetUser();
    const action = this.pendingActionType();
    if (!target || !action) return;
    this.isUserSearchModalOpen.set(false);
    this.executeTransferAction(action, target.id);
  }

  executeAction(actionType: string): void {
    if (!this.selectedDocument() || !this.selectedVersion()) return;

    if (actionType === 'Approve & Transfer' || actionType === 'Transfer') {
      this.openUserSearchModal(actionType as PendingActionType);
      return;
    }

    const docId = this.selectedDocument()!.id;

    if (actionType === 'Approve Completely') {
      const requestDto: ApproveDocumentRequestDto = { action: ApproveDocumentAction.ApproveEntirely };
      this.callApproveApi(docId, requestDto, actionType);
    } else if (actionType === 'Approve & Forward') {
      const requestDto: ApproveDocumentRequestDto = { action: ApproveDocumentAction.ApproveAndForward };
      this.callApproveApi(docId, requestDto, actionType);
    } else if (actionType === 'Reject') {
      if (!this.actionComments.trim()) {
        alert('A reason must be provided to reject this document.');
        return;
      }
      const requestDto: RejectDocumentRequestDto = { reason: this.actionComments.trim() };
      this.isLoading.set(true);
      this.documentService.rejectDocumentApiCall(docId, requestDto).subscribe({
        next: () => {
          alert('Document Rejected Successfully');
          this.closeActionModal();
          this.loadPage();
        },
        error: (error) => {
          console.error('Reject failed', error);
          alert(error.error.message);
          this.isLoading.set(false);
        }
      });
    }
  }

  /** Called after user selects a target — handles both Approve & Transfer and Transfer */
  private executeTransferAction(actionType: PendingActionType, targetUserId: number): void {
    if (!this.selectedDocument()) return;
    const docId = this.selectedDocument()!.id;

    if (actionType === 'Approve & Transfer') {
      const requestDto: ApproveDocumentRequestDto = {
        action: ApproveDocumentAction.ApproveAndTransfer,
        targetUserId
      };
      this.callApproveApi(docId, requestDto, 'Approve & Transfer');
    } else if (actionType === 'Transfer') {
      const requestDto: TransferDocumentRequestDto = {
        targetUserId,
        comments: this.actionComments.trim() || undefined
      };
      this.isLoading.set(true);
      this.documentService.transferDocumentApiCall(docId, requestDto).subscribe({
        next: () => {
          alert('Document Transferred Successfully');
          this.closeActionModal();
          this.loadPage();
        },
        error: (error) => {
          console.error('Transfer failed', error);
          alert(error.error.message);
          this.isLoading.set(false);
        }
      });
    }
  }

  private callApproveApi(docId: number, requestDto: ApproveDocumentRequestDto, label: string): void {
    this.isLoading.set(true);
    this.documentService.approveDocumentApiCall(docId, requestDto).subscribe({
      next: () => {
        alert(`${label} Successful`);
        this.closeActionModal();
        this.loadPage();
      },
      error: (error) => {
        console.error(`${label} failed`, error);
        alert(error.error.message);
        this.isLoading.set(false);
      }
    });
  }

  viewDocument(documentId: number, version: DocumentVersionResponseDto): void {
    this.previewFileName.set(version.originalFileName);
    this.isPreviewLoading.set(true);
    this.previewError.set(null);
    this.isPreviewOpen.set(true);

    this.cleanupObjectURL();

    this.documentService.viewDocumentApiCall(documentId, version.id).subscribe({
      next: (blob: Blob) => {
        const mimeType = version.mimeType || blob.type;

        if (mimeType.startsWith('image/')) {
          this.previewFileType.set('image');
        } else if (mimeType === 'application/pdf') {
          this.previewFileType.set('pdf');
        } else {
          this.previewFileType.set('other');
        }

        this.rawFileUrl = URL.createObjectURL(blob);
        this.previewFileUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.rawFileUrl));
        this.isPreviewLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load document version file', err);
        this.previewError.set('Could not load document preview. Please check your internet connection or try again later.');
        this.isPreviewLoading.set(false);
      }
    });
  }

  cleanupObjectURL(): void {
    if (this.rawFileUrl) {
      URL.revokeObjectURL(this.rawFileUrl);
      this.rawFileUrl = null;
    }
  }

  closePreviewModal(): void {
    this.isPreviewOpen.set(false);
    this.cleanupObjectURL();
    this.previewFileUrl.set(null);
    this.previewFileType.set(null);
    this.previewFileName.set('');
    this.previewError.set(null);
  }

  ngOnDestroy(): void {
    this.cleanupObjectURL();
    if (this.searchTimeout) {
      clearTimeout(this.searchTimeout);
    }
    if (this.userSearchTimeout) {
      clearTimeout(this.userSearchTimeout);
    }
  }

  formatUploaderName(name: string | null): string {
    if (!name) return '';
    if (name.length > 15) {
      return name.slice(0, 15) + '.....';
    }
    return name;
  }
}
