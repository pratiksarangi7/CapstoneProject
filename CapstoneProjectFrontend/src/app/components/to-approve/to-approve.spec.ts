import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ToApprove } from './to-approve';
import { DocumentService } from '../../services/document.service';
import { UserService } from '../../services/user.service';
import { UserDocumentResponseDto } from '../../dtos/user-document.response.dto';
import { DocumentVersionResponseDto } from '../../dtos/document-version.response.dto';
import { DocsPendingApprovalApiResponse } from '../../dtos/docs-pending-approval.response.dto';
import { OtherDepartmentUsersResponseDto } from '../../dtos/other-departments-users.response.dto';

const makeVersion = (overrides: Partial<DocumentVersionResponseDto> = {}): DocumentVersionResponseDto => ({
  id: 1,
  documentId: 100,
  versionNumber: 1,
  originalFileName: 'test.pdf',
  storedFileName: 'stored-test.pdf',
  fileSize: 2048,
  mimeType: 'application/pdf',
  isCurrentVersion: true,
  uploadedByUserId: 1,
  uploadedByUserName: 'Alice',
  createdAt: '2024-01-01T10:00:00Z',
  approvalAction: null,
  ...overrides,
});

const makeDoc = (overrides: Partial<UserDocumentResponseDto> = {}): UserDocumentResponseDto => ({
  id: 100,
  title: 'Test Doc',
  description: 'A test document',
  documentStatus: 'PendingApproval',
  createdAt: '2024-01-01T10:00:00Z',
  targetDepartmentId: 10,
  targetDepartmentName: 'Engineering',
  currentApproverUserId: 2,
  currentApproverName: 'Bob',
  currentApproverEmail: 'bob@example.com',
  currentApproverDepartmentName: 'Engineering',
  createdByUserId: 1,
  createdByUserName: 'Alice',
  createdByUserEmail: 'alice@example.com',
  versions: [makeVersion()],
  ...overrides,
});

const makePagedResponse = (
  items: UserDocumentResponseDto[] = [],
  overrides: Partial<DocsPendingApprovalApiResponse> = {}
): DocsPendingApprovalApiResponse => ({
  items,
  pageNumber: 1,
  pageSize: 10,
  totalCount: items.length,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false,
  ...overrides,
});

const makeOtherUser = (overrides: Partial<OtherDepartmentUsersResponseDto> = {}): OtherDepartmentUsersResponseDto => ({
  id: 5,
  name: 'Charlie',
  email: 'charlie@example.com',
  departmentName: 'HR',
  ...overrides,
});

async function setup() {
  await TestBed.configureTestingModule({
    imports: [ToApprove],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      DocumentService,
      UserService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(ToApprove);
  const component = fixture.componentInstance;
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();
  httpTesting
    .expectOne((r) => r.url.includes('/document/pending'))
    .flush(makePagedResponse([makeDoc()]));
  fixture.detectChanges();

  return { fixture, component, httpTesting };
}

describe('ToApprove Component', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
  });

  describe('Component Initialization', () => {
    it('should create the component', async () => {
      const { component } = await setup();
      expect(component).toBeTruthy();
    });

    it('should initialize basic states', async () => {
      const { component } = await setup();
      expect(component.currentPage()).toBe(1);
      expect(component.pageSize()).toBe(10);
      expect(component.searchQuery()).toBe('');
      expect(component.isLoading()).toBe(false);
    });

    it('should initialize modal signals to defaults', async () => {
      const { component } = await setup();
      expect(component.isActionModalOpen()).toBe(false);
      expect(component.selectedDocument()).toBeNull();
      expect(component.selectedVersion()).toBeNull();
      expect(component.actionComments).toBe('');
      expect(component.isApproveDropdownOpen()).toBe(false);
      expect(component.pendingActionType()).toBeNull();
    });

    it('should initialize user search modal states', async () => {
      const { component } = await setup();
      expect(component.isUserSearchModalOpen()).toBe(false);
      expect(component.allOtherDeptUsers()).toEqual([]);
      expect(component.isUsersLoading()).toBe(false);
      expect(component.userSearchQuery).toBe('');
      expect(component.selectedTargetUser()).toBeNull();
    });

    it('should load documents on init', async () => {
      const { component } = await setup();
      expect(component.pendingDocuments().items).toHaveLength(1);
    });
  });

  describe('loadPage()', () => {
    it('should set isLoading to true while fetching', async () => {
      await TestBed.configureTestingModule({
        imports: [ToApprove],
        providers: [provideHttpClient(), provideHttpClientTesting(), DocumentService, UserService],
      }).compileComponents();

      const fixture = TestBed.createComponent(ToApprove);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      expect(component.isLoading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/document/pending')).flush(makePagedResponse());
    });

    it('should handle api error gracefully', async () => {
      await TestBed.configureTestingModule({
        imports: [ToApprove],
        providers: [provideHttpClient(), provideHttpClientTesting(), DocumentService, UserService],
      }).compileComponents();

      const fixture = TestBed.createComponent(ToApprove);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      httpTesting
        .expectOne((r) => r.url.includes('/document/pending'))
        .flush({ message: 'err' }, { status: 500, statusText: 'Server Error' });

      expect(component.isLoading()).toBe(false);
    });
  });

  describe('onSearchInput()', () => {
    it('should set searchQuery and reset page to 1', async () => {
      const { component } = await setup();
      component.currentPage.set(2);
      const event = { target: { value: 'Test' } } as unknown as Event;
      component.onSearchInput(event);
      expect(component.searchQuery()).toBe('Test');
      expect(component.currentPage()).toBe(1);
    });
  });

  describe('goToPage()', () => {
    it('should not navigate below page 1 or beyond totalPages', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(component, 'loadPage');
      component.goToPage(0);
      component.goToPage(999);
      expect(spy).not.toHaveBeenCalled();
      httpTesting.verify();
    });

    it('should set currentPage and call loadPage for a valid page', async () => {
      const { component, httpTesting } = await setup();
      component.pendingDocuments.set(makePagedResponse([makeDoc()], { totalPages: 5 }));
      component.goToPage(3);
      expect(component.currentPage()).toBe(3);
      httpTesting.expectOne((r) => r.url.includes('/document/pending')).flush(makePagedResponse());
    });
  });

  describe('toggleExpand() / isExpanded()', () => {
    it('should toggle docId in expandedDocIds', async () => {
      const { component } = await setup();
      component.toggleExpand(100);
      expect(component.isExpanded(100)).toBe(true);
      component.toggleExpand(100);
      expect(component.isExpanded(100)).toBe(false);
    });
  });

  describe('Modals and Toggles', () => {
    it('openActionModal should set selected doc/version and reset action state', async () => {
      const { component } = await setup();
      const doc = makeDoc();
      const ver = makeVersion();
      
      component.actionComments = 'old';
      component.isApproveDropdownOpen.set(true);
      component.pendingActionType.set('Transfer');
      component.selectedTargetUser.set(makeOtherUser());
      
      component.openActionModal(doc, ver);
      
      expect(component.selectedDocument()).toEqual(doc);
      expect(component.selectedVersion()).toEqual(ver);
      expect(component.actionComments).toBe('');
      expect(component.isApproveDropdownOpen()).toBe(false);
      expect(component.pendingActionType()).toBeNull();
      expect(component.selectedTargetUser()).toBeNull();
      expect(component.isActionModalOpen()).toBe(true);
    });

    it('closeActionModal should reset all action states and close modal', async () => {
      const { component } = await setup();
      component.isActionModalOpen.set(true);
      component.selectedDocument.set(makeDoc());
      
      component.closeActionModal();
      
      expect(component.isActionModalOpen()).toBe(false);
      expect(component.selectedDocument()).toBeNull();
    });

    it('toggleApproveDropdown should toggle the signal', async () => {
      const { component } = await setup();
      component.toggleApproveDropdown();
      expect(component.isApproveDropdownOpen()).toBe(true);
      component.toggleApproveDropdown();
      expect(component.isApproveDropdownOpen()).toBe(false);
    });
  });

  describe('User Search Modal', () => {
    it('openUserSearchModal should initialize state and fetch top 10 users', async () => {
      const { component, httpTesting } = await setup();
      component.isApproveDropdownOpen.set(true);
      
      component.openUserSearchModal('Transfer');
      
      expect(component.pendingActionType()).toBe('Transfer');
      expect(component.isApproveDropdownOpen()).toBe(false);
      expect(component.isUsersLoading()).toBe(true);
      expect(component.isUserSearchModalOpen()).toBe(true);

      const req = httpTesting.expectOne((r) => r.url.includes('/User/external') && r.params.get('search') === '');
      req.flush([makeOtherUser()]);
      
      expect(component.allOtherDeptUsers()).toHaveLength(1);
      expect(component.isUsersLoading()).toBe(false);
    });

    it('closeUserSearchModal should reset user search state and clear timeout', async () => {
      const { component } = await setup();
      component.isUserSearchModalOpen.set(true);
      component.pendingActionType.set('Transfer');
      
      component.closeUserSearchModal();
      
      expect(component.isUserSearchModalOpen()).toBe(false);
      expect(component.pendingActionType()).toBeNull();
    });

    it('loadOtherDeptUsers should fetch users based on query', async () => {
      const { component, httpTesting } = await setup();
      component.userSearchQuery = 'charlie';
      
      component.loadOtherDeptUsers();
      
      expect(component.isUsersLoading()).toBe(true);
      const req = httpTesting.expectOne((r) => r.url.includes('/User/external') && r.params.get('search') === 'charlie');
      expect(req.request.method).toBe('GET');
      req.flush([makeOtherUser()]);
      
      expect(component.allOtherDeptUsers()).toHaveLength(1);
      expect(component.isUsersLoading()).toBe(false);
      expect(component.filteredUsers).toHaveLength(1);
    });

    it('loadOtherDeptUsers with empty query should fetch top 10 users', async () => {
      const { component, httpTesting } = await setup();
      component.userSearchQuery = '';
      
      component.loadOtherDeptUsers();
      
      expect(component.isUsersLoading()).toBe(true);
      const req = httpTesting.expectOne((r) => r.url.includes('/User/external') && r.params.get('search') === '');
      req.flush([makeOtherUser()]);
      
      expect(component.allOtherDeptUsers()).toHaveLength(1);
      expect(component.isUsersLoading()).toBe(false);
    });

    it('onUserSearchChange should debounce user search API call', async () => {
      vi.useFakeTimers();
      const { component, httpTesting } = await setup();
      component.userSearchQuery = 'test';
      
      component.onUserSearchChange();
      
      // Fast-forward by 499ms
      vi.advanceTimersByTime(499);
      // API should not have been called yet
      httpTesting.expectNone((r) => r.url.includes('/User/external'));
      
      // Fast-forward by another 1ms (total 500ms)
      vi.advanceTimersByTime(1);
      
      const req = httpTesting.expectOne((r) => r.url.includes('/User/external'));
      req.flush([makeOtherUser()]);
      
      vi.useRealTimers();
    });

    it('selectTargetUser should update selectedTargetUser signal', async () => {
      const { component } = await setup();
      const u = makeOtherUser();
      component.selectTargetUser(u);
      expect(component.selectedTargetUser()).toEqual(u);
    });
  });

  describe('Execution Actions', () => {
    it('executeAction should return if no doc/ver is selected', async () => {
      const { component } = await setup();
      const spy = vi.spyOn(component, 'openUserSearchModal');
      component.executeAction('Approve & Transfer');
      expect(spy).not.toHaveBeenCalled();
    });

    it('executeAction should open user search modal for Transfer actions', async () => {
      const { component } = await setup();
      component.selectedDocument.set(makeDoc());
      component.selectedVersion.set(makeVersion());
      const spy = vi.spyOn(component, 'openUserSearchModal');
      
      component.executeAction('Transfer');
      
      expect(spy).toHaveBeenCalledWith('Transfer');
    });

    it('executeAction should call approve api for Approve Completely', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.selectedDocument.set(makeDoc());
      component.selectedVersion.set(makeVersion());
      
      component.executeAction('Approve Completely');
      
      const req = httpTesting.expectOne((r) => r.url.includes('/document/100/approve'));
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.action).toBe(0);
      req.flush({});
      
      httpTesting.expectOne((r) => r.url.includes('/document/pending')).flush(makePagedResponse());
      
      expect(alertSpy).toHaveBeenCalledWith('Approve Completely Successful');
      alertSpy.mockRestore();
    });

    it('executeAction should call approve api for Approve & Forward', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.selectedDocument.set(makeDoc());
      component.selectedVersion.set(makeVersion());
      
      component.executeAction('Approve & Forward');
      
      const req = httpTesting.expectOne((r) => r.url.includes('/document/100/approve'));
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.action).toBe(1);
      req.flush({});
      
      httpTesting.expectOne((r) => r.url.includes('/document/pending')).flush(makePagedResponse());
      alertSpy.mockRestore();
    });

    it('executeAction should reject document and require reason', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.selectedDocument.set(makeDoc());
      component.selectedVersion.set(makeVersion());
      
      component.actionComments = '';
      component.executeAction('Reject');
      expect(alertSpy).toHaveBeenCalledWith('A reason must be provided to reject this document.');
      
      component.actionComments = 'No thanks';
      component.executeAction('Reject');
      
      const req = httpTesting.expectOne((r) => r.url.includes('/document/100/reject'));
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.reason).toBe('No thanks');
      req.flush({});
      
      httpTesting.expectOne((r) => r.url.includes('/document/pending')).flush(makePagedResponse());
      
      expect(alertSpy).toHaveBeenCalledWith('Document Rejected Successfully');
      alertSpy.mockRestore();
    });

    it('confirmTransferAction should call transfer api for Transfer', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.selectedDocument.set(makeDoc());
      component.selectedVersion.set(makeVersion());
      component.pendingActionType.set('Transfer');
      component.selectedTargetUser.set(makeOtherUser({ id: 5 }));
      component.actionComments = 'Please review';
      
      component.confirmTransferAction();
      
      const req = httpTesting.expectOne((r) => r.url.includes('/document/100/transfer'));
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.targetUserId).toBe(5);
      expect(req.request.body.comments).toBe('Please review');
      req.flush({});
      
      httpTesting.expectOne((r) => r.url.includes('/document/pending')).flush(makePagedResponse());
      
      expect(alertSpy).toHaveBeenCalledWith('Document Transferred Successfully');
      alertSpy.mockRestore();
    });

    it('confirmTransferAction should call approve api for Approve & Transfer', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      component.selectedDocument.set(makeDoc());
      component.selectedVersion.set(makeVersion());
      component.pendingActionType.set('Approve & Transfer');
      component.selectedTargetUser.set(makeOtherUser({ id: 5 }));
      
      component.confirmTransferAction();
      
      const req = httpTesting.expectOne((r) => r.url.includes('/document/100/approve'));
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.action).toBe(2);
      expect(req.request.body.targetUserId).toBe(5);
      req.flush({});
      
      httpTesting.expectOne((r) => r.url.includes('/document/pending')).flush(makePagedResponse());
      
      expect(alertSpy).toHaveBeenCalledWith('Approve & Transfer Successful');
      alertSpy.mockRestore();
    });
  });

  describe('Document Preview', () => {
    it('viewDocument should open the preview modal and set file properties', async () => {
      const { component, httpTesting } = await setup();
      const version = makeVersion({ originalFileName: 'report.pdf', mimeType: 'application/pdf' });
      
      component.viewDocument(100, version);
      
      expect(component.isPreviewOpen()).toBe(true);
      expect(component.previewFileName()).toBe('report.pdf');
      expect(component.isPreviewLoading()).toBe(true);
      
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['pdf'], { type: 'application/pdf' }));
      
      expect(component.previewFileType()).toBe('pdf');
      expect(component.isPreviewLoading()).toBe(false);
    });

    it('viewDocument should handle image type', async () => {
      const { component, httpTesting } = await setup();
      component.viewDocument(100, makeVersion({ mimeType: 'image/png' }));
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['img'], { type: 'image/png' }));
      expect(component.previewFileType()).toBe('image');
    });

    it('viewDocument should handle other type', async () => {
      const { component, httpTesting } = await setup();
      component.viewDocument(100, makeVersion({ mimeType: 'text/plain' }));
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['txt'], { type: 'text/plain' }));
      expect(component.previewFileType()).toBe('other');
    });

    it('viewDocument should handle api error', async () => {
      const { component, httpTesting } = await setup();
      component.viewDocument(100, makeVersion());
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).error(new ProgressEvent('error'));
      expect(component.previewError()).toBe('Could not load document preview. Please check your internet connection or try again later.');
      expect(component.isPreviewLoading()).toBe(false);
    });

    it('closePreviewModal should reset preview properties', async () => {
      const { component, httpTesting } = await setup();
      component.viewDocument(100, makeVersion());
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob());
      
      component.closePreviewModal();
      
      expect(component.isPreviewOpen()).toBe(false);
      expect(component.previewFileUrl()).toBeNull();
      expect(component.previewFileType()).toBeNull();
      expect(component.previewFileName()).toBe('');
      expect(component.previewError()).toBeNull();
    });
  });

  describe('formatters', () => {
    it('formatFileSize should format bytes properly', async () => {
      const { component } = await setup();
      expect(component.formatFileSize(512)).toBe('512 B');
      expect(component.formatFileSize(1024 * 2)).toBe('2.0 KB');
      expect(component.formatFileSize(1024 * 1024 * 4)).toBe('4.0 MB');
    });

    it('formatUploaderName should truncate long names', async () => {
      const { component } = await setup();
      expect(component.formatUploaderName(null)).toBe('');
      expect(component.formatUploaderName('Alice Smith')).toBe('Alice Smith');
      expect(component.formatUploaderName('Alice Smith The Third')).toBe('Alice Smith The.....');
    });
  });
});
