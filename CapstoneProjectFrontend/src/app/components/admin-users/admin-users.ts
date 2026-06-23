import { Component, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../services/admin.service';
import { DepartmentService } from '../../services/department.service';
import { UserDetailsResponseDto, UserDetails } from '../../dtos/user-details-response.dto';
import { Department } from '../../models/department.model';
import { RejectAllDocs } from '../../dtos/reject-all-docs.request.dto';
import { ReassignDocumentsRequestDto } from '../../dtos/reassign-documents.request.dto';

@Component({
  selector: 'app-admin-users',
  imports: [FormsModule],
  templateUrl: './admin-users.html',
  styleUrl: './admin-users.css',
})
export class AdminUsers implements OnInit {
  currentPage = signal(1);
  pageSize = signal(10);
  isLoading = signal(false);

  usersResponse = signal<UserDetailsResponseDto>({
    items: [],
    pageNumber: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  });

  isModalOpen = signal(false);
  selectedUser = signal<UserDetails | null>(null);
  allDepartments = signal<Department[]>([]);

  potentialManagers = signal<UserDetails[]>([]);
  managerSearchQuery = signal('');
  isManagerDropdownOpen = signal(false);
  isManagersLoading = signal(false);

  filteredManagers = computed(() => {
    const q = this.managerSearchQuery().trim().toLowerCase();
    if (!q) return [];
    return this.potentialManagers().filter(m =>
      m.name.toLowerCase().includes(q) ||
      m.email.toLowerCase().includes(q)
    );
  });

  placeholderMessage = signal<string | null>(null);

  // ── Reject-all-docs danger zone ───────────────────────────────────────────
  isRejectPanelOpen = signal(false);
  rejectReason = signal('');
  isRejecting = signal(false);
  rejectError = signal<string | null>(null);
  rejectSuccess = signal(false);

  // ── Reassign documents danger zone ────────────────────────────────────────
  isReassignPanelOpen = signal(false);
  reassignToUserId = signal<number | null>(null);
  isReassigning = signal(false);
  reassignError = signal<string | null>(null);
  reassignSuccess = signal(false);
  allUsersList = signal<UserDetails[]>([]);

  constructor(
    private adminService: AdminService,
    private departmentService: DepartmentService
  ) { }

  ngOnInit(): void {
    this.loadPage();
    this.loadDepartments();
    this.loadAllUsersForReassign();
  }

  loadAllUsersForReassign(): void {
    this.adminService.getUsersApiCall(1, 100).subscribe({
      next: (data) => {
        this.allUsersList.set(data.items);
      },
      error: (err) => console.error('Failed to load users for reassignment', err)
    });
  }

  loadPage(): void {
    this.isLoading.set(true);
    this.adminService
      .getUsersApiCall(this.currentPage(), this.pageSize())
      .subscribe({
        next: (data) => {
          this.usersResponse.set(data);
          this.isLoading.set(false);
        },
        error: (error) => {
          console.error('Failed to load users', error);
          this.isLoading.set(false);
        },
      });
  }

  loadDepartments(): void {
    this.departmentService.GetAllDepartments().subscribe({
      next: (depts) => {
        this.allDepartments.set(depts);
      },
      error: (error) => {
        console.error('Failed to load departments', error);
      }
    });
  }

  loadPotentialManagers(userId: number): void {
    this.isManagersLoading.set(true);
    this.potentialManagers.set([]);
    this.adminService.getPotentialManagers(userId).subscribe({
      next: (data) => {
        console.log(data);
        this.potentialManagers.set(data);
        this.isManagersLoading.set(false);
      },
      error: (error) => {
        console.error('Failed to load potential managers', error);
        this.isManagersLoading.set(false);
      }
    });
  }

  goToPage(page: number): void {
    const total = this.usersResponse().totalPages;
    if (page < 1 || page > total) return;
    this.currentPage.set(page);
    this.loadPage();
  }

  get pageNumbers(): number[] {
    const total = this.usersResponse().totalPages;
    return Array.from({ length: total }, (_, i) => i + 1);
  }

  openUserDetails(user: UserDetails): void {
    this.selectedUser.set(user);
    this.isModalOpen.set(true);
    this.placeholderMessage.set(null);
    this.resetManagerSearch();
    this.loadPotentialManagers(user.id);
  }

  closeUserDetails(): void {
    this.isModalOpen.set(false);
    this.selectedUser.set(null);
    this.placeholderMessage.set(null);
    this.resetManagerSearch();
    this.closeRejectPanel();
    this.closeReassignPanel();
  }

  // ── Reassign documents ────────────────────────────────────────────────────
  openReassignPanel(): void {
    this.reassignToUserId.set(null);
    this.reassignError.set(null);
    this.reassignSuccess.set(false);
    this.isReassignPanelOpen.set(true);
  }

  closeReassignPanel(): void {
    this.isReassignPanelOpen.set(false);
    this.reassignToUserId.set(null);
    this.reassignError.set(null);
    this.reassignSuccess.set(false);
  }

  submitReassignDocuments(): void {
    const toUserId = this.reassignToUserId();
    const user = this.selectedUser();
    if (!user) return;
    if (!toUserId) {
      this.reassignError.set('Please select a user to reassign to.');
      return;
    }
    if (toUserId === user.id) {
      this.reassignError.set('Cannot reassign documents to the same user.');
      return;
    }
    this.isReassigning.set(true);
    this.reassignError.set(null);
    const body: ReassignDocumentsRequestDto = { fromApproverId: user.id, toApproverId: toUserId };
    this.adminService.reassignDocuments(body).subscribe({
      next: () => {
        this.isReassigning.set(false);
        this.reassignSuccess.set(true);
      },
      error: (err) => {
        console.error('Failed to reassign documents', err);
        this.reassignError.set(
          err?.error?.message ?? 'Failed to reassign documents. Please try again.'
        );
        this.isReassigning.set(false);
      },
    });
  }

  // ── Reject all pending documents ──────────────────────────────────────────
  openRejectPanel(): void {
    this.rejectReason.set('');
    this.rejectError.set(null);
    this.rejectSuccess.set(false);
    this.isRejectPanelOpen.set(true);
  }

  closeRejectPanel(): void {
    this.isRejectPanelOpen.set(false);
    this.rejectReason.set('');
    this.rejectError.set(null);
    this.rejectSuccess.set(false);
  }

  submitRejectAllDocs(): void {
    const reason = this.rejectReason().trim();
    const user = this.selectedUser();
    if (!user) return;
    if (!reason) {
      this.rejectError.set('A rejection reason is required.');
      return;
    }
    this.isRejecting.set(true);
    this.rejectError.set(null);
    const body: RejectAllDocs = { reason };
    this.adminService.rejectAllDocs(user.id, body).subscribe({
      next: () => {
        this.isRejecting.set(false);
        this.rejectSuccess.set(true);
      },
      error: (err) => {
        console.error('Failed to reject all pending documents', err);
        this.rejectError.set(
          err?.error?.message ?? 'Failed to reject documents. Please try again.'
        );
        this.isRejecting.set(false);
      },
    });
  }

  resetManagerSearch(): void {
    this.managerSearchQuery.set('');
    this.isManagerDropdownOpen.set(false);
    this.potentialManagers.set([]);
  }

  onManagerSearchInput(): void {
    this.isManagerDropdownOpen.set(this.managerSearchQuery().trim().length > 0);
  }

  selectManager(manager: UserDetails): void {
    const user = this.selectedUser();
    if (!user) return;
    this.isManagerDropdownOpen.set(false);
    this.managerSearchQuery.set(manager.name);
    this.changeManager(user.id, manager.id, manager.name);
  }

  changeManager(userId: number, newManagerId: number, newManagerName: string): void {
    this.isLoading.set(true);
    this.adminService.changeManager({ userId, managerId: newManagerId }).subscribe({
      next: () => {
        const user = this.selectedUser();
        if (user && user.id === userId) {
          this.selectedUser.set({ ...user, managerId: newManagerId, managerName: newManagerName });
          const response = this.usersResponse();
          const updatedItems = response.items.map(u => u.id === userId ? { ...u, managerId: newManagerId, managerName: newManagerName } : u);
          this.usersResponse.set({ ...response, items: updatedItems });
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to change manager', err);
        this.isLoading.set(false);
        alert('An error occurred while changing manager. Please try again.');
      }
    });
  }

  changeLevel(userId: number, newLevel: number): void {
    this.isLoading.set(true);
    this.adminService.changeLevel({ userId, level: newLevel }).subscribe({
      next: () => {
        const user = this.selectedUser();
        if (user && user.id === userId) {
          this.selectedUser.set({ ...user, level: newLevel });
          const response = this.usersResponse();
          const updatedItems = response.items.map(u => u.id === userId ? { ...u, level: newLevel } : u);
          this.usersResponse.set({ ...response, items: updatedItems });
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to change level', err);
        this.isLoading.set(false);
        alert(`Error ${err.error.message}`);
      }
    });
  }

  changeDepartment(userId: number, newDeptId: number): void {
    this.isLoading.set(true);
    this.adminService.changeDepartment({ userId, departmentId: newDeptId }).subscribe({
      next: () => {
        const targetDept = this.allDepartments().find(d => d.id === newDeptId);
        const targetDeptName = targetDept ? targetDept.name : 'Unknown';

        const user = this.selectedUser();
        if (user && user.id === userId) {
          this.selectedUser.set({ ...user, departmentId: newDeptId, departmentName: targetDeptName });
          const response = this.usersResponse();
          const updatedItems = response.items.map(u => u.id === userId ? { ...u, departmentId: newDeptId, departmentName: targetDeptName } : u);
          this.usersResponse.set({ ...response, items: updatedItems });
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to change department', err);
        this.isLoading.set(false);
        alert('An error occurred while changing department. Please try again.');
      }
    });
  }

  deactivateUser(userId: number): void {
    this.isLoading.set(true);
    this.adminService.deactivateUser(userId).subscribe({
      next: () => {
        const user = this.selectedUser();
        if (user && user.id === userId) {
          this.selectedUser.set({ ...user, isActive: false });
          const response = this.usersResponse();
          const updatedItems = response.items.map(u => u.id === userId ? { ...u, isActive: false } : u);
          this.usersResponse.set({ ...response, items: updatedItems });
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to deactivate user', err);
        this.isLoading.set(false);
        alert('An error occurred while deactivating the user. Please try again.');
      }
    });
  }

  reactivateUser(userId: number): void {
    this.isLoading.set(true);
    this.adminService.reactivateUser(userId).subscribe({
      next: () => {
        const user = this.selectedUser();
        if (user && user.id === userId) {
          this.selectedUser.set({ ...user, isActive: true });
          const response = this.usersResponse();
          const updatedItems = response.items.map(u => u.id === userId ? { ...u, isActive: true } : u);
          this.usersResponse.set({ ...response, items: updatedItems });
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to reactivate user', err);
        this.isLoading.set(false);
        alert('An error occurred while reactivating the user. Please try again.');
      }
    });
  }

  onDepartmentChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const user = this.selectedUser();
    if (user && select.value) {
      this.changeDepartment(user.id, parseInt(select.value, 10));
    }
  }

  onLevelChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const user = this.selectedUser();
    if (user && select.value) {
      this.changeLevel(user.id, parseInt(select.value, 10));
    }
  }
}

