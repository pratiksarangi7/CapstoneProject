import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Component } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Login } from './login';
import { AuthService } from '../../services/auth.service';

@Component({ template: '', standalone: true }) class StubAdminDashboard {}
@Component({ template: '', standalone: true }) class StubUserDashboard {}


async function setup() {
  await TestBed.configureTestingModule({
    imports: [Login],
    providers: [
      provideRouter([
        { path: 'admin-dashboard', component: StubAdminDashboard },
        { path: 'user-dashboard',  component: StubUserDashboard  },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      AuthService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(Login);
  const component = fixture.componentInstance;
  const router = TestBed.inject(Router);
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();
  return { fixture, component, router, httpTesting };
}


describe('Login Component', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });


  describe('Component Initialization', () => {
    it('should create the component', async () => {
      const { component } = await setup();
      expect(component).toBeTruthy();
    });

    it('should initialize loginModel with empty strings', async () => {
      const { component } = await setup();
      expect(component.loginModel()).toEqual({ email: '', password: '' });
    });

    it('should initialize progress signal as false', async () => {
      const { component } = await setup();
      expect(component.progress()).toBe(false);
    });

    it('should define email and password regex patterns', async () => {
      const { component } = await setup();
      expect(component.emailRegex).toBeInstanceOf(RegExp);
      expect(component.passwordRegex).toBeInstanceOf(RegExp);
    });
  });


  describe('Email Regex Validation', () => {
    it('should accept a valid email address', async () => {
      const { component } = await setup();
      expect(component.emailRegex.test('user@example.com')).toBe(true);
    });

    it('should accept email with sub-domain', async () => {
      const { component } = await setup();
      expect(component.emailRegex.test('user@mail.example.co')).toBe(true);
    });

    it('should reject an email without @ symbol', async () => {
      const { component } = await setup();
      expect(component.emailRegex.test('userexample.com')).toBe(false);
    });

    it('should reject an email without domain extension', async () => {
      const { component } = await setup();
      expect(component.emailRegex.test('user@example')).toBe(false);
    });

    it('should reject an empty string as email', async () => {
      const { component } = await setup();
      expect(component.emailRegex.test('')).toBe(false);
    });
  });


  describe('Password Regex Validation', () => {
    it('should accept a password with letters and numbers (8+ chars)', async () => {
      const { component } = await setup();
      expect(component.passwordRegex.test('Pass1234')).toBe(true);
    });

    it('should reject a password shorter than 8 characters', async () => {
      const { component } = await setup();
      expect(component.passwordRegex.test('Ab1')).toBe(false);
    });

    it('should reject a password with only letters', async () => {
      const { component } = await setup();
      expect(component.passwordRegex.test('Password')).toBe(false);
    });

    it('should reject a password with only numbers', async () => {
      const { component } = await setup();
      expect(component.passwordRegex.test('12345678')).toBe(false);
    });

    it('should reject an empty password', async () => {
      const { component } = await setup();
      expect(component.passwordRegex.test('')).toBe(false);
    });
  });


  describe('Form Validity', () => {
    it('should have an invalid form when model is empty', async () => {
      const { component } = await setup();
      expect(component.loginForm().invalid()).toBe(true);
    });

    it('should have a valid form when email and password are correct', async () => {
      const { component } = await setup();
      component.loginModel.set({ email: 'test@example.com', password: 'Valid1234' });
      expect(component.loginForm().invalid()).toBe(false);
    });

    it('should have an invalid form when only email is set', async () => {
      const { component } = await setup();
      component.loginModel.set({ email: 'test@example.com', password: '' });
      expect(component.loginForm().invalid()).toBe(true);
    });

    it('should have an invalid form when only password is set', async () => {
      const { component } = await setup();
      component.loginModel.set({ email: '', password: 'Valid1234' });
      expect(component.loginForm().invalid()).toBe(true);
    });

    it('should have an invalid form with a bad email format', async () => {
      const { component } = await setup();
      component.loginModel.set({ email: 'not-an-email', password: 'Valid1234' });
      expect(component.loginForm().invalid()).toBe(true);
    });

    it('should have an invalid form with a weak password', async () => {
      const { component } = await setup();
      component.loginModel.set({ email: 'test@example.com', password: 'short' });
      expect(component.loginForm().invalid()).toBe(true);
    });
  });


  describe('handleLoginClick() – invalid form', () => {
    it('should alert and NOT call AuthService when form is invalid', async () => {
      const { component } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      const authService = TestBed.inject(AuthService);
      const apiSpy = vi.spyOn(authService, 'LoginApiCall');

      component.handleLoginClick();

      expect(alertSpy).toHaveBeenCalledWith(
        'Please fix the errors in the form before submitting.'
      );
      expect(apiSpy).not.toHaveBeenCalled();
      alertSpy.mockRestore();
    });

    it('should NOT set progress to true when form is invalid', async () => {
      const { component } = await setup();
      vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.handleLoginClick();
      expect(component.progress()).toBe(false);
    });
  });


  describe('handleLoginClick() – success', () => {
    it('should set progress to true while the request is in flight', async () => {
      const { component, httpTesting } = await setup();
      component.loginModel.set({ email: 'admin@example.com', password: 'Admin1234' });

      component.handleLoginClick();
      expect(component.progress()).toBe(true);

      
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/login'))
        .flush({ token: 'fake.jwt.token', name: 'Admin User' });
    });

    it('should store token and name in localStorage on success', async () => {
      const { component, httpTesting } = await setup();
      component.loginModel.set({ email: 'user@example.com', password: 'Pass1234' });

      component.handleLoginClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/login'))
        .flush({ token: 'my.jwt.token', name: 'Test User' });

      expect(localStorage.getItem('token')).toBe('my.jwt.token');
      expect(localStorage.getItem('name')).toBe('Test User');
    });

    it('should call authService.updateUserName with the returned name', async () => {
      const { component, httpTesting } = await setup();
      const authService = TestBed.inject(AuthService);
      const updateSpy = vi.spyOn(authService, 'updateUserName');

      component.loginModel.set({ email: 'user@example.com', password: 'Pass1234' });
      component.handleLoginClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/login'))
        .flush({ token: 'some.token', name: 'Jane Doe' });

      expect(updateSpy).toHaveBeenCalledWith('Jane Doe');
    });

    it('should set progress to false after a successful response', async () => {
      const { component, httpTesting } = await setup();
      component.loginModel.set({ email: 'user@example.com', password: 'Pass1234' });

      component.handleLoginClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/login'))
        .flush({ token: 'tok', name: 'User' });

      expect(component.progress()).toBe(false);
    });

    it('should navigate to /admin-dashboard when the user has the Admin role', async () => {
      const { component, router, httpTesting } = await setup();
      const navigateSpy = vi.spyOn(router, 'navigate');

      const payload = {
        exp: Math.floor(Date.now() / 1000) + 3600,
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Admin',
      };
      const fakeToken = `header.${btoa(JSON.stringify(payload))}.sig`;

      component.loginModel.set({ email: 'admin@example.com', password: 'Admin1234' });
      component.handleLoginClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/login'))
        .flush({ token: fakeToken, name: 'Admin User' });

      expect(navigateSpy).toHaveBeenCalledWith(['/admin-dashboard']);
    });

    it('should navigate to /user-dashboard when the user has a non-Admin role', async () => {
      const { component, router, httpTesting } = await setup();
      const navigateSpy = vi.spyOn(router, 'navigate');

      const payload = {
        exp: Math.floor(Date.now() / 1000) + 3600,
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'User',
      };
      const fakeToken = `header.${btoa(JSON.stringify(payload))}.sig`;

      component.loginModel.set({ email: 'user@example.com', password: 'Pass1234' });
      component.handleLoginClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/login'))
        .flush({ token: fakeToken, name: 'Regular User' });

      expect(navigateSpy).toHaveBeenCalledWith(['/user-dashboard']);
    });
  });


  describe('handleLoginClick() – error', () => {
    it('should show an alert with the server error message on failure', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});

      component.loginModel.set({ email: 'user@example.com', password: 'Pass1234' });
      component.handleLoginClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/login'))
        .flush({ message: 'Invalid credentials' }, { status: 401, statusText: 'Unauthorized' });

      expect(alertSpy).toHaveBeenCalledWith('Invalid credentials');
      alertSpy.mockRestore();
    });

    it('should set progress to false after a failed response', async () => {
      const { component, httpTesting } = await setup();
      vi.spyOn(window, 'alert').mockImplementation(() => {});

      component.loginModel.set({ email: 'user@example.com', password: 'Pass1234' });
      component.handleLoginClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/login'))
        .flush({ message: 'Server error' }, { status: 500, statusText: 'Internal Server Error' });

      expect(component.progress()).toBe(false);
    });

    it('should NOT navigate on a failed login', async () => {
      const { component, router, httpTesting } = await setup();
      const navigateSpy = vi.spyOn(router, 'navigate');
      vi.spyOn(window, 'alert').mockImplementation(() => {});

      component.loginModel.set({ email: 'user@example.com', password: 'Pass1234' });
      component.handleLoginClick();
      httpTesting
        .expectOne((req) => req.url.includes('/Auth/login'))
        .flush({ message: 'Invalid credentials' }, { status: 401, statusText: 'Unauthorized' });

      expect(navigateSpy).not.toHaveBeenCalled();
    });
  });

  describe('HTTP request', () => {
    it('should send a POST request to the /Auth/login endpoint', async () => {
      const { component, httpTesting } = await setup();

      component.loginModel.set({ email: 'test@example.com', password: 'MyPass12' });
      component.handleLoginClick();

      const req = httpTesting.expectOne((r) => r.url.includes('/Auth/login'));
      expect(req.request.method).toBe('POST');
      req.flush({ token: 'tok', name: 'Tester' });
    });

    it('should include email and password in the request body', async () => {
      const { component, httpTesting } = await setup();

      component.loginModel.set({ email: 'test@example.com', password: 'MyPass12' });
      component.handleLoginClick();

      const req = httpTesting.expectOne((r) => r.url.includes('/Auth/login'));
      expect(req.request.body).toEqual({ email: 'test@example.com', password: 'MyPass12' });
      req.flush({ token: 'tok', name: 'Tester' });
    });
  });
});
