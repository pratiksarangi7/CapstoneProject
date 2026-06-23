import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../services/admin.service';
import { DepartmentResponseDto } from '../../dtos/department.response.dto';

@Component({
  selector: 'app-admin-departments',
  imports: [FormsModule],
  templateUrl: './admin-departments.html',
  styleUrl: './admin-departments.css',
})
export class AdminDepartments implements OnInit {
  // ── Data ─────────────────────────────────────────────────────────────────────
  departments = signal<DepartmentResponseDto[]>([]);
  isLoading = signal(false);

  // ── Accordion state ───────────────────────────────────────────────────────────
  // Stores the id of the currently open department; null = all collapsed
  openDeptId = signal<number | null>(null);

  // ── Create-department modal ───────────────────────────────────────────────────
  isModalOpen = signal(false);
  newDeptName = signal('');
  isCreating = signal(false);
  createError = signal<string | null>(null);
  createSuccess = signal<string | null>(null);

  constructor(private adminService: AdminService) {}

  ngOnInit(): void {
    this.loadDepartments();
  }

  // ── Load departments ──────────────────────────────────────────────────────────
  loadDepartments(): void {
    this.isLoading.set(true);
    this.adminService.getDepartments().subscribe({
      next: (data) => {
        this.departments.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load departments', err);
        this.isLoading.set(false);
      },
    });
  }

  // ── Accordion toggle ──────────────────────────────────────────────────────────
  toggleDept(id: number): void {
    this.openDeptId.set(this.openDeptId() === id ? null : id);
  }

  isDeptOpen(id: number): boolean {
    return this.openDeptId() === id;
  }

  // ── Create department modal ───────────────────────────────────────────────────
  openCreateModal(): void {
    this.newDeptName.set('');
    this.createError.set(null);
    this.createSuccess.set(null);
    this.isModalOpen.set(true);
  }

  closeCreateModal(): void {
    if (this.isCreating()) return;
    this.isModalOpen.set(false);
    this.newDeptName.set('');
    this.createError.set(null);
    this.createSuccess.set(null);
  }

  submitCreateDepartment(): void {
    const name = this.newDeptName().trim();
    if (!name) {
      this.createError.set('Department name cannot be empty.');
      return;
    }

    this.isCreating.set(true);
    this.createError.set(null);

    this.adminService.createNewDepartments({ name }).subscribe({
      next: () => {
        this.isCreating.set(false);
        this.createSuccess.set(`Department "${name}" created successfully!`);
        // Refresh the list, then close after a brief delay
        this.loadDepartments();
        setTimeout(() => this.closeCreateModal(), 1400);
      },
      error: (err) => {
        console.error('Failed to create department', err);
        this.createError.set(
          err?.error?.message ?? 'Failed to create department. Please try again.'
        );
        this.isCreating.set(false);
      },
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────────
  getInitials(name: string): string {
    return name
      .split(' ')
      .map((n) => n.charAt(0))
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }

  getLevelLabel(level: number): string {
    return `Level ${level}`;
  }
}
