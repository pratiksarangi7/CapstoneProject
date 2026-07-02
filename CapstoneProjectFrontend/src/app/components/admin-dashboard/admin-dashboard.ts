import { Component, signal, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AdminUsers } from '../admin-users/admin-users';
import { AdminDocuments } from '../admin-documents/admin-documents';
import { AdminDepartments } from '../admin-departments/admin-departments';
import { AdminAuditLogs } from '../admin-audit-logs/admin-audit-logs';
import { AuthService } from '../../services/auth.service';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-admin-dashboard',
  imports: [AdminUsers, AdminDocuments, AdminDepartments, AdminAuditLogs],
  templateUrl: './admin-dashboard.html',
  styleUrl: './admin-dashboard.css',
})
export class AdminDashboard {
  activeTab = signal<'users' | 'documents' | 'departments' | 'audit-logs'>('users');

  menuItems = [
    { id: 'users', label: 'Users' },
    { id: 'documents', label: 'Documents' },
    { id: 'departments', label: 'Departments' },
    { id: 'audit-logs', label: 'Audit Logs' }
  ] as const;

  private router = inject(Router);
  private authService = inject(AuthService);

  userName = toSignal(this.authService.userName$, { initialValue: null });

  setActiveTab(tab: 'users' | 'documents' | 'departments' | 'audit-logs') {
    this.activeTab.set(tab);
  }

  logout() {
    localStorage.removeItem('token');
    this.router.navigate(['/login']);
  }
}
