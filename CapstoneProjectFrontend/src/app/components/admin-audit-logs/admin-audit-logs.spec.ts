import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AdminAuditLogs } from './admin-audit-logs';
import { AdminService } from '../../services/admin.service';
import { AuditlogService } from '../../services/auditlogs.service';
import { AuditLogResponseDto } from '../../dtos/audit-logs.response.dto';
import { UserDetails } from '../../dtos/user-details-response.dto';
import { PaginatedResponse } from '../../helpers';
import { AuditAction } from '../../enums/audit-action.enum';

const makeUser = (overrides: Partial<UserDetails> = {}): UserDetails => ({
  id: 1,
  name: 'Alice Smith',
  email: 'alice@example.com',
  isAdmin: false,
  departmentId: 10,
  departmentName: 'Engineering',
  managerId: null,
  managerName: null,
  level: 1,
  isActive: true,
  ...overrides,
});

const makeLog = (overrides: Partial<AuditLogResponseDto> = {}): AuditLogResponseDto => ({
  id: 1,
  performedByUserId: 1,
  performedByUserName: 'Alice Smith',
  performedByUserEmail: 'alice@example.com',
  documentId: null,
  documentTitle: null,
  documentVersionId: null,
  documentVersionNumber: null,
  action: AuditAction.UserLoggedIn,
  details: null,
  createdAt: '2024-06-01T09:00:00Z',
  ...overrides,
});

const makePagedUsers = (items: UserDetails[] = []): PaginatedResponse<UserDetails> => ({
  items,
  pageNumber: 1,
  pageSize: 200,
  totalCount: items.length,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false,
});

const makePagedLogs = (
  items: AuditLogResponseDto[] = [],
  overrides: Partial<PaginatedResponse<AuditLogResponseDto>> = {}
): PaginatedResponse<AuditLogResponseDto> => ({
  items,
  pageNumber: 1,
  pageSize: 15,
  totalCount: items.length,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false,
  ...overrides,
});

async function setup() {
  await TestBed.configureTestingModule({
    imports: [AdminAuditLogs],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      AdminService,
      AuditlogService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(AdminAuditLogs);
  const component = fixture.componentInstance;
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();

  httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush(makePagedUsers([makeUser()]));
  httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs([makeLog()]));
  fixture.detectChanges();

  return { fixture, component, httpTesting };
}

describe('AdminAuditLogs Component', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  describe('Component Initialization', () => {
    it('should create the component', async () => {
      const { component } = await setup();
      expect(component).toBeTruthy();
    });

    it('should initialize currentPage to 1', async () => {
      const { component } = await setup();
      expect(component.currentPage()).toBe(1);
    });

    it('should initialize pageSize to 15', async () => {
      const { component } = await setup();
      expect(component.pageSize()).toBe(15);
    });

    it('should initialize isLoading to false after data loads', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should initialize selectedAction to null', async () => {
      const { component } = await setup();
      expect(component.selectedAction()).toBeNull();
    });

    it('should initialize selectedUser to null', async () => {
      const { component } = await setup();
      expect(component.selectedUser()).toBeNull();
    });

    it('should initialize userSearchQuery to empty string', async () => {
      const { component } = await setup();
      expect(component.userSearchQuery()).toBe('');
    });

    it('should initialize isUserDropdownOpen to false', async () => {
      const { component } = await setup();
      expect(component.isUserDropdownOpen()).toBe(false);
    });

    it('should initialize expandedLogId to null', async () => {
      const { component } = await setup();
      expect(component.expandedLogId()).toBeNull();
    });

    it('should expose all AuditAction values in auditActions', async () => {
      const { component } = await setup();
      expect(component.auditActions).toEqual(Object.values(AuditAction));
    });

    it('should load users on init', async () => {
      const { component } = await setup();
      expect(component.allUsers()).toHaveLength(1);
    });

    it('should load logs on init', async () => {
      const { component } = await setup();
      expect(component.logsResponse().items).toHaveLength(1);
    });
  });

  describe('loadUsers()', () => {
    it('should set isUsersLoading to true while fetching', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminAuditLogs],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService, AuditlogService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminAuditLogs);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      expect(component.isUsersLoading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush(makePagedUsers());
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });

    it('should populate allUsers on success', async () => {
      const { component } = await setup();
      expect(component.allUsers()[0].name).toBe('Alice Smith');
    });

    it('should set isUsersLoading to false after success', async () => {
      const { component } = await setup();
      expect(component.isUsersLoading()).toBe(false);
    });

    it('should set isUsersLoading to false after error', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminAuditLogs],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService, AuditlogService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminAuditLogs);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush({ message: 'err' }, { status: 500, statusText: 'Server Error' });
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());

      expect(component.isUsersLoading()).toBe(false);
    });
  });

  describe('loadLogs()', () => {
    it('should set isLoading to true while fetching', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminAuditLogs],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService, AuditlogService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminAuditLogs);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      expect(component.isLoading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush(makePagedUsers());
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });

    it('should populate logsResponse on success', async () => {
      const { component } = await setup();
      expect(component.logsResponse().totalCount).toBe(1);
    });

    it('should set isLoading to false after success', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should set isLoading to false after error', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminAuditLogs],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService, AuditlogService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminAuditLogs);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush(makePagedUsers());
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush({ message: 'err' }, { status: 500, statusText: 'Server Error' });

      expect(component.isLoading()).toBe(false);
    });

    it('should send the action param when selectedAction is set', async () => {
      const { component, httpTesting } = await setup();
      component.selectedAction.set(AuditAction.DocumentUploaded);
      component.loadLogs();
      const req = httpTesting.expectOne((r) => r.url.includes('/auditlog'));
      expect(req.request.params.get('action')).toBe('DocumentUploaded');
      req.flush(makePagedLogs());
    });

    it('should send the userId param when selectedUser is set', async () => {
      const { component, httpTesting } = await setup();
      component.selectedUser.set(makeUser({ id: 5 }));
      component.loadLogs();
      const req = httpTesting.expectOne((r) => r.url.includes('/auditlog'));
      expect(req.request.params.get('userId')).toBe('5');
      req.flush(makePagedLogs());
    });
  });

  describe('toggleLogExpand()', () => {
    it('should set expandedLogId when a log is toggled open', async () => {
      const { component } = await setup();
      component.toggleLogExpand(42);
      expect(component.expandedLogId()).toBe(42);
    });

    it('should collapse a log when toggled again', async () => {
      const { component } = await setup();
      component.toggleLogExpand(42);
      component.toggleLogExpand(42);
      expect(component.expandedLogId()).toBeNull();
    });

    it('should switch to a different log when a new one is toggled', async () => {
      const { component } = await setup();
      component.toggleLogExpand(1);
      component.toggleLogExpand(2);
      expect(component.expandedLogId()).toBe(2);
    });
  });

  describe('goToPage()', () => {
    it('should not navigate below page 1', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(component, 'loadLogs');
      component.goToPage(0);
      expect(spy).not.toHaveBeenCalled();
      httpTesting.verify();
    });

    it('should not navigate beyond totalPages', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(component, 'loadLogs');
      component.goToPage(999);
      expect(spy).not.toHaveBeenCalled();
      httpTesting.verify();
    });

    it('should set currentPage and call loadLogs for a valid page', async () => {
      const { component, httpTesting } = await setup();
      component.logsResponse.set(makePagedLogs([makeLog()], { totalPages: 5 }));
      component.goToPage(3);
      expect(component.currentPage()).toBe(3);
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });
  });

  describe('pageNumbers getter', () => {
    it('should return all pages when totalPages is <= 7', async () => {
      const { component } = await setup();
      component.logsResponse.set(makePagedLogs([], { totalPages: 4 }));
      expect(component.pageNumbers).toEqual([1, 2, 3, 4]);
    });

    it('should return empty array when totalPages is 0', async () => {
      const { component } = await setup();
      component.logsResponse.set(makePagedLogs([], { totalPages: 0 }));
      expect(component.pageNumbers).toEqual([]);
    });

    it('should return a window of 7 pages when totalPages exceeds 7', async () => {
      const { component } = await setup();
      component.logsResponse.set(makePagedLogs([], { totalPages: 20 }));
      component.currentPage.set(1);
      expect(component.pageNumbers).toHaveLength(7);
    });

    it('should include the current page in the returned window', async () => {
      const { component } = await setup();
      component.logsResponse.set(makePagedLogs([], { totalPages: 20 }));
      component.currentPage.set(10);
      expect(component.pageNumbers).toContain(10);
    });
  });

  describe('onActionChange()', () => {
    it('should reset currentPage to 1 and call loadLogs', async () => {
      const { component, httpTesting } = await setup();
      component.currentPage.set(3);
      component.selectedAction.set(AuditAction.DocumentApproved);
      component.onActionChange();
      expect(component.currentPage()).toBe(1);
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });
  });

  describe('clearActionFilter()', () => {
    it('should set selectedAction to null', async () => {
      const { component, httpTesting } = await setup();
      component.selectedAction.set(AuditAction.DocumentApproved);
      component.clearActionFilter();
      expect(component.selectedAction()).toBeNull();
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });

    it('should reset currentPage to 1 and trigger loadLogs', async () => {
      const { component, httpTesting } = await setup();
      component.currentPage.set(4);
      component.clearActionFilter();
      expect(component.currentPage()).toBe(1);
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });
  });

  describe('filteredUsers computed', () => {
    it('should return empty array when userSearchQuery is empty', async () => {
      const { component } = await setup();
      component.userSearchQuery.set('');
      expect(component.filteredUsers()).toHaveLength(0);
    });

    it('should filter users by name (case-insensitive)', async () => {
      const { component } = await setup();
      component.allUsers.set([makeUser({ name: 'Alice Smith' }), makeUser({ id: 2, name: 'Bob Jones', email: 'bob@example.com' })]);
      component.userSearchQuery.set('alice');
      expect(component.filteredUsers()).toHaveLength(1);
      expect(component.filteredUsers()[0].name).toBe('Alice Smith');
    });

    it('should filter users by email (case-insensitive)', async () => {
      const { component } = await setup();
      component.allUsers.set([makeUser({ email: 'alice@corp.com' }), makeUser({ id: 2, name: 'Bob', email: 'bob@corp.com' })]);
      component.userSearchQuery.set('BOB');
      expect(component.filteredUsers()).toHaveLength(1);
      expect(component.filteredUsers()[0].name).toBe('Bob');
    });

    it('should return empty array when no user matches the query', async () => {
      const { component } = await setup();
      component.allUsers.set([makeUser()]);
      component.userSearchQuery.set('xyz-nonexistent');
      expect(component.filteredUsers()).toHaveLength(0);
    });
  });

  describe('onUserSearchInput()', () => {
    it('should open the dropdown when query is non-empty', async () => {
      const { component } = await setup();
      component.userSearchQuery.set('Al');
      component.onUserSearchInput();
      expect(component.isUserDropdownOpen()).toBe(true);
    });

    it('should close the dropdown when query is empty', async () => {
      const { component } = await setup();
      component.isUserDropdownOpen.set(true);
      component.userSearchQuery.set('');
      component.onUserSearchInput();
      expect(component.isUserDropdownOpen()).toBe(false);
    });

    it('should call clearUserFilter when query is cleared and a user was selected', async () => {
      const { component, httpTesting } = await setup();
      component.selectedUser.set(makeUser());
      component.userSearchQuery.set('');
      component.onUserSearchInput();
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
      expect(component.selectedUser()).toBeNull();
    });
  });

  describe('selectUser()', () => {
    it('should set selectedUser', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 7, name: 'Carol' });
      component.selectUser(user);
      expect(component.selectedUser()).toEqual(user);
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });

    it('should update userSearchQuery to the selected user name', async () => {
      const { component, httpTesting } = await setup();
      component.selectUser(makeUser({ name: 'Carol' }));
      expect(component.userSearchQuery()).toBe('Carol');
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });

    it('should close the dropdown', async () => {
      const { component, httpTesting } = await setup();
      component.isUserDropdownOpen.set(true);
      component.selectUser(makeUser());
      expect(component.isUserDropdownOpen()).toBe(false);
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });

    it('should reset currentPage to 1 and trigger loadLogs', async () => {
      const { component, httpTesting } = await setup();
      component.currentPage.set(4);
      component.selectUser(makeUser());
      expect(component.currentPage()).toBe(1);
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });
  });

  describe('clearUserFilter()', () => {
    it('should set selectedUser to null', async () => {
      const { component, httpTesting } = await setup();
      component.selectedUser.set(makeUser());
      component.clearUserFilter();
      expect(component.selectedUser()).toBeNull();
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });

    it('should clear userSearchQuery', async () => {
      const { component, httpTesting } = await setup();
      component.userSearchQuery.set('Alice');
      component.clearUserFilter();
      expect(component.userSearchQuery()).toBe('');
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });

    it('should close the dropdown and reset currentPage to 1', async () => {
      const { component, httpTesting } = await setup();
      component.isUserDropdownOpen.set(true);
      component.currentPage.set(3);
      component.clearUserFilter();
      expect(component.isUserDropdownOpen()).toBe(false);
      expect(component.currentPage()).toBe(1);
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });
  });

  describe('resetFilters()', () => {
    it('should clear selectedAction and selectedUser', async () => {
      const { component, httpTesting } = await setup();
      component.selectedAction.set(AuditAction.DocumentApproved);
      component.selectedUser.set(makeUser());
      component.resetFilters();
      expect(component.selectedAction()).toBeNull();
      expect(component.selectedUser()).toBeNull();
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });

    it('should reset userSearchQuery and currentPage', async () => {
      const { component, httpTesting } = await setup();
      component.userSearchQuery.set('Alice');
      component.currentPage.set(5);
      component.resetFilters();
      expect(component.userSearchQuery()).toBe('');
      expect(component.currentPage()).toBe(1);
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });

    it('should close the user dropdown', async () => {
      const { component, httpTesting } = await setup();
      component.isUserDropdownOpen.set(true);
      component.resetFilters();
      expect(component.isUserDropdownOpen()).toBe(false);
      httpTesting.expectOne((r) => r.url.includes('/auditlog')).flush(makePagedLogs());
    });
  });

  describe('hasActiveFilters getter', () => {
    it('should return false when neither action nor user is selected', async () => {
      const { component } = await setup();
      expect(component.hasActiveFilters).toBe(false);
    });

    it('should return true when selectedAction is set', async () => {
      const { component } = await setup();
      component.selectedAction.set(AuditAction.DocumentApproved);
      expect(component.hasActiveFilters).toBe(true);
    });

    it('should return true when selectedUser is set', async () => {
      const { component } = await setup();
      component.selectedUser.set(makeUser());
      expect(component.hasActiveFilters).toBe(true);
    });

    it('should return true when both are set', async () => {
      const { component } = await setup();
      component.selectedAction.set(AuditAction.DocumentApproved);
      component.selectedUser.set(makeUser());
      expect(component.hasActiveFilters).toBe(true);
    });
  });

  describe('formatAction()', () => {
    it('should insert spaces before each capital letter', async () => {
      const { component } = await setup();
      expect(component.formatAction(AuditAction.DocumentUploaded)).toBe('Document Uploaded');
    });

    it('should handle single-word actions', async () => {
      const { component } = await setup();
      expect(component.formatAction(AuditAction.UserLoggedIn)).toBe('User Logged In');
    });
  });

  describe('getActionCategory()', () => {
    it('should return "user" for UserRegistered', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.UserRegistered)).toBe('user');
    });

    it('should return "user" for UserLoggedIn', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.UserLoggedIn)).toBe('user');
    });

    it('should return "user" for UserDeactivated', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.UserDeactivated)).toBe('user');
    });

    it('should return "user" for UserReactivated', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.UserReactivated)).toBe('user');
    });

    it('should return "user" for PasswordChanged', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.PasswordChanged)).toBe('user');
    });

    it('should return "document" for DocumentUploaded', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.DocumentUploaded)).toBe('document');
    });

    it('should return "document" for DocumentApproved', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.DocumentApproved)).toBe('document');
    });

    it('should return "document" for DocumentRejected', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.DocumentRejected)).toBe('document');
    });

    it('should return "document" for DocumentForwarded', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.DocumentForwarded)).toBe('document');
    });

    it('should return "document" for DocumentWithdrawn', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.DocumentWithdrawn)).toBe('document');
    });

    it('should return "system" for DepartmentDeleted', async () => {
      const { component } = await setup();
      expect(component.getActionCategory(AuditAction.DepartmentDeleted)).toBe('system');
    });
  });

  describe('getInitials()', () => {
    it('should return the first letter of a single-word name', async () => {
      const { component } = await setup();
      expect(component.getInitials('Alice')).toBe('A');
    });

    it('should return initials of a two-word name', async () => {
      const { component } = await setup();
      expect(component.getInitials('Alice Smith')).toBe('AS');
    });

    it('should return at most 2 characters for longer names', async () => {
      const { component } = await setup();
      expect(component.getInitials('Alice Bob Carol')).toBe('AB');
    });

    it('should return uppercase initials', async () => {
      const { component } = await setup();
      expect(component.getInitials('john doe')).toBe('JD');
    });
  });
});
