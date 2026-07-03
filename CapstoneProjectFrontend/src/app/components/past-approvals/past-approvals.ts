import { Component, OnInit, signal } from '@angular/core';
import { UserService } from '../../services/user.service';
import { UserApprovalActionResponseDto } from '../../dtos/user-approval-actions.response.dto';
import { AuditAction } from '../../enums/audit-action.enum';
import { PaginatedResponse } from '../../helpers';

@Component({
  selector: 'app-past-approvals',
  imports: [],
  templateUrl: './past-approvals.html',
  styleUrl: './past-approvals.css',
})
export class PastApprovals implements OnInit {
  currentPage = signal(1);
  pageSize = signal(10);
  isLoading = signal(false);

  pastActionsResponse = signal<PaginatedResponse<UserApprovalActionResponseDto>>({
    items: [],
    pageNumber: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  });

  constructor(private userService: UserService) { }

  ngOnInit(): void {
    this.loadPastActions();
  }

  loadPastActions(): void {
    this.isLoading.set(true);
    this.userService.getMyPastApprovalActions(this.currentPage(), this.pageSize()).subscribe({
      next: (data) => {
        this.pastActionsResponse.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load past approval actions', err);
        this.isLoading.set(false);
      },
    });
  }

  goToPage(page: number): void {
    const total = this.pastActionsResponse().totalPages;
    if (page < 1 || page > total) return;
    this.currentPage.set(page);
    this.loadPastActions();
  }

  get pageNumbers(): number[] {
    const total = this.pastActionsResponse().totalPages;
    return Array.from({ length: total }, (_, i) => i + 1);
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-IN', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatAction(action: AuditAction): string {
    return action.replace(/([A-Z])/g, ' $1').trim();
  }

  getActionBadgeClass(action: AuditAction): string {
    switch (action) {
      case AuditAction.DocumentApproved:
        return 'badge-approved';
      case AuditAction.DocumentRejected:
        return 'badge-rejected';
      case AuditAction.DocumentForwarded:
        return 'badge-forwarded';
      default:
        return 'badge-default';
    }
  }
}

