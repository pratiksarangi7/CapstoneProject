import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AdminDepartments } from './admin-departments';
import { AdminService } from '../../services/admin.service';
import { DepartmentResponseDto, DepartmentUserDto } from '../../dtos/department.response.dto';

const makeDeptUser = (overrides: Partial<DepartmentUserDto> = {}): DepartmentUserDto => ({
  id: 1,
  name: 'Alice',
  email: 'alice@example.com',
  isAdmin: false,
  managerId: null,
  managerName: null,
  level: 1,
  ...overrides,
});

const makeDept = (overrides: Partial<DepartmentResponseDto> = {}): DepartmentResponseDto => ({
  id: 10,
  name: 'Engineering',
  users: [],
  ...overrides,
});

async function setup() {
  await TestBed.configureTestingModule({
    imports: [AdminDepartments],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      AdminService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(AdminDepartments);
  const component = fixture.componentInstance;
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();
  httpTesting
    .expectOne((r) => r.url.includes('/Admin/departments'))
    .flush([makeDept(), makeDept({ id: 20, name: 'HR' })]);
  fixture.detectChanges();

  return { fixture, component, httpTesting };
}

describe('AdminDepartments Component', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  describe('Component Initialization', () => {
    it('should create the component', async () => {
      const { component } = await setup();
      expect(component).toBeTruthy();
    });

    it('should initialize isLoading to false after data loads', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should initialize openDeptId to null', async () => {
      const { component } = await setup();
      expect(component.openDeptId()).toBeNull();
    });

    it('should initialize isModalOpen to false', async () => {
      const { component } = await setup();
      expect(component.isModalOpen()).toBe(false);
    });

    it('should initialize newDeptName to empty string', async () => {
      const { component } = await setup();
      expect(component.newDeptName()).toBe('');
    });

    it('should initialize createError to null', async () => {
      const { component } = await setup();
      expect(component.createError()).toBeNull();
    });

    it('should initialize createSuccess to null', async () => {
      const { component } = await setup();
      expect(component.createSuccess()).toBeNull();
    });

    it('should load departments on init', async () => {
      const { component } = await setup();
      expect(component.departments()).toHaveLength(2);
      expect(component.departments()[0].name).toBe('Engineering');
    });
  });

  describe('loadDepartments()', () => {
    it('should set isLoading to true while fetching', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminDepartments],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminDepartments);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      expect(component.isLoading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/Admin/departments')).flush([]);
    });

    it('should set isLoading to false after success', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should set isLoading to false after error', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminDepartments],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminDepartments);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      httpTesting
        .expectOne((r) => r.url.includes('/Admin/departments'))
        .flush({ message: 'err' }, { status: 500, statusText: 'Server Error' });

      expect(component.isLoading()).toBe(false);
    });

    it('should populate departments signal with returned data', async () => {
      const { component } = await setup();
      expect(component.departments()[1].name).toBe('HR');
    });

    it('should send a GET request to the departments endpoint', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminDepartments],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminDepartments);
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      const req = httpTesting.expectOne((r) => r.url.includes('/Admin/departments'));
      expect(req.request.method).toBe('GET');
      req.flush([]);
    });
  });

  describe('toggleDept()', () => {
    it('should open a department by setting its id', async () => {
      const { component } = await setup();
      component.toggleDept(10);
      expect(component.openDeptId()).toBe(10);
    });

    it('should collapse the department when toggled again', async () => {
      const { component } = await setup();
      component.toggleDept(10);
      component.toggleDept(10);
      expect(component.openDeptId()).toBeNull();
    });

    it('should switch open department when toggling a different one', async () => {
      const { component } = await setup();
      component.toggleDept(10);
      component.toggleDept(20);
      expect(component.openDeptId()).toBe(20);
    });
  });

  describe('isDeptOpen()', () => {
    it('should return true when the department id matches openDeptId', async () => {
      const { component } = await setup();
      component.openDeptId.set(10);
      expect(component.isDeptOpen(10)).toBe(true);
    });

    it('should return false when the department id does not match', async () => {
      const { component } = await setup();
      component.openDeptId.set(10);
      expect(component.isDeptOpen(20)).toBe(false);
    });

    it('should return false when openDeptId is null', async () => {
      const { component } = await setup();
      expect(component.isDeptOpen(10)).toBe(false);
    });
  });

  describe('openCreateModal()', () => {
    it('should set isModalOpen to true', async () => {
      const { component } = await setup();
      component.openCreateModal();
      expect(component.isModalOpen()).toBe(true);
    });

    it('should reset newDeptName to empty string', async () => {
      const { component } = await setup();
      component.newDeptName.set('Old Name');
      component.openCreateModal();
      expect(component.newDeptName()).toBe('');
    });

    it('should reset createError to null', async () => {
      const { component } = await setup();
      component.createError.set('some error');
      component.openCreateModal();
      expect(component.createError()).toBeNull();
    });

    it('should reset createSuccess to null', async () => {
      const { component } = await setup();
      component.createSuccess.set('some success');
      component.openCreateModal();
      expect(component.createSuccess()).toBeNull();
    });
  });

  describe('closeCreateModal()', () => {
    it('should set isModalOpen to false', async () => {
      const { component } = await setup();
      component.openCreateModal();
      component.closeCreateModal();
      expect(component.isModalOpen()).toBe(false);
    });

    it('should clear newDeptName', async () => {
      const { component } = await setup();
      component.openCreateModal();
      component.newDeptName.set('Test');
      component.closeCreateModal();
      expect(component.newDeptName()).toBe('');
    });

    it('should clear createError and createSuccess', async () => {
      const { component } = await setup();
      component.openCreateModal();
      component.createError.set('err');
      component.createSuccess.set('ok');
      component.closeCreateModal();
      expect(component.createError()).toBeNull();
      expect(component.createSuccess()).toBeNull();
    });

    it('should NOT close the modal while isCreating is true', async () => {
      const { component } = await setup();
      component.openCreateModal();
      component.isCreating.set(true);
      component.closeCreateModal();
      expect(component.isModalOpen()).toBe(true);
    });
  });

  describe('submitCreateDepartment()', () => {
    it('should set createError when name is empty', async () => {
      const { component } = await setup();
      component.newDeptName.set('');
      component.submitCreateDepartment();
      expect(component.createError()).toBe('Department name cannot be empty.');
    });

    it('should set createError when name is only whitespace', async () => {
      const { component } = await setup();
      component.newDeptName.set('   ');
      component.submitCreateDepartment();
      expect(component.createError()).toBe('Department name cannot be empty.');
    });

    it('should NOT call the API when name is empty', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(TestBed.inject(AdminService), 'createNewDepartments');
      component.newDeptName.set('');
      component.submitCreateDepartment();
      expect(spy).not.toHaveBeenCalled();
      httpTesting.verify();
    });

    it('should set isCreating to true while request is in flight', async () => {
      const { component, httpTesting } = await setup();
      component.newDeptName.set('Finance');
      component.submitCreateDepartment();
      expect(component.isCreating()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/admin/department')).flush({});
      httpTesting.expectOne((r) => r.url.includes('/Admin/departments')).flush([]);
    });

    it('should set createSuccess with the department name on success', async () => {
      const { component, httpTesting } = await setup();
      component.newDeptName.set('Finance');
      component.submitCreateDepartment();
      httpTesting.expectOne((r) => r.url.includes('/admin/department')).flush({});
      httpTesting.expectOne((r) => r.url.includes('/Admin/departments')).flush([]);
      expect(component.createSuccess()).toBe('Department "Finance" created successfully!');
    });

    it('should set isCreating to false on success', async () => {
      const { component, httpTesting } = await setup();
      component.newDeptName.set('Finance');
      component.submitCreateDepartment();
      httpTesting.expectOne((r) => r.url.includes('/admin/department')).flush({});
      httpTesting.expectOne((r) => r.url.includes('/Admin/departments')).flush([]);
      expect(component.isCreating()).toBe(false);
    });

    it('should refresh the department list on success', async () => {
      const { component, httpTesting } = await setup();
      component.newDeptName.set('Finance');
      component.submitCreateDepartment();
      httpTesting.expectOne((r) => r.url.includes('/admin/department')).flush({});
      const refreshReq = httpTesting.expectOne((r) => r.url.includes('/Admin/departments'));
      expect(refreshReq.request.method).toBe('GET');
      refreshReq.flush([makeDept(), makeDept({ id: 30, name: 'Finance' })]);
      expect(component.departments()).toHaveLength(2);
    });

    it('should set createError on API failure', async () => {
      const { component, httpTesting } = await setup();
      component.newDeptName.set('Finance');
      component.submitCreateDepartment();
      httpTesting
        .expectOne((r) => r.url.includes('/admin/department'))
        .flush({ message: 'Already exists' }, { status: 409, statusText: 'Conflict' });
      expect(component.createError()).toBe('Already exists');
    });

    it('should fallback to a default error message when the response has none', async () => {
      const { component, httpTesting } = await setup();
      component.newDeptName.set('Finance');
      component.submitCreateDepartment();
      httpTesting
        .expectOne((r) => r.url.includes('/admin/department'))
        .flush({}, { status: 500, statusText: 'Server Error' });
      expect(component.createError()).toBe('Failed to create department. Please try again.');
    });

    it('should set isCreating to false on API failure', async () => {
      const { component, httpTesting } = await setup();
      component.newDeptName.set('Finance');
      component.submitCreateDepartment();
      httpTesting
        .expectOne((r) => r.url.includes('/admin/department'))
        .flush({ message: 'err' }, { status: 500, statusText: 'Server Error' });
      expect(component.isCreating()).toBe(false);
    });

    it('should send the department name in the POST body', async () => {
      const { component, httpTesting } = await setup();
      component.newDeptName.set('  Legal  ');
      component.submitCreateDepartment();
      const req = httpTesting.expectOne((r) => r.url.includes('/admin/department'));
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ name: 'Legal' });
      req.flush({});
      httpTesting.expectOne((r) => r.url.includes('/Admin/departments')).flush([]);
    });
  });

  describe('getInitials()', () => {
    it('should return the first letter of a single-word name uppercased', async () => {
      const { component } = await setup();
      expect(component.getInitials('Engineering')).toBe('E');
    });

    it('should return the first letter of each word for a two-word name', async () => {
      const { component } = await setup();
      expect(component.getInitials('Human Resources')).toBe('HR');
    });

    it('should return at most 2 characters for a three-word name', async () => {
      const { component } = await setup();
      expect(component.getInitials('Research And Development')).toBe('RA');
    });

    it('should return uppercase initials', async () => {
      const { component } = await setup();
      expect(component.getInitials('finance')).toBe('F');
    });
  });

  describe('getLevelLabel()', () => {
    it('should return "Level 1" for input 1', async () => {
      const { component } = await setup();
      expect(component.getLevelLabel(1)).toBe('Level 1');
    });

    it('should return "Level 3" for input 3', async () => {
      const { component } = await setup();
      expect(component.getLevelLabel(3)).toBe('Level 3');
    });
  });
});
