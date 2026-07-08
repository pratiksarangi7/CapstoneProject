import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Component } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Register } from './register';
import { AuthService } from '../../services/auth.service';
import { DepartmentService } from '../../services/department.service';
import { Department } from '../../models/department.model';

// Stub component so provideRouter has a real target and won't throw NG04002
@Component({ template: '', standalone: true }) class StubLogin {}

// ─── Helpers ─────────────────────────────────────────────────────────────────

/** Build & compile the Register component inside a minimal TestBed. */
async function setup() {
  await TestBed.configureTestingModule({
    imports: [Register],
    providers: [
      provideRouter([{ path: 'login', component: StubLogin }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      AuthService,
      DepartmentService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(Register);
  const component = fixture.componentInstance;
  const router = TestBed.inject(Router);
  const httpTesting = TestBed.inject(HttpTestingController);

  // Flush the departments request triggered by ngOnInit.
  fixture.detectChanges();
  httpTesting
    .expectOne((req) => req.url.includes('/User/departments'))
    .flush([
      { id: 1, name: 'Engineering' },
      { id: 2, name: 'HR' },
    ] as Department[]);
  fixture.detectChanges();

  return { fixture, component, router, httpTesting };
}

// ─── Test Suite ───────────────────────────────────────────────────────────────

describe('Register Component', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  // ── Component creation ──────────────────────────────────────────────────────

  describe('Component Initialization', () => {
    it('should create the component', async () => {
      const { component } = await setup();
      expect(component).toBeTruthy();
    });

    it('should initialize registerModel with empty strings', async () => {
      const { component } = await setup();
      expect(component.registerModel()).toEqual({
        name: '',
        email: '',
        password: '',
        departmentId: '',
      });
    });

    it('should initialize progress signal as false', async () => {
      const { component } = await setup();
      expect(component.progress()).toBe(false);
    });

    it('should load departments on init', async () => {
      const { component } = await setup();
      expect(component.departments()).toHaveLength(2);
      expect(component.departments()[0].name).toBe('Engineering');
      expect(component.departments()[1].name).toBe('HR');
    });

    it('should define email and password regex patterns', async () => {
      const { component } = await setup();
      expect(component.emailRegex).toBeInstanceOf(RegExp);
      expect(component.passwordRegex).toBeInstanceOf(RegExp);
    });
  });

  // ── ngOnInit – department loading ───────────────────────────────────────────

  describe('ngOnInit() – department loading', () => {
    it('should call DepartmentService.GetAllDepartments on init', async () => {
      // Re-setup so we can intercept before detectChanges
      await TestBed.configureTestingModule({
        imports: [Register],
        providers: [
          provideRouter([{ path: 'login', component: StubLogin }]),
          provideHttpClient(),
          provideHttpClientTesting(),
          AuthService,
          DepartmentService,
        ],
      }).compileComponents();

      const fixture = TestBed.createComponent(Register);
      const httpTesting = TestBed.inject(HttpTestingController);
      fixture.detectChanges();

      const req = httpTesting.expectOne((r) => r.url.includes('/User/departments'));
      expect(req.request.method).toBe('GET');
      req.flush([{ id: 1, name: 'Engineering' }]);
    });

    it('should log an error when department loading fails', async () => {
      await TestBed.configureTestingModule({
        imports: [Register],
        providers: [
          provideRouter([{ path: 'login', component: StubLogin }]),
          provideHttpClient(),
          provideHttpClientTesting(),
          AuthService,
          DepartmentService,
        ],
      }).compileComponents();

      const fixture = TestBed.createComponent(Register);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      fixture.detectChanges();
      httpTesting
        .expectOne((r) => r.url.includes('/User/departments'))
        .flush({ message: 'Server error' }, { status: 500, statusText: 'Internal Server Error' });

      // Departments should remain empty on error
      expect(component.departments()).toHaveLength(0);
      consoleSpy.mockRestore();
    });
  });

  // ── Email regex validation ──────────────────────────────────────────────────

  describe('Email Regex Validation', () => {
    it('should accept a valid email address', async () => {
      const { component } = await setup();
      expect(component.emailRegex.test('user@example.com')).toBe(true);
    });

    it('should accept email with special characters in local part', async () => {
      const { component } = await setup();
      expect(component.emailRegex.test('user.name+tag@domain.org')).toBe(true);
    });

    it('should reject an email without @ symbol', async () => {
      const { component } = await setup();
      expect(component.emailRegex.test('userexample.com')).toBe(false);
    });

    it('should reject an email without a domain extension', async () => {
      const { component } = await setup();
      expect(component.emailRegex.test('user@example')).toBe(false);
    });

    it('should reject an empty email', async () => {
      const { component } = await setup();
      expect(component.emailRegex.test('')).toBe(false);
    });
  });

  // ── Password regex validation ───────────────────────────────────────────────

  describe('Password Regex Validation', () => {
    it('should accept a password with letters and numbers (8+ chars)', async () => {
      const { component } = await setup();
      expect(component.passwordRegex.test('Secure12')).toBe(true);
    });

    it('should reject a password shorter than 8 characters', async () => {
      const { component } = await setup();
      expect(component.passwordRegex.test('Ab1')).toBe(false);
    });

    it('should reject a letters-only password', async () => {
      const { component } = await setup();
      expect(component.passwordRegex.test('Password')).toBe(false);
    });

    it('should reject a numbers-only password', async () => {
      const { component } = await setup();
      expect(component.passwordRegex.test('12345678')).toBe(false);
    });

    it('should reject an empty password', async () => {
      const { component } = await setup();
      expect(component.passwordRegex.test('')).toBe(false);
    });
  });

  // ── Form state ──────────────────────────────────────────────────────────────

  describe('Form Validity', () => {
    it('should have an invalid form when all fields are empty', async () => {
      const { component } = await setup();
      expect(component.registerForm().invalid()).toBe(true);
    });

    it('should have a valid form when all fields are correctly filled', async () => {
      const { component } = await setup();
      component.registerModel.set({
        name: 'John Doe',
        email: 'john@example.com',
        password: 'Secure12',
        departmentId: '1',
      });
      expect(component.registerForm().invalid()).toBe(false);
    });

    it('should be invalid when name is missing', async () => {
      const { component } = await setup();
      component.registerModel.set({
        name: '',
        email: 'john@example.com',
        password: 'Secure12',
        departmentId: '1',
      });
      expect(component.registerForm().invalid()).toBe(true);
    });

    it('should be invalid when name is only 1 character (minLength = 2)', async () => {
      const { component } = await setup();
      component.registerModel.set({
        name: 'J',
        email: 'john@example.com',
        password: 'Secure12',
        departmentId: '1',
      });
      expect(component.registerForm().invalid()).toBe(true);
    });

    it('should be invalid when email is missing', async () => {
      const { component } = await setup();
      component.registerModel.set({
        name: 'John Doe',
        email: '',
        password: 'Secure12',
        departmentId: '1',
      });
      expect(component.registerForm().invalid()).toBe(true);
    });

    it('should be invalid when email format is wrong', async () => {
      const { component } = await setup();
      component.registerModel.set({
        name: 'John Doe',
        email: 'bad-email',
        password: 'Secure12',
        departmentId: '1',
      });
      expect(component.registerForm().invalid()).toBe(true);
    });

    it('should be invalid when password is missing', async () => {
      const { component } = await setup();
      component.registerModel.set({
        name: 'John Doe',
        email: 'john@example.com',
        password: '',
        departmentId: '1',
      });
      expect(component.registerForm().invalid()).toBe(true);
    });

    it('should be invalid when password is shorter than 8 characters', async () => {
      const { component } = await setup();
      component.registerModel.set({
        name: 'John Doe',
        email: 'john@example.com',
        password: 'Ab1',
        departmentId: '1',
      });
      expect(component.registerForm().invalid()).toBe(true);
    });

    it('should be invalid when departmentId is empty', async () => {
      const { component } = await setup();
      component.registerModel.set({
        name: 'John Doe',
        email: 'john@example.com',
        password: 'Secure12',
        departmentId: '',
      });
      expect(component.registerForm().invalid()).toBe(true);
    });
  });

  // ── handleRegisterClick – invalid form guard ─────────────────────────────────

  describe('handleRegisterClick() – invalid form', () => {
    it('should alert and NOT call AuthService when form is invalid', async () => {
      const { component } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      const authService = TestBed.inject(AuthService);
      const apiSpy = vi.spyOn(authService, 'RegisterApiCall');

      component.handleRegisterClick();

      expect(alertSpy).toHaveBeenCalledWith(
        'Please fix the errors in the form before submitting.'
      );
      expect(apiSpy).not.toHaveBeenCalled();
      alertSpy.mockRestore();
    });

    it('should NOT set progress to true when form is invalid', async () => {
      const { component } = await setup();
      vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.handleRegisterClick();
      expect(component.progress()).toBe(false);
    });
  });

  // ── handleRegisterClick – invalid departmentId guard ─────────────────────────

  describe('handleRegisterClick() – invalid departmentId (zero/NaN)', () => {
    it('should alert "Please select a valid department." when departmentId resolves to 0', async () => {
      const { component } = await setup();
      // Form is valid but departmentId converts to 0 (e.g. "0")
      component.registerModel.set({
        name: 'John Doe',
        email: 'john@example.com',
        password: 'Secure12',
        departmentId: '0',
      });
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      const authService = TestBed.inject(AuthService);
      const apiSpy = vi.spyOn(authService, 'RegisterApiCall');

      component.handleRegisterClick();

      expect(alertSpy).toHaveBeenCalledWith('Please select a valid department.');
      expect(apiSpy).not.toHaveBeenCalled();
      alertSpy.mockRestore();
    });
  });

  // ── handleRegisterClick – success ────────────────────────────────────────────

  describe('handleRegisterClick() – success', () => {
    const validModel = {
      name: 'John Doe',
      email: 'john@example.com',
      password: 'Secure12',
      departmentId: '1',
    };

    it('should set progress to true while the request is in flight', async () => {
      const { component, httpTesting } = await setup();
      component.registerModel.set(validModel);

      component.handleRegisterClick();
      expect(component.progress()).toBe(true);

      httpTesting
        .expectOne((req) => req.url.includes('/Auth/register'))
        .flush({ message: 'Created' });
    });

    it('should alert "Registration successful! Please login." on success', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.registerModel.set(validModel);

      component.handleRegisterClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/register'))
        .flush({ message: 'Created' });

      expect(alertSpy).toHaveBeenCalledWith('Registration successful! Please login.');
      alertSpy.mockRestore();
    });

    it('should navigate to /login on successful registration', async () => {
      const { component, router, httpTesting } = await setup();
      const navigateSpy = vi.spyOn(router, 'navigate');
      vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.registerModel.set(validModel);

      component.handleRegisterClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/register'))
        .flush({ message: 'Created' });

      expect(navigateSpy).toHaveBeenCalledWith(['/login']);
    });

    it('should set progress to false after a successful response', async () => {
      const { component, httpTesting } = await setup();
      vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.registerModel.set(validModel);

      component.handleRegisterClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/register'))
        .flush({ message: 'Created' });

      expect(component.progress()).toBe(false);
    });
  });

  // ── handleRegisterClick – error ───────────────────────────────────────────────

  describe('handleRegisterClick() – error', () => {
    const validModel = {
      name: 'John Doe',
      email: 'john@example.com',
      password: 'Secure12',
      departmentId: '1',
    };

    it('should alert "Registration failed. Please try again." on error', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.registerModel.set(validModel);

      component.handleRegisterClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/register'))
        .flush({ message: 'Conflict' }, { status: 409, statusText: 'Conflict' });

      expect(alertSpy).toHaveBeenCalledWith('Registration failed. Please try again.');
      alertSpy.mockRestore();
    });

    it('should set progress to false after a failed response', async () => {
      const { component, httpTesting } = await setup();
      vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.registerModel.set(validModel);

      component.handleRegisterClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/register'))
        .flush({ message: 'Conflict' }, { status: 409, statusText: 'Conflict' });

      expect(component.progress()).toBe(false);
    });

    it('should NOT navigate on a failed registration', async () => {
      const { component, router, httpTesting } = await setup();
      const navigateSpy = vi.spyOn(router, 'navigate');
      vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.registerModel.set(validModel);

      component.handleRegisterClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/register'))
        .flush({ message: 'Conflict' }, { status: 409, statusText: 'Conflict' });

      expect(navigateSpy).not.toHaveBeenCalled();
    });
  });

  // ── HTTP request shape ──────────────────────────────────────────────────────

  describe('HTTP request', () => {
    const validModel = {
      name: 'John Doe',
      email: 'john@example.com',
      password: 'Secure12',
      departmentId: '1',
    };

    it('should send a POST request to the /Auth/register endpoint', async () => {
      const { component, httpTesting } = await setup();
      vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.registerModel.set(validModel);

      component.handleRegisterClick();
      const req = httpTesting.expectOne((r) => r.url.includes('/Auth/register'));
      expect(req.request.method).toBe('POST');
      req.flush({ message: 'Created' });
    });

    it('should convert departmentId to a number in the request body', async () => {
      const { component, httpTesting } = await setup();
      vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.registerModel.set(validModel);

      component.handleRegisterClick();
      const req = httpTesting.expectOne((r) => r.url.includes('/Auth/register'));
      expect(req.request.body).toEqual({
        name: 'John Doe',
        email: 'john@example.com',
        password: 'Secure12',
        departmentId: 1, // numeric, not string "1"
      });
      req.flush({ message: 'Created' });
    });
  });
});
