import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { PastApprovals } from './past-approvals';
import { UserService } from '../../services/user.service';
import { UserApprovalActionResponseDto } from '../../dtos/user-approval-actions.response.dto';
import { AuditAction } from '../../enums/audit-action.enum';
import { PaginatedResponse } from '../../helpers';

const makeAction = (overrides: Partial<UserApprovalActionResponseDto> = {}): UserApprovalActionResponseDto => ({
  id: 1,
  performedByUserId: 10,
  performedByUserName: 'Alice',
  performedByUserEmail: 'alice@example.com',
  documentId: 100,
  documentTitle: 'Test Document',
  documentVersionId: 200,
  documentVersionNumber: 1,
  action: AuditAction.DocumentApproved,
  details: null,
  createdAt: '2024-01-01T10:00:00Z',
  ...overrides,
});

const makePagedResponse = (
  items: UserApprovalActionResponseDto[] = [],
  overrides: Partial<PaginatedResponse<UserApprovalActionResponseDto>> = {}
): PaginatedResponse<UserApprovalActionResponseDto> => ({
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
    imports: [PastApprovals],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      UserService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(PastApprovals);
  const component = fixture.componentInstance;
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();
  httpTesting
    .expectOne((r) => r.url.includes('/user/me/actions'))
    .flush(makePagedResponse([makeAction()]));
  fixture.detectChanges();

  return { fixture, component, httpTesting };
}

describe('PastApprovals Component', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
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

    it('should load past approval actions on init', async () => {
      const { component } = await setup();
      expect(component.pastActionsResponse().items).toHaveLength(1);
    });
  });

  describe('loadPastActions()', () => {
    it('should set isLoading to true while fetching', async () => {
      await TestBed.configureTestingModule({
        imports: [PastApprovals],
        providers: [provideHttpClient(), provideHttpClientTesting(), UserService],
      }).compileComponents();

      const fixture = TestBed.createComponent(PastApprovals);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      expect(component.isLoading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/user/me/actions')).flush(makePagedResponse());
    });

    it('should set isLoading to false after success', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should set isLoading to false after error', async () => {
      await TestBed.configureTestingModule({
        imports: [PastApprovals],
        providers: [provideHttpClient(), provideHttpClientTesting(), UserService],
      }).compileComponents();

      const fixture = TestBed.createComponent(PastApprovals);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      httpTesting
        .expectOne((r) => r.url.includes('/user/me/actions'))
        .flush({ message: 'err' }, { status: 500, statusText: 'Server Error' });

      expect(component.isLoading()).toBe(false);
    });

    it('should populate pastActionsResponse on success', async () => {
      const { component } = await setup();
      expect(component.pastActionsResponse().totalCount).toBe(1);
    });
  });

  describe('goToPage()', () => {
    it('should not navigate below page 1', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(component, 'loadPastActions');
      component.goToPage(0);
      expect(spy).not.toHaveBeenCalled();
      httpTesting.verify();
    });

    it('should not navigate beyond totalPages', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(component, 'loadPastActions');
      component.goToPage(999);
      expect(spy).not.toHaveBeenCalled();
      httpTesting.verify();
    });

    it('should set currentPage and call loadPastActions for a valid page', async () => {
      const { component, httpTesting } = await setup();
      component.pastActionsResponse.set(makePagedResponse([makeAction()], { totalPages: 5 }));
      component.goToPage(3);
      expect(component.currentPage()).toBe(3);
      httpTesting.expectOne((r) => r.url.includes('/user/me/actions')).flush(makePagedResponse());
    });
  });

  describe('pageNumbers getter', () => {
    it('should return all pages up to totalPages', async () => {
      const { component } = await setup();
      component.pastActionsResponse.set(makePagedResponse([], { totalPages: 4 }));
      expect(component.pageNumbers).toEqual([1, 2, 3, 4]);
    });

    it('should return empty array when totalPages is 0', async () => {
      const { component } = await setup();
      component.pastActionsResponse.set(makePagedResponse([], { totalPages: 0 }));
      expect(component.pageNumbers).toEqual([]);
    });
  });

  describe('formatters', () => {
    it('should format action strings properly by adding spaces before capitals', async () => {
      const { component } = await setup();
      expect(component.formatAction(AuditAction.DocumentApproved)).toBe('Document Approved');
      expect(component.formatAction(AuditAction.DocumentRejected)).toBe('Document Rejected');
    });

    it('should return badge-approved for DocumentApproved', async () => {
      const { component } = await setup();
      expect(component.getActionBadgeClass(AuditAction.DocumentApproved)).toBe('badge-approved');
    });

    it('should return badge-rejected for DocumentRejected', async () => {
      const { component } = await setup();
      expect(component.getActionBadgeClass(AuditAction.DocumentRejected)).toBe('badge-rejected');
    });

    it('should return badge-forwarded for DocumentForwarded', async () => {
      const { component } = await setup();
      expect(component.getActionBadgeClass(AuditAction.DocumentForwarded)).toBe('badge-forwarded');
    });

    it('should return badge-default for unknown actions', async () => {
      const { component } = await setup();
      expect(component.getActionBadgeClass(AuditAction.UserLoggedIn)).toBe('badge-default');
    });
  });
});
