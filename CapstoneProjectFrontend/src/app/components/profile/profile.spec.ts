import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Profile } from './profile';
import { UserService } from '../../services/user.service';
import { UserProfileResponseDto } from '../../dtos/user-profile.response.dto';

const makeProfile = (overrides: Partial<UserProfileResponseDto> = {}): UserProfileResponseDto => ({
  id: 1,
  name: 'Alice Smith',
  email: 'alice@example.com',
  isAdmin: false,
  departmentId: 10,
  departmentName: 'Engineering',
  managerId: 0,
  managerName: '',
  level: 1,
  isActive: true,
  ...overrides,
});

async function setup() {
  await TestBed.configureTestingModule({
    imports: [Profile],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      UserService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(Profile);
  const component = fixture.componentInstance;
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();
  httpTesting
    .expectOne((r) => r.url.includes('/User/me'))
    .flush(makeProfile());
  fixture.detectChanges();

  return { fixture, component, httpTesting };
}

describe('Profile Component', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
  });

  describe('Component Initialization', () => {
    it('should create the component', async () => {
      const { component } = await setup();
      expect(component).toBeTruthy();
    });

    it('should initialize profile to populated data', async () => {
      const { component } = await setup();
      expect(component.profile()).toBeTruthy();
      expect(component.profile()?.name).toBe('Alice Smith');
    });

    it('should initialize isLoading to false after data loads', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should initialize loadError to null', async () => {
      const { component } = await setup();
      expect(component.loadError()).toBeNull();
    });

    it('should initialize change password properties', async () => {
      const { component } = await setup();
      expect(component.oldPassword).toBe('');
      expect(component.newPassword).toBe('');
      expect(component.confirmPassword).toBe('');
      expect(component.isChangingPassword()).toBe(false);
      expect(component.passwordSuccess()).toBeNull();
      expect(component.passwordError()).toBeNull();
      expect(component.showOldPassword()).toBe(false);
      expect(component.showNewPassword()).toBe(false);
      expect(component.showConfirmPassword()).toBe(false);
    });
  });

  describe('loadProfile()', () => {
    it('should set isLoading to true while fetching', async () => {
      await TestBed.configureTestingModule({
        imports: [Profile],
        providers: [provideHttpClient(), provideHttpClientTesting(), UserService],
      }).compileComponents();

      const fixture = TestBed.createComponent(Profile);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      expect(component.isLoading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/User/me')).flush(makeProfile());
    });

    it('should clear loadError when called', async () => {
      const { component, httpTesting } = await setup();
      component.loadError.set('Some previous error');
      component.loadProfile();
      expect(component.loadError()).toBeNull();
      httpTesting.expectOne((r) => r.url.includes('/User/me')).flush(makeProfile());
    });

    it('should handle error during load', async () => {
      await TestBed.configureTestingModule({
        imports: [Profile],
        providers: [provideHttpClient(), provideHttpClientTesting(), UserService],
      }).compileComponents();

      const fixture = TestBed.createComponent(Profile);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      httpTesting
        .expectOne((r) => r.url.includes('/User/me'))
        .flush({ message: 'err' }, { status: 500, statusText: 'Server Error' });

      expect(component.isLoading()).toBe(false);
      expect(component.loadError()).toBe('Could not load your profile. Please try again.');
      expect(component.profile()).toBeNull();
    });
  });

  describe('submitChangePassword()', () => {
    it('should set error if fields are empty', async () => {
      const { component, httpTesting } = await setup();
      component.oldPassword = '';
      component.newPassword = '';
      component.confirmPassword = '';
      component.submitChangePassword();
      expect(component.passwordError()).toBe('All fields are required.');
      httpTesting.verify();
    });

    it('should set error if new password is too short', async () => {
      const { component, httpTesting } = await setup();
      component.oldPassword = 'oldPassword123';
      component.newPassword = 'short';
      component.confirmPassword = 'short';
      component.submitChangePassword();
      expect(component.passwordError()).toBe('New password must be at least 8 characters.');
      httpTesting.verify();
    });

    it('should set error if passwords do not match', async () => {
      const { component, httpTesting } = await setup();
      component.oldPassword = 'oldPassword123';
      component.newPassword = 'newPassword123';
      component.confirmPassword = 'differentPassword123';
      component.submitChangePassword();
      expect(component.passwordError()).toBe('New password and confirm password do not match.');
      httpTesting.verify();
    });

    it('should set error if new password is same as old password', async () => {
      const { component, httpTesting } = await setup();
      component.oldPassword = 'samePassword123';
      component.newPassword = 'samePassword123';
      component.confirmPassword = 'samePassword123';
      component.submitChangePassword();
      expect(component.passwordError()).toBe('New password must differ from the current password.');
      httpTesting.verify();
    });

    it('should call api and handle success', async () => {
      const { component, httpTesting } = await setup();
      component.oldPassword = 'oldPassword123';
      component.newPassword = 'newPassword123';
      component.confirmPassword = 'newPassword123';
      
      component.submitChangePassword();
      
      expect(component.isChangingPassword()).toBe(true);
      expect(component.passwordError()).toBeNull();
      
      const req = httpTesting.expectOne((r) => r.url.includes('/User/me/change-password'));
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        oldPassword: 'oldPassword123',
        newPassword: 'newPassword123',
      });
      req.flush({});
      
      expect(component.isChangingPassword()).toBe(false);
      expect(component.passwordSuccess()).toBe('Password changed successfully.');
      expect(component.oldPassword).toBe('');
      expect(component.newPassword).toBe('');
      expect(component.confirmPassword).toBe('');
    });

    it('should handle api error with message property', async () => {
      const { component, httpTesting } = await setup();
      component.oldPassword = 'oldPassword123';
      component.newPassword = 'newPassword123';
      component.confirmPassword = 'newPassword123';
      
      component.submitChangePassword();
      
      httpTesting.expectOne((r) => r.url.includes('/User/me/change-password')).flush(
        { message: 'Invalid old password' }, { status: 400, statusText: 'Bad Request' }
      );
      
      expect(component.isChangingPassword()).toBe(false);
      expect(component.passwordError()).toBe('Invalid old password');
      expect(component.passwordSuccess()).toBeNull();
    });

    it('should handle api error with plain string response', async () => {
      const { component, httpTesting } = await setup();
      component.oldPassword = 'oldPassword123';
      component.newPassword = 'newPassword123';
      component.confirmPassword = 'newPassword123';
      
      component.submitChangePassword();
      
      httpTesting.expectOne((r) => r.url.includes('/User/me/change-password')).flush(
        'Some error occurred', { status: 400, statusText: 'Bad Request' }
      );
      
      expect(component.isChangingPassword()).toBe(false);
      expect(component.passwordError()).toBe('Some error occurred');
    });

    it('should handle api error with fallback message', async () => {
      const { component, httpTesting } = await setup();
      component.oldPassword = 'oldPassword123';
      component.newPassword = 'newPassword123';
      component.confirmPassword = 'newPassword123';
      
      component.submitChangePassword();
      
      httpTesting.expectOne((r) => r.url.includes('/User/me/change-password')).flush(
        null, { status: 500, statusText: 'Server Error' }
      );
      
      expect(component.isChangingPassword()).toBe(false);
      expect(component.passwordError()).toBe('Failed to change password. Please verify your current password.');
    });
  });

  describe('Helpers', () => {
    it('getInitials should return the first letter of a single word', async () => {
      const { component } = await setup();
      expect(component.getInitials('Alice')).toBe('A');
    });

    it('getInitials should return the first letters of multiple words', async () => {
      const { component } = await setup();
      expect(component.getInitials('Alice Smith')).toBe('AS');
      expect(component.getInitials('alice smith')).toBe('AS');
    });

    it('getInitials should cap at two letters', async () => {
      const { component } = await setup();
      expect(component.getInitials('Alice Bob Charlie')).toBe('AB');
    });

    it('levelLabel should format the level number', async () => {
      const { component } = await setup();
      expect(component.levelLabel(3)).toBe('Level 3');
      expect(component.levelLabel(0)).toBe('Level 0');
    });
  });
});
