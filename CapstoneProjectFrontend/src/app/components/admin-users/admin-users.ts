import { Component, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../services/admin.service';
import { DepartmentService } from '../../services/department.service';
import { UserDetailsResponseDto, UserDetails } from '../../dtos/user-details-response.dto';
import { Department } from '../../models/department.model';

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

