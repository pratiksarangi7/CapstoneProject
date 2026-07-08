import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Component } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AdminDashboard } from './admin-dashboard';
import { AuthService } from '../../services/auth.service';
import { AdminService } from '../../services/admin.service';
import { DepartmentService } from '../../services/department.service';
import { DocumentService } from '../../services/document.service';
import { AuditlogService } from '../../services/auditlogs.service';

@Component({ template: '', standalone: true }) class StubLogin {}

const emptyPaged = {
  items: [], pageNumber: 1, pageSize: 10,
  totalCount: 0, totalPages: 0,
  hasPreviousPage: false, hasNextPage: false,
};

async function setup() {
  await TestBed.configureTestingModule({
    imports: [AdminDashboard],
    providers: [
      provideRouter([{ path: 'login', component: StubLogin }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      AuthService,
      AdminService,
      DepartmentService,
      DocumentService,
      AuditlogService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(AdminDashboard);
  const component = fixture.componentInstance;
  const router = TestBed.inject(Router);
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();

  httpTesting.match((r) => r.url.includes('/admin/users')).forEach((r) => r.flush(emptyPaged));
  httpTesting.match((r) => r.url.includes('/User/departments')).forEach((r) => r.flush([]));
  httpTesting.match((r) => r.url.includes('/admin/documents/all')).forEach((r) => r.flush(emptyPaged));
  httpTesting.match((r) => r.url.includes('/Admin/departments')).forEach((r) => r.flush([]));
  httpTesting.match((r) => r.url.includes('/auditlog')).forEach((r) => r.flush({ ...emptyPaged, pageSize: 15 }));
  fixture.detectChanges();

  return { fixture, component, router, httpTesting };
}

describe('AdminDashboard Component', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  describe('Component Initialization', () => {
    it('should create the component', async () => {
      const { component } = await setup();
      expect(component).toBeTruthy();
    });

    it('should initialize activeTab to "users"', async () => {
      const { component } = await setup();
      expect(component.activeTab()).toBe('users');
    });

    it('should expose four menu items', async () => {
      const { component } = await setup();
      expect(component.menuItems).toHaveLength(4);
    });

    it('should have menu items with correct ids', async () => {
      const { component } = await setup();
      const ids = component.menuItems.map((m) => m.id);
      expect(ids).toEqual(['users', 'documents', 'departments', 'audit-logs']);
    });

    it('should have menu items with correct labels', async () => {
      const { component } = await setup();
      const labels = component.menuItems.map((m) => m.label);
      expect(labels).toEqual(['Users', 'Documents', 'Departments', 'Audit Logs']);
    });
  });

  describe('setActiveTab()', () => {
    it('should switch activeTab to "documents"', async () => {
      const { component } = await setup();
      component.setActiveTab('documents');
      expect(component.activeTab()).toBe('documents');
    });

    it('should switch activeTab to "departments"', async () => {
      const { component } = await setup();
      component.setActiveTab('departments');
      expect(component.activeTab()).toBe('departments');
    });

    it('should switch activeTab to "audit-logs"', async () => {
      const { component } = await setup();
      component.setActiveTab('audit-logs');
      expect(component.activeTab()).toBe('audit-logs');
    });

    it('should switch activeTab back to "users"', async () => {
      const { component } = await setup();
      component.setActiveTab('documents');
      component.setActiveTab('users');
      expect(component.activeTab()).toBe('users');
    });
  });

  describe('userName signal', () => {
    it('should reflect null when no name is stored', async () => {
      const { component } = await setup();
      expect(component.userName()).toBeNull();
    });

    it('should reflect the name pushed by AuthService.updateUserName', async () => {
      const { component } = await setup();
      const authService = TestBed.inject(AuthService);
      authService.updateUserName('Alice');
      expect(component.userName()).toBe('Alice');
    });

    it('should reflect null after clearing the name', async () => {
      const { component } = await setup();
      const authService = TestBed.inject(AuthService);
      authService.updateUserName('Alice');
      authService.updateUserName(null);
      expect(component.userName()).toBeNull();
    });
  });

  describe('logout()', () => {
    it('should remove the token from localStorage', async () => {
      const { component } = await setup();
      localStorage.setItem('token', 'some.jwt.token');
      component.logout();
      expect(localStorage.getItem('token')).toBeNull();
    });

    it('should navigate to /login', async () => {
      const { component, router } = await setup();
      const navigateSpy = vi.spyOn(router, 'navigate');
      component.logout();
      expect(navigateSpy).toHaveBeenCalledWith(['/login']);
    });

    it('should not throw when token is already absent', async () => {
      const { component } = await setup();
      expect(() => component.logout()).not.toThrow();
    });
  });
});
