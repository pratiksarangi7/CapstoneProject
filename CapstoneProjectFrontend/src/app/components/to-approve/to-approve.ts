import { Component, OnInit, OnDestroy, signal } from '@angular/core';
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
  imports: [FormsModule],
  templateUrl: './to-approve.html',
  styleUrl: './to-approve.css',
})
export class ToApprove implements OnInit, OnDestroy {
  currentPage = signal(1);
  pageSize = signal(10);
  isLoading = signal(false);

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

  // Action modal state
  isActionModalOpen = signal(false);
  selectedDocument = signal<UserDocumentResponseDto | null>(null);
  selectedVersion = signal<DocumentVersionResponseDto | null>(null);
  actionComments = '';
  isApproveDropdownOpen = signal(false);

  // Pending action (chosen but awaiting target user selection for transfer actions)
  pendingActionType = signal<PendingActionType>(null);

  // User search state
  isUserSearchModalOpen = signal(false);
  allOtherDeptUsers = signal<OtherDepartmentUsersResponseDto[]>([]);
  isUsersLoading = signal(false);
  userSearchQuery = '';
  selectedTargetUser = signal<OtherDepartmentUsersResponseDto | null>(null);

  // Preview modal state
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
      .getDocsPendingApproval(this.currentPage(), this.pageSize())
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

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  get pageNumbers(): number[] {
    const total = this.pendingDocuments().totalPages;
    return Array.from({ length: total }, (_, i) => i + 1);
  }

  // ── Action Modal ─────────────────────────────────────────────────────────
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

  // ── User Search Modal ─────────────────────────────────────────────────────
  openUserSearchModal(actionType: PendingActionType): void {
    this.pendingActionType.set(actionType);
    this.isApproveDropdownOpen.set(false);
    this.userSearchQuery = '';
    this.selectedTargetUser.set(null);
    this.isUsersLoading.set(true);
    this.isUserSearchModalOpen.set(true);

    this.userService.getOtherDeptUsers().subscribe({
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

  closeUserSearchModal(): void {
    this.isUserSearchModalOpen.set(false);
    this.pendingActionType.set(null);
    this.userSearchQuery = '';
    this.selectedTargetUser.set(null);
  }

  get filteredUsers(): OtherDepartmentUsersResponseDto[] {
    const q = this.userSearchQuery.trim().toLowerCase();
    if (!q) return this.allOtherDeptUsers();
    return this.allOtherDeptUsers().filter(u =>
      u.name.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q) ||
      u.departmentName.toLowerCase().includes(q)
    );
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

  // ── Execute Actions ───────────────────────────────────────────────────────
  /** Called for non-transfer approve actions (Approve Completely, Approve & Forward, Reject) */
  executeAction(actionType: string): void {
    if (!this.selectedDocument() || !this.selectedVersion()) return;

    // For transfer-requiring actions, open user search first
    if (actionType === 'Approve & Transfer' || actionType === 'Transfer') {
      this.openUserSearchModal(actionType as PendingActionType);
      return;
    }

    const docId = this.selectedDocument()!.id;

    if (actionType === 'Approve Completely') {
      const requestDto: ApproveDocumentRequestDto = { approveDocumentAction: ApproveDocumentAction.ApproveEntirely };
      this.callApproveApi(docId, requestDto, actionType);
    } else if (actionType === 'Approve & Forward') {
      const requestDto: ApproveDocumentRequestDto = { approveDocumentAction: ApproveDocumentAction.ApproveAndForward };
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
          alert('Failed to perform rejection. Please try again.');
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
        approveDocumentAction: ApproveDocumentAction.ApproveAndTransfer,
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
          alert('Failed to transfer document. Please try again.');
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
        alert(`Failed to perform action. Please try again.`);
        this.isLoading.set(false);
      }
    });
  }

  // ── Preview Modal ─────────────────────────────────────────────────────────
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
  }
}
