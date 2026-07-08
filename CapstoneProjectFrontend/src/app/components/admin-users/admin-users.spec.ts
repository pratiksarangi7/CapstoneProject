import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AdminUsers } from './admin-users';
import { AdminService } from '../../services/admin.service';
import { DepartmentService } from '../../services/department.service';
import { UserDetails, UserDetailsResponseDto } from '../../dtos/user-details-response.dto';
import { Department } from '../../models/department.model';

const makeUser = (overrides: Partial<UserDetails> = {}): UserDetails => ({
  id: 1,
  name: 'Alice',
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

const makePagedResponse = (items: UserDetails[] = [], overrides: Partial<UserDetailsResponseDto> = {}): UserDetailsResponseDto => ({
  items,
  pageNumber: 1,
  pageSize: 10,
  totalCount: items.length,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false,
  ...overrides,
});

async function setup() {
  await TestBed.configureTestingModule({
    imports: [AdminUsers],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      AdminService,
      DepartmentService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(AdminUsers);
  const component = fixture.componentInstance;
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();

  httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush(makePagedResponse([makeUser()]));
  httpTesting.expectOne((r) => r.url.includes('/User/departments')).flush([{ id: 10, name: 'Engineering' }, { id: 20, name: 'HR' }] as Department[]);
  fixture.detectChanges();

  return { fixture, component, httpTesting };
}

describe('AdminUsers Component', () => {
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

    it('should initialize pageSize to 10', async () => {
      const { component } = await setup();
      expect(component.pageSize()).toBe(10);
    });

    it('should initialize isLoading to false after data loads', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should initialize isModalOpen to false', async () => {
      const { component } = await setup();
      expect(component.isModalOpen()).toBe(false);
    });

    it('should initialize selectedUser to null', async () => {
      const { component } = await setup();
      expect(component.selectedUser()).toBeNull();
    });

    it('should load users on init', async () => {
      const { component } = await setup();
      expect(component.usersResponse().items).toHaveLength(1);
    });

    it('should load departments on init', async () => {
      const { component } = await setup();
      expect(component.allDepartments()).toHaveLength(2);
    });
  });

  describe('loadPage()', () => {
    it('should set isLoading to true while fetching', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminUsers],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService, DepartmentService],
      }).compileComponents();
      const fixture = TestBed.createComponent(AdminUsers);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      expect(component.isLoading()).toBe(true);

      httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush(makePagedResponse());
      httpTesting.expectOne((r) => r.url.includes('/User/departments')).flush([]);
    });

    it('should populate usersResponse on success', async () => {
      const { component } = await setup();
      expect(component.usersResponse().totalCount).toBe(1);
    });

    it('should set isLoading to false after success', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should set isLoading to false after error', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminUsers],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService, DepartmentService],
      }).compileComponents();
      const fixture = TestBed.createComponent(AdminUsers);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush({ message: 'err' }, { status: 500, statusText: 'Server Error' });
      httpTesting.expectOne((r) => r.url.includes('/User/departments')).flush([]);

      expect(component.isLoading()).toBe(false);
    });
  });

  describe('goToPage()', () => {
    it('should not navigate below page 1', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(component, 'loadPage');
      component.goToPage(0);
      expect(spy).not.toHaveBeenCalled();
      httpTesting.verify();
    });

    it('should not navigate beyond totalPages', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(component, 'loadPage');
      component.goToPage(999);
      expect(spy).not.toHaveBeenCalled();
      httpTesting.verify();
    });

    it('should set currentPage and call loadPage for a valid page', async () => {
      const { component, httpTesting } = await setup();
      component.usersResponse.set(makePagedResponse([makeUser()], { totalPages: 3 }));
      component.goToPage(2);
      expect(component.currentPage()).toBe(2);
      httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush(makePagedResponse());
    });
  });

  describe('pageNumbers getter', () => {
    it('should return an array based on totalPages', async () => {
      const { component } = await setup();
      component.usersResponse.set(makePagedResponse([], { totalPages: 3 }));
      expect(component.pageNumbers).toEqual([1, 2, 3]);
    });

    it('should return empty array when totalPages is 0', async () => {
      const { component } = await setup();
      component.usersResponse.set(makePagedResponse([], { totalPages: 0 }));
      expect(component.pageNumbers).toEqual([]);
    });
  });

  describe('openUserDetails() / closeUserDetails()', () => {
    it('should set selectedUser and open the modal', async () => {
      const { component } = await setup();
      const user = makeUser();
      component.openUserDetails(user);
      expect(component.selectedUser()).toEqual(user);
      expect(component.isModalOpen()).toBe(true);
    });

    it('should reset placeholderMessage when opening details', async () => {
      const { component } = await setup();
      component.placeholderMessage.set('old message');
      component.openUserDetails(makeUser());
      expect(component.placeholderMessage()).toBeNull();
    });

    it('should close modal and clear selectedUser', async () => {
      const { component } = await setup();
      component.openUserDetails(makeUser());
      component.closeUserDetails();
      expect(component.isModalOpen()).toBe(false);
      expect(component.selectedUser()).toBeNull();
    });

    it('should reset manager search on close', async () => {
      const { component } = await setup();
      component.managerSearchQuery.set('some query');
      component.openUserDetails(makeUser());
      component.closeUserDetails();
      expect(component.managerSearchQuery()).toBe('');
    });
  });

  describe('openRejectPanel() / closeRejectPanel()', () => {
    it('should open the reject panel and reset state', async () => {
      const { component } = await setup();
      component.rejectReason.set('old reason');
      component.rejectError.set('old error');
      component.rejectSuccess.set(true);
      component.openRejectPanel();
      expect(component.isRejectPanelOpen()).toBe(true);
      expect(component.rejectReason()).toBe('');
      expect(component.rejectError()).toBeNull();
      expect(component.rejectSuccess()).toBe(false);
    });

    it('should close the reject panel and reset state', async () => {
      const { component } = await setup();
      component.openRejectPanel();
      component.rejectReason.set('reason');
      component.closeRejectPanel();
      expect(component.isRejectPanelOpen()).toBe(false);
      expect(component.rejectReason()).toBe('');
    });
  });

  describe('submitRejectAllDocs()', () => {
    it('should set rejectError when reason is empty', async () => {
      const { component } = await setup();
      component.selectedUser.set(makeUser());
      component.rejectReason.set('   ');
      component.submitRejectAllDocs();
      expect(component.rejectError()).toBe('A rejection reason is required.');
    });

    it('should do nothing when selectedUser is null', async () => {
      const { component, httpTesting } = await setup();
      component.selectedUser.set(null);
      component.rejectReason.set('some reason');
      component.submitRejectAllDocs();
      httpTesting.verify();
    });

    it('should set isRejecting to true while request is in flight', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 5 });
      component.selectedUser.set(user);
      component.rejectReason.set('bad docs');
      component.submitRejectAllDocs();
      expect(component.isRejecting()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/reject-pending-documents')).flush({});
    });

    it('should set rejectSuccess to true on success', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 5 });
      component.selectedUser.set(user);
      component.rejectReason.set('bad docs');
      component.submitRejectAllDocs();
      httpTesting.expectOne((r) => r.url.includes('/reject-pending-documents')).flush({});
      expect(component.rejectSuccess()).toBe(true);
      expect(component.isRejecting()).toBe(false);
    });

    it('should set rejectError and clear isRejecting on API error', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 5 });
      component.selectedUser.set(user);
      component.rejectReason.set('bad docs');
      component.submitRejectAllDocs();
      httpTesting.expectOne((r) => r.url.includes('/reject-pending-documents')).flush(
        { message: 'Server reject error' },
        { status: 500, statusText: 'Internal Server Error' }
      );
      expect(component.rejectError()).toBe('Server reject error');
      expect(component.isRejecting()).toBe(false);
    });

    it('should send the reject reason in the request body', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 7 });
      component.selectedUser.set(user);
      component.rejectReason.set('invalid format');
      component.submitRejectAllDocs();
      const req = httpTesting.expectOne((r) => r.url.includes('/users/7/reject-pending-documents'));
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ reason: 'invalid format' });
      req.flush({});
    });
  });

  describe('openReassignPanel() / closeReassignPanel()', () => {
    it('should open the reassign panel and reset state', async () => {
      const { component } = await setup();
      component.openReassignPanel();
      expect(component.isReassignPanelOpen()).toBe(true);
      expect(component.reassignToUserId()).toBeNull();
      expect(component.reassignError()).toBeNull();
      expect(component.reassignSuccess()).toBe(false);
    });

    it('should close the reassign panel and reset state', async () => {
      const { component } = await setup();
      component.openReassignPanel();
      component.closeReassignPanel();
      expect(component.isReassignPanelOpen()).toBe(false);
    });
  });

  describe('selectReassignUser()', () => {
    it('should set reassignToUserId and update the search query', async () => {
      const { component } = await setup();
      const target = makeUser({ id: 99, name: 'Bob' });
      component.selectReassignUser(target);
      expect(component.reassignToUserId()).toBe(99);
      expect(component.reassignSearchQuery()).toBe('Bob');
      expect(component.isReassignDropdownOpen()).toBe(false);
    });
  });

  describe('submitReassignDocuments()', () => {
    it('should set reassignError when no target user is selected', async () => {
      const { component } = await setup();
      component.selectedUser.set(makeUser({ id: 1 }));
      component.reassignToUserId.set(null);
      component.submitReassignDocuments();
      expect(component.reassignError()).toBe('Please select a user to reassign to.');
    });

    it('should set reassignError when reassigning to the same user', async () => {
      const { component } = await setup();
      component.selectedUser.set(makeUser({ id: 1 }));
      component.reassignToUserId.set(1);
      component.submitReassignDocuments();
      expect(component.reassignError()).toBe('Cannot reassign documents to the same user.');
    });

    it('should do nothing when selectedUser is null', async () => {
      const { component, httpTesting } = await setup();
      component.selectedUser.set(null);
      component.reassignToUserId.set(5);
      component.submitReassignDocuments();
      httpTesting.verify();
    });

    it('should set isReassigning to true while request is in flight', async () => {
      const { component, httpTesting } = await setup();
      component.selectedUser.set(makeUser({ id: 1 }));
      component.reassignToUserId.set(2);
      component.submitReassignDocuments();
      expect(component.isReassigning()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/reassign-documents')).flush({});
    });

    it('should set reassignSuccess on success', async () => {
      const { component, httpTesting } = await setup();
      component.selectedUser.set(makeUser({ id: 1 }));
      component.reassignToUserId.set(2);
      component.submitReassignDocuments();
      httpTesting.expectOne((r) => r.url.includes('/reassign-documents')).flush({});
      expect(component.reassignSuccess()).toBe(true);
      expect(component.isReassigning()).toBe(false);
    });

    it('should set reassignError and clear isReassigning on API error', async () => {
      const { component, httpTesting } = await setup();
      component.selectedUser.set(makeUser({ id: 1 }));
      component.reassignToUserId.set(2);
      component.submitReassignDocuments();
      httpTesting.expectOne((r) => r.url.includes('/reassign-documents')).flush(
        { message: 'Reassign error' },
        { status: 500, statusText: 'Internal Server Error' }
      );
      expect(component.reassignError()).toBe('Reassign error');
      expect(component.isReassigning()).toBe(false);
    });

    it('should send correct fromApproverId and toApproverId in request body', async () => {
      const { component, httpTesting } = await setup();
      component.selectedUser.set(makeUser({ id: 3 }));
      component.reassignToUserId.set(7);
      component.submitReassignDocuments();
      const req = httpTesting.expectOne((r) => r.url.includes('/reassign-documents'));
      expect(req.request.body).toEqual({ fromApproverId: 3, toApproverId: 7 });
      req.flush({});
    });
  });

  describe('deactivateUser()', () => {
    it('should update selectedUser.isActive to false on success', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 4, isActive: true });
      component.selectedUser.set(user);
      component.usersResponse.set(makePagedResponse([user]));
      component.deactivateUser(4);
      httpTesting.expectOne((r) => r.url.includes('/users/4/deactivate')).flush({});
      expect(component.selectedUser()?.isActive).toBe(false);
    });

    it('should update the item in usersResponse on success', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 4, isActive: true });
      component.selectedUser.set(user);
      component.usersResponse.set(makePagedResponse([user]));
      component.deactivateUser(4);
      httpTesting.expectOne((r) => r.url.includes('/users/4/deactivate')).flush({});
      expect(component.usersResponse().items[0].isActive).toBe(false);
    });

    it('should alert and reset isLoading on error', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.deactivateUser(4);
      httpTesting.expectOne((r) => r.url.includes('/users/4/deactivate')).flush(
        { message: 'Cannot deactivate' },
        { status: 400, statusText: 'Bad Request' }
      );
      expect(component.isLoading()).toBe(false);
      alertSpy.mockRestore();
    });

    it('should send a PUT request to the deactivate endpoint', async () => {
      const { component, httpTesting } = await setup();
      component.deactivateUser(4);
      const req = httpTesting.expectOne((r) => r.url.includes('/users/4/deactivate'));
      expect(req.request.method).toBe('PUT');
      req.flush({});
    });
  });

  describe('reactivateUser()', () => {
    it('should update selectedUser.isActive to true on success', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 6, isActive: false });
      component.selectedUser.set(user);
      component.usersResponse.set(makePagedResponse([user]));
      component.reactivateUser(6);
      httpTesting.expectOne((r) => r.url.includes('/users/6/reactivate')).flush({});
      expect(component.selectedUser()?.isActive).toBe(true);
    });

    it('should update the item in usersResponse on success', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 6, isActive: false });
      component.selectedUser.set(user);
      component.usersResponse.set(makePagedResponse([user]));
      component.reactivateUser(6);
      httpTesting.expectOne((r) => r.url.includes('/users/6/reactivate')).flush({});
      expect(component.usersResponse().items[0].isActive).toBe(true);
    });

    it('should alert and reset isLoading on error', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.reactivateUser(6);
      httpTesting.expectOne((r) => r.url.includes('/users/6/reactivate')).flush(
        { message: 'err' },
        { status: 500, statusText: 'Server Error' }
      );
      expect(component.isLoading()).toBe(false);
      alertSpy.mockRestore();
    });
  });

  describe('changeManager()', () => {
    it('should update selectedUser managerId and managerName on success', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 1, managerId: null, managerName: null });
      component.selectedUser.set(user);
      component.usersResponse.set(makePagedResponse([user]));
      component.changeManager(1, 50, 'Carol');
      httpTesting.expectOne((r) => r.url.includes('/change-manager')).flush({});
      expect(component.selectedUser()?.managerId).toBe(50);
      expect(component.selectedUser()?.managerName).toBe('Carol');
    });

    it('should update the item in usersResponse on success', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 1 });
      component.selectedUser.set(user);
      component.usersResponse.set(makePagedResponse([user]));
      component.changeManager(1, 50, 'Carol');
      httpTesting.expectOne((r) => r.url.includes('/change-manager')).flush({});
      expect(component.usersResponse().items[0].managerId).toBe(50);
    });

    it('should alert on error', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.changeManager(1, 50, 'Carol');
      httpTesting.expectOne((r) => r.url.includes('/change-manager')).flush(
        { message: 'err' },
        { status: 500, statusText: 'Server Error' }
      );
      expect(alertSpy).toHaveBeenCalled();
      alertSpy.mockRestore();
    });

    it('should send correct userId and managerId in request body', async () => {
      const { component, httpTesting } = await setup();
      component.changeManager(1, 50, 'Carol');
      const req = httpTesting.expectOne((r) => r.url.includes('/change-manager'));
      expect(req.request.body).toEqual({ userId: 1, managerId: 50 });
      req.flush({});
    });
  });

  describe('changeLevel()', () => {
    it('should update selectedUser level on success', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 1, level: 1 });
      component.selectedUser.set(user);
      component.usersResponse.set(makePagedResponse([user]));
      component.changeLevel(1, 3);
      httpTesting.expectOne((r) => r.url.includes('/change-level')).flush({});
      expect(component.selectedUser()?.level).toBe(3);
    });

    it('should alert on error', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.changeLevel(1, 3);
      httpTesting.expectOne((r) => r.url.includes('/change-level')).flush(
        { message: 'Level change failed' },
        { status: 400, statusText: 'Bad Request' }
      );
      expect(alertSpy).toHaveBeenCalledWith('Error Level change failed');
      alertSpy.mockRestore();
    });

    it('should send correct userId and level in request body', async () => {
      const { component, httpTesting } = await setup();
      component.changeLevel(1, 2);
      const req = httpTesting.expectOne((r) => r.url.includes('/change-level'));
      expect(req.request.body).toEqual({ userId: 1, level: 2 });
      req.flush({});
    });
  });

  describe('changeDepartment()', () => {
    it('should update selectedUser department on success', async () => {
      const { component, httpTesting } = await setup();
      const user = makeUser({ id: 1, departmentId: 10, departmentName: 'Engineering' });
      component.selectedUser.set(user);
      component.usersResponse.set(makePagedResponse([user]));
      component.changeDepartment(1, 20);
      httpTesting.expectOne((r) => r.url.includes('/change-department')).flush({});
      expect(component.selectedUser()?.departmentId).toBe(20);
      expect(component.selectedUser()?.departmentName).toBe('HR');
    });

    it('should alert on error', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.changeDepartment(1, 20);
      httpTesting.expectOne((r) => r.url.includes('/change-department')).flush(
        { message: 'err' },
        { status: 500, statusText: 'Server Error' }
      );
      expect(alertSpy).toHaveBeenCalled();
      alertSpy.mockRestore();
    });
  });

  describe('loadPotentialManagers()', () => {
    it('should clear potentialManagers and not call API when query is empty', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(TestBed.inject(AdminService), 'getPotentialManagers');
      component.loadPotentialManagers(1, '');
      expect(spy).not.toHaveBeenCalled();
      expect(component.potentialManagers()).toHaveLength(0);
      httpTesting.verify();
    });

    it('should fetch potential managers when query is provided', async () => {
      const { component, httpTesting } = await setup();
      component.loadPotentialManagers(1, 'Carol');
      expect(component.isManagersLoading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/potential-managers')).flush([makeUser({ id: 99, name: 'Carol' })]);
      expect(component.potentialManagers()).toHaveLength(1);
      expect(component.isManagersLoading()).toBe(false);
    });

    it('should reset isManagersLoading on error', async () => {
      const { component, httpTesting } = await setup();
      component.loadPotentialManagers(1, 'Carol');
      httpTesting.expectOne((r) => r.url.includes('/potential-managers')).flush(
        { message: 'err' },
        { status: 500, statusText: 'Server Error' }
      );
      expect(component.isManagersLoading()).toBe(false);
    });
  });

  describe('resetManagerSearch()', () => {
    it('should clear managerSearchQuery, close dropdown, and clear potentialManagers', async () => {
      const { component } = await setup();
      component.managerSearchQuery.set('Alice');
      component.isManagerDropdownOpen.set(true);
      component.potentialManagers.set([makeUser()]);
      component.resetManagerSearch();
      expect(component.managerSearchQuery()).toBe('');
      expect(component.isManagerDropdownOpen()).toBe(false);
      expect(component.potentialManagers()).toHaveLength(0);
    });
  });

  describe('openAddUserModal() / closeAddUserModal()', () => {
    it('should open the add-user modal and reset state', async () => {
      const { component } = await setup();
      component.addUserError.set('old error');
      component.addUserSuccess.set(true);
      component.openAddUserModal();
      expect(component.isAddUserModalOpen()).toBe(true);
      expect(component.addUserError()).toBeNull();
      expect(component.addUserSuccess()).toBe(false);
      expect(component.newUser()).toEqual({ name: '', email: '', password: '', departmentId: 0 });
    });

    it('should close the add-user modal and reset state', async () => {
      const { component } = await setup();
      component.openAddUserModal();
      component.addUserError.set('err');
      component.addUserSuccess.set(true);
      component.closeAddUserModal();
      expect(component.isAddUserModalOpen()).toBe(false);
      expect(component.addUserError()).toBeNull();
      expect(component.addUserSuccess()).toBe(false);
    });
  });

  describe('submitAddUser()', () => {
    it('should set addUserError when any required field is missing', async () => {
      const { component } = await setup();
      component.newUser.set({ name: '', email: 'a@b.com', password: 'pass', departmentId: 1 });
      component.submitAddUser();
      expect(component.addUserError()).toBe('All fields are required.');
    });

    it('should set addUserError when departmentId is 0', async () => {
      const { component } = await setup();
      component.newUser.set({ name: 'Jane', email: 'j@x.com', password: 'pass', departmentId: 0 });
      component.submitAddUser();
      expect(component.addUserError()).toBe('All fields are required.');
    });

    it('should set isAddingUser to true while request is in flight', async () => {
      const { component, httpTesting } = await setup();
      component.newUser.set({ name: 'Jane', email: 'j@x.com', password: 'pass123', departmentId: 10 });
      component.submitAddUser();
      expect(component.isAddingUser()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/admin/add-user')).flush({});
    });

    it('should set addUserSuccess to true on success', async () => {
      const { component, httpTesting } = await setup();
      component.newUser.set({ name: 'Jane', email: 'j@x.com', password: 'pass123', departmentId: 10 });
      component.submitAddUser();
      httpTesting.expectOne((r) => r.url.includes('/admin/add-user')).flush({});
      expect(component.addUserSuccess()).toBe(true);
      expect(component.isAddingUser()).toBe(false);
    });

    it('should set addUserError on API failure', async () => {
      const { component, httpTesting } = await setup();
      component.newUser.set({ name: 'Jane', email: 'j@x.com', password: 'pass123', departmentId: 10 });
      component.submitAddUser();
      httpTesting.expectOne((r) => r.url.includes('/admin/add-user')).flush(
        { message: 'Email already exists' },
        { status: 409, statusText: 'Conflict' }
      );
      expect(component.addUserError()).toBe('Email already exists');
      expect(component.isAddingUser()).toBe(false);
    });

    it('should send the correct payload to the API', async () => {
      const { component, httpTesting } = await setup();
      component.newUser.set({ name: 'Jane', email: 'j@x.com', password: 'pass123', departmentId: 10 });
      component.submitAddUser();
      const req = httpTesting.expectOne((r) => r.url.includes('/admin/add-user'));
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ name: 'Jane', email: 'j@x.com', password: 'pass123', departmentId: 10 });
      req.flush({});
    });
  });

  describe('fetchUsersForReassign()', () => {
    it('should clear allUsersList and not call API when query is empty', async () => {
      const { component, httpTesting } = await setup();
      component.fetchUsersForReassign('');
      expect(component.allUsersList()).toHaveLength(0);
      httpTesting.verify();
    });

    it('should populate allUsersList on success', async () => {
      const { component, httpTesting } = await setup();
      component.fetchUsersForReassign('Bob');
      httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush(makePagedResponse([makeUser({ id: 2, name: 'Bob' })]));
      expect(component.allUsersList()).toHaveLength(1);
      expect(component.isReassignSearchLoading()).toBe(false);
    });

    it('should reset isReassignSearchLoading on error', async () => {
      const { component, httpTesting } = await setup();
      component.fetchUsersForReassign('Bob');
      httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush(
        { message: 'err' },
        { status: 500, statusText: 'Server Error' }
      );
      expect(component.isReassignSearchLoading()).toBe(false);
    });
  });

  describe('closeBulkResultModal()', () => {
    it('should close the bulk result modal and clear the result', async () => {
      const { component } = await setup();
      component.isBulkResultModalOpen.set(true);
      component.bulkUploadResult.set({ totalRows: 5, successCount: 4, failureCount: 1, results: [] });
      component.closeBulkResultModal();
      expect(component.isBulkResultModalOpen()).toBe(false);
      expect(component.bulkUploadResult()).toBeNull();
    });
  });

  describe('onBulkUploadFileChange()', () => {
    it('should alert and not upload when a non-CSV file is selected', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      const file = new File(['data'], 'report.pdf', { type: 'application/pdf' });
      const input = document.createElement('input') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: [file] });
      const event = { target: input } as unknown as Event;
      component.onBulkUploadFileChange(event);
      expect(alertSpy).toHaveBeenCalledWith('Please upload a valid CSV file.');
      httpTesting.verify();
      alertSpy.mockRestore();
    });

    it('should upload and set bulkUploadResult on success with a CSV file', async () => {
      const { component, httpTesting } = await setup();
      const file = new File(['name,email\nJane,j@x.com'], 'users.csv', { type: 'text/csv' });
      const input = document.createElement('input') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: [file] });
      const event = { target: input } as unknown as Event;
      const bulkResult = { totalRows: 1, successCount: 1, failureCount: 0, results: [] };
      component.onBulkUploadFileChange(event);
      expect(component.isBulkUploading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/bulk-upload')).flush(bulkResult);
      httpTesting.expectOne((r) => r.url.includes('/admin/users')).flush(makePagedResponse());
      expect(component.bulkUploadResult()).toEqual(bulkResult);
      expect(component.isBulkResultModalOpen()).toBe(true);
      expect(component.isBulkUploading()).toBe(false);
    });

    it('should alert and reset isBulkUploading on upload error', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      const file = new File(['data'], 'users.csv', { type: 'text/csv' });
      const input = document.createElement('input') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: [file] });
      const event = { target: input } as unknown as Event;
      component.onBulkUploadFileChange(event);
      httpTesting.expectOne((r) => r.url.includes('/bulk-upload')).flush(
        { message: 'Upload failed' },
        { status: 500, statusText: 'Server Error' }
      );
      expect(component.isBulkUploading()).toBe(false);
      alertSpy.mockRestore();
    });
  });
});
