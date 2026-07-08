import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { UserDashboard } from './user-dashboard';
import { AuthService } from '../../services/auth.service';
import { BehaviorSubject, of } from 'rxjs';

// We need to mock the services used by the child components to avoid HTTP errors during rendering
import { DocumentService } from '../../services/document.service';
import { UserService } from '../../services/user.service';
import { DepartmentService } from '../../services/department.service';

const mockAuthService = {
  userName$: new BehaviorSubject<string | null>('Test User')
};

const mockRouter = {
  navigate: vi.fn()
};

const mockDocumentService = {
  myUploadsApiCall: vi.fn().mockReturnValue(of({ items: [], totalPages: 0 })),
  getMyDocumentsApiCall: vi.fn().mockReturnValue(of({ items: [], totalPages: 0 })),
  getDocsPendingApproval: vi.fn().mockReturnValue(of({ items: [], totalPages: 0 })),
};

const mockUserService = {
  getProfileDetails: vi.fn().mockReturnValue(of({})),
  getMyPastApprovalActions: vi.fn().mockReturnValue(of({ items: [], totalPages: 0 })),
};

const mockDepartmentService = {
  GetAllDepartments: vi.fn().mockReturnValue(of([])),
};

async function setup() {
  await TestBed.configureTestingModule({
    imports: [UserDashboard],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: AuthService, useValue: mockAuthService },
      { provide: Router, useValue: mockRouter },
      { provide: DocumentService, useValue: mockDocumentService },
      { provide: UserService, useValue: mockUserService },
      { provide: DepartmentService, useValue: mockDepartmentService }
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(UserDashboard);
  const component = fixture.componentInstance;
  
  // Create spy for localStorage
  vi.spyOn(Storage.prototype, 'removeItem');

  fixture.detectChanges();

  return { fixture, component, router: mockRouter };
}

describe('UserDashboard Component', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
    mockRouter.navigate.mockClear();
  });

  describe('Component Initialization', () => {
    it('should create the component', async () => {
      const { component } = await setup();
      expect(component).toBeTruthy();
    });

    it('should default activeTab to my-uploads', async () => {
      const { component } = await setup();
      expect(component.activeTab()).toBe('my-uploads');
    });

    it('should initialize menu items', async () => {
      const { component } = await setup();
      expect(component.menuItems).toHaveLength(4);
      expect(component.menuItems[0].id).toBe('my-uploads');
      expect(component.menuItems[1].id).toBe('to-approve');
      expect(component.menuItems[2].id).toBe('past-approvals');
      expect(component.menuItems[3].id).toBe('profile');
    });

    it('should get userName from AuthService', async () => {
      const { component } = await setup();
      expect(component.userName()).toBe('Test User');
    });
  });

  describe('setActiveTab', () => {
    it('should update activeTab signal', async () => {
      const { component } = await setup();
      
      component.setActiveTab('profile');
      expect(component.activeTab()).toBe('profile');
      
      component.setActiveTab('to-approve');
      expect(component.activeTab()).toBe('to-approve');
    });
  });

  describe('logout', () => {
    it('should clear token and navigate to login', async () => {
      const { component, router } = await setup();
      
      component.logout();
      
      expect(localStorage.removeItem).toHaveBeenCalledWith('token');
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });
  });
});
