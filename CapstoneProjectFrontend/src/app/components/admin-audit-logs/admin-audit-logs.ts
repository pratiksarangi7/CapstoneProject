import { Component, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuditlogService } from '../../services/auditlogs.service';
import { AdminService } from '../../services/admin.service';
import { AuditLogResponseDto } from '../../dtos/audit-logs.response.dto';
import { UserDetails } from '../../dtos/user-details-response.dto';
import { AuditLogParams } from '../../models/auditlogparams.model';
import { AuditAction } from '../../enums/audit-action.enum';
import { PaginatedResponse } from '../../helpers';

@Component({
  selector: 'app-admin-audit-logs',
  imports: [FormsModule],
  templateUrl: './admin-audit-logs.html',
  styleUrl: './admin-audit-logs.css',
})
export class AdminAuditLogs implements OnInit {
  currentPage = signal(1);
  pageSize = signal(15);

  isLoading = signal(false);
  isUsersLoading = signal(false);

  logsResponse = signal<PaginatedResponse<AuditLogResponseDto>>({
    items: [],
    pageNumber: 1,
    pageSize: 15,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  });

  // ── Filter: Action ────────────────────────────────────────────────────────────
  selectedAction = signal<AuditAction | null>(null);
  readonly auditActions = Object.values(AuditAction);

  // ── Filter: User typeahead ────────────────────────────────────────────────────
  allUsers = signal<UserDetails[]>([]);
  userSearchQuery = signal('');
  selectedUser = signal<UserDetails | null>(null);
  isUserDropdownOpen = signal(false);
  expandedLogId = signal<number | null>(null);

  filteredUsers = computed(() => {
    const q = this.userSearchQuery().trim().toLowerCase();
    if (!q) return [];
    return this.allUsers().filter(
      (u) =>
        u.name.toLowerCase().includes(q) || u.email.toLowerCase().includes(q)
    );
  });

  constructor(
    private auditlogService: AuditlogService,
    private adminService: AdminService
  ) {}

  ngOnInit(): void {
    this.loadUsers();
    this.loadLogs();
  }

  loadUsers(): void {
    this.isUsersLoading.set(true);
    this.adminService.getUsersApiCall(1, 200).subscribe({
      next: (data) => {
        this.allUsers.set(data.items);
        this.isUsersLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load users', err);
        this.isUsersLoading.set(false);
      },
    });
  }

  loadLogs(): void {
    this.isLoading.set(true);
    const params: AuditLogParams = {
      pageNumber: this.currentPage(),
      pageSize: this.pageSize(),
    };
    if (this.selectedAction()) {
      params.action = this.selectedAction()!;
    }
    if (this.selectedUser()) {
      params.userId = this.selectedUser()!.id;
    }
    this.auditlogService.getAuditLogs(params).subscribe({
      next: (data) => {
        this.logsResponse.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load audit logs', err);
        this.isLoading.set(false);
      },
    });
  }

  toggleLogExpand(logId: number): void {
    if (this.expandedLogId() === logId) {
      this.expandedLogId.set(null);
    } else {
      this.expandedLogId.set(logId);
    }
  }

  goToPage(page: number): void {
    const total = this.logsResponse().totalPages;
    if (page < 1 || page > total) return;
    this.currentPage.set(page);
    this.loadLogs();
  }

  get pageNumbers(): number[] {
    const total = this.logsResponse().totalPages;
    const current = this.currentPage();
    const maxVisible = 7;
    if (total <= maxVisible) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }
    let start = Math.max(1, current - 3);
    let end = Math.min(total, start + maxVisible - 1);
    if (end - start < maxVisible - 1) {
      start = Math.max(1, end - maxVisible + 1);
    }
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  }

  onActionChange(): void {
    this.currentPage.set(1);
    this.loadLogs();
  }

  clearActionFilter(): void {
    this.selectedAction.set(null);
    this.currentPage.set(1);
    this.loadLogs();
  }

  onUserSearchInput(): void {
    const q = this.userSearchQuery().trim();
    this.isUserDropdownOpen.set(q.length > 0);
    if (!q && this.selectedUser()) {
      this.clearUserFilter();
    }
  }

  selectUser(user: UserDetails): void {
    this.selectedUser.set(user);
    this.userSearchQuery.set(user.name);
    this.isUserDropdownOpen.set(false);
    this.currentPage.set(1);
    this.loadLogs();
  }

  clearUserFilter(): void {
    this.selectedUser.set(null);
    this.userSearchQuery.set('');
    this.isUserDropdownOpen.set(false);
    this.currentPage.set(1);
    this.loadLogs();
  }

  resetFilters(): void {
    this.selectedAction.set(null);
    this.selectedUser.set(null);
    this.userSearchQuery.set('');
    this.isUserDropdownOpen.set(false);
    this.currentPage.set(1);
    this.loadLogs();
  }

  get hasActiveFilters(): boolean {
    return this.selectedAction() !== null || this.selectedUser() !== null;
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-IN', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    });
  }

  formatTime(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleTimeString('en-IN', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: true,
    });
  }

  formatAction(action: AuditAction): string {
    return action.replace(/([A-Z])/g, ' $1').trim();
  }

  getActionCategory(action: AuditAction): 'user' | 'document' | 'system' {
    const userActions: AuditAction[] = [
      AuditAction.UserRegistered,
      AuditAction.UserLoggedIn,
      AuditAction.UserDeactivated,
      AuditAction.UserReactivated,
      AuditAction.PasswordChanged,
    ];
    const docActions: AuditAction[] = [
      AuditAction.DocumentUploaded,
      AuditAction.NewVersionUploaded,
      AuditAction.DocumentApproved,
      AuditAction.DocumentRejected,
      AuditAction.DocumentDownloaded,
      AuditAction.DocumentForwarded,
      AuditAction.DocumentWithdrawn,
    ];
    if (userActions.includes(action)) return 'user';
    if (docActions.includes(action)) return 'document';
    return 'system';
  }

  getInitials(name: string): string {
    return name
      .split(' ')
      .map((n) => n.charAt(0))
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }
}
