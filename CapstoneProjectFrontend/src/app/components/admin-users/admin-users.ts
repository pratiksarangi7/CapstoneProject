import { Component, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../services/admin.service';
import { DepartmentService } from '../../services/department.service';
import { UserDetailsResponseDto, UserDetails } from '../../dtos/user-details-response.dto';
import { Department } from '../../models/department.model';
import { RejectAllDocs } from '../../dtos/reject-all-docs.request.dto';
import { ReassignDocumentsRequestDto } from '../../dtos/reassign-documents.request.dto';
import { AddUserRequestDto } from '../../dtos/add-user.request.dto';
import { BulkUploadResponseDto } from '../../dtos/bulk-upload.response.dto';

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
  mainSearchQuery = signal('');
  private mainSearchTimeout: any;

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
  private managerSearchTimeout: any;

  placeholderMessage = signal<string | null>(null);

  isRejectPanelOpen = signal(false);
  rejectReason = signal('');
  isRejecting = signal(false);
  rejectError = signal<string | null>(null);
  rejectSuccess = signal(false);

  isReassignPanelOpen = signal(false);
  reassignToUserId = signal<number | null>(null);
  isReassigning = signal(false);
  reassignError = signal<string | null>(null);
  reassignSuccess = signal(false);
  allUsersList = signal<UserDetails[]>([]);
  isReassignDropdownOpen = signal(false);
  isReassignSearchLoading = signal(false);
  reassignSearchQuery = signal('');
  private reassignSearchTimeout: any;

  isAddUserModalOpen = signal(false);
  newUser = signal<AddUserRequestDto>({ name: '', email: '', password: '', departmentId: 0 });
  isAddingUser = signal(false);
  addUserError = signal<string | null>(null);
  addUserSuccess = signal(false);

  isBulkUploading = signal(false);
  bulkUploadResult = signal<BulkUploadResponseDto | null>(null);
  isBulkResultModalOpen = signal(false);

  constructor(
    private adminService: AdminService,
    private departmentService: DepartmentService
  ) { }

  ngOnInit(): void {
    this.loadPage();
    this.loadDepartments();
  }

  loadPage(): void {
    this.isLoading.set(true);
    this.adminService
      .getUsersApiCall(this.currentPage(), this.pageSize(), this.mainSearchQuery())
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

  onMainSearchInput(query: string): void {
    this.mainSearchQuery.set(query);
    this.currentPage.set(1);
    if (this.mainSearchTimeout) {
      clearTimeout(this.mainSearchTimeout);
    }
    this.mainSearchTimeout = setTimeout(() => {
      this.loadPage();
    }, 500);
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

  loadPotentialManagers(userId: number, query: string = ''): void {
    if (!query.trim()) {
      this.potentialManagers.set([]);
      this.isManagersLoading.set(false);
      return;
    }
    
    this.isManagersLoading.set(true);
    this.adminService.getPotentialManagers(userId, query).subscribe({
      next: (data) => {
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
  }

  closeUserDetails(): void {
    this.isModalOpen.set(false);
    this.selectedUser.set(null);
    this.placeholderMessage.set(null);
    this.resetManagerSearch();
    this.closeRejectPanel();
    this.closeReassignPanel();
  }

  openReassignPanel(): void {
    this.reassignToUserId.set(null);
    this.reassignError.set(null);
    this.reassignSuccess.set(false);
    this.reassignSearchQuery.set('');
    this.isReassignDropdownOpen.set(false);
    this.allUsersList.set([]);
    this.isReassignPanelOpen.set(true);
  }

  closeReassignPanel(): void {
    this.isReassignPanelOpen.set(false);
    this.reassignToUserId.set(null);
    this.reassignError.set(null);
    this.reassignSuccess.set(false);
    this.reassignSearchQuery.set('');
    this.isReassignDropdownOpen.set(false);
  }

  onReassignSearchInput(query: string): void {
    this.reassignSearchQuery.set(query);
    this.isReassignDropdownOpen.set(query.trim().length > 0);
    this.reassignToUserId.set(null);

    if (this.reassignSearchTimeout) {
      clearTimeout(this.reassignSearchTimeout);
    }

    this.reassignSearchTimeout = setTimeout(() => {
      this.fetchUsersForReassign(query);
    }, 500);
  }

  fetchUsersForReassign(query: string): void {
    if (!query.trim()) {
      this.allUsersList.set([]);
      this.isReassignSearchLoading.set(false);
      return;
    }
    this.isReassignSearchLoading.set(true);
    this.adminService.getUsersApiCall(1, 20, query).subscribe({
      next: (data) => {
        this.allUsersList.set(data.items);
        this.isReassignSearchLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load users for reassignment', err);
        this.isReassignSearchLoading.set(false);
      }
    });
  }

  selectReassignUser(user: UserDetails): void {
    this.reassignToUserId.set(user.id);
    this.reassignSearchQuery.set(user.name);
    this.isReassignDropdownOpen.set(false);
    this.reassignError.set(null);
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

  onManagerSearchInput(query: string): void {
    this.managerSearchQuery.set(query);
    this.isManagerDropdownOpen.set(query.trim().length > 0);
    
    const user = this.selectedUser();
    if (!user) return;

    if (this.managerSearchTimeout) {
      clearTimeout(this.managerSearchTimeout);
    }

    this.managerSearchTimeout = setTimeout(() => {
      this.loadPotentialManagers(user.id, query);
    }, 500);
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
        this.isLoading.set(false);
        alert(`${err.error.message}`);
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

  openAddUserModal(): void {
    this.newUser.set({ name: '', email: '', password: '', departmentId: 0 });
    this.addUserError.set(null);
    this.addUserSuccess.set(false);
    this.isAddUserModalOpen.set(true);
  }

  closeAddUserModal(): void {
    this.isAddUserModalOpen.set(false);
    this.addUserError.set(null);
    this.addUserSuccess.set(false);
  }

  submitAddUser(): void {
    const user = this.newUser();
    if (!user.name || !user.email || !user.password || !user.departmentId || user.departmentId == 0) {
      this.addUserError.set('All fields are required.');
      return;
    }
    this.isAddingUser.set(true);
    this.addUserError.set(null);
    
    // Convert to number explicitly just in case
    const payload = { ...user, departmentId: Number(user.departmentId) };

    this.adminService.addUser(payload).subscribe({
      next: () => {
        this.isAddingUser.set(false);
        this.addUserSuccess.set(true);
        setTimeout(() => {
          this.closeAddUserModal();
          this.loadPage();
        }, 1500);
      },
      error: (err) => {
        console.error('Failed to add user', err);
        this.addUserError.set(err?.error?.message ?? 'Failed to add user. Please try again.');
        this.isAddingUser.set(false);
      }
    });
  }

  triggerBulkUpload(): void {
    document.getElementById('bulk-upload-input')?.click();
  }

  onBulkUploadFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const file = input.files[0];

    if (file.type !== 'text/csv' && !file.name.toLowerCase().endsWith('.csv')) {
      alert('Please upload a valid CSV file.');
      input.value = '';
      return;
    }

    this.isBulkUploading.set(true);
    this.adminService.bulkAddUsers(file).subscribe({
      next: (result) => {
        this.isBulkUploading.set(false);
        this.bulkUploadResult.set(result);
        this.isBulkResultModalOpen.set(true);
        this.loadPage();
        input.value = '';
      },
      error: (err) => {
        console.error('Bulk upload failed', err);
        alert(err?.error?.message ?? 'Bulk upload failed. Please try again.');
        this.isBulkUploading.set(false);
        input.value = '';
      }
    });
  }

  closeBulkResultModal(): void {
    this.isBulkResultModalOpen.set(false);
    this.bulkUploadResult.set(null);
  }
}

