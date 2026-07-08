import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AdminDocuments } from './admin-documents';
import { AdminService } from '../../services/admin.service';
import { DocumentService } from '../../services/document.service';
import { UserDocumentResponseDto } from '../../dtos/user-document.response.dto';
import { DocumentVersionResponseDto } from '../../dtos/document-version.response.dto';
import { PaginatedResponse } from '../../helpers';
import { DocumentStatus } from '../../enums/document-status-filter.enum';

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
  overrides: Partial<PaginatedResponse<UserDocumentResponseDto>> = {}
): PaginatedResponse<UserDocumentResponseDto> => ({
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
    imports: [AdminDocuments],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      AdminService,
      DocumentService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(AdminDocuments);
  const component = fixture.componentInstance;
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();
  httpTesting
    .expectOne((r) => r.url.includes('/admin/documents/all'))
    .flush(makePagedResponse([makeDoc()]));
  fixture.detectChanges();

  return { fixture, component, httpTesting };
}

describe('AdminDocuments Component', () => {
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

    it('should initialize mainSearchQuery to empty string', async () => {
      const { component } = await setup();
      expect(component.mainSearchQuery()).toBe('');
    });

    it('should initialize statusFilter to undefined', async () => {
      const { component } = await setup();
      expect(component.statusFilter()).toBeUndefined();
    });

    it('should initialize isLoading to false after data loads', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should initialize isModalOpen to false', async () => {
      const { component } = await setup();
      expect(component.isModalOpen()).toBe(false);
    });

    it('should initialize expandedDocIds to an empty Set', async () => {
      const { component } = await setup();
      expect(component.expandedDocIds().size).toBe(0);
    });

    it('should load documents on init', async () => {
      const { component } = await setup();
      expect(component.docsResponse().items).toHaveLength(1);
    });
  });

  describe('loadPage()', () => {
    it('should set isLoading to true while fetching', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminDocuments],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService, DocumentService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminDocuments);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      expect(component.isLoading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/admin/documents/all')).flush(makePagedResponse());
    });

    it('should set isLoading to false after success', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should set isLoading to false after error', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminDocuments],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService, DocumentService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminDocuments);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      httpTesting
        .expectOne((r) => r.url.includes('/admin/documents/all'))
        .flush({ message: 'err' }, { status: 500, statusText: 'Server Error' });

      expect(component.isLoading()).toBe(false);
    });

    it('should populate docsResponse on success', async () => {
      const { component } = await setup();
      expect(component.docsResponse().totalCount).toBe(1);
    });

    it('should reset expandedDocIds on each load', async () => {
      const { component, httpTesting } = await setup();
      component.expandedDocIds.set(new Set([100]));
      component.loadPage();
      expect(component.expandedDocIds().size).toBe(0);
      httpTesting.expectOne((r) => r.url.includes('/admin/documents/all')).flush(makePagedResponse());
    });

    it('should flatten nested items when the API returns a nested array', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminDocuments],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService, DocumentService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminDocuments);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      const nestedResponse = { ...makePagedResponse(), items: [[makeDoc(), makeDoc({ id: 101 })]] as any };
      httpTesting.expectOne((r) => r.url.includes('/admin/documents/all')).flush(nestedResponse);
      fixture.detectChanges();

      expect(component.docsResponse().items).toHaveLength(2);
    });

    it('should send a GET request to the documents endpoint', async () => {
      await TestBed.configureTestingModule({
        imports: [AdminDocuments],
        providers: [provideHttpClient(), provideHttpClientTesting(), AdminService, DocumentService],
      }).compileComponents();

      const fixture = TestBed.createComponent(AdminDocuments);
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      const req = httpTesting.expectOne((r) => r.url.includes('/admin/documents/all'));
      expect(req.request.method).toBe('GET');
      req.flush(makePagedResponse());
    });
  });

  describe('onStatusChange()', () => {
    it('should set statusFilter to undefined when status is "ALL"', async () => {
      const { component, httpTesting } = await setup();
      component.onStatusChange('ALL');
      expect(component.statusFilter()).toBeUndefined();
      httpTesting.expectOne((r) => r.url.includes('/admin/documents/all')).flush(makePagedResponse());
    });

    it('should set statusFilter to undefined when status is empty string', async () => {
      const { component, httpTesting } = await setup();
      component.onStatusChange('');
      expect(component.statusFilter()).toBeUndefined();
      httpTesting.expectOne((r) => r.url.includes('/admin/documents/all')).flush(makePagedResponse());
    });

    it('should set statusFilter to the given DocumentStatus', async () => {
      const { component, httpTesting } = await setup();
      component.onStatusChange('Approved');
      expect(component.statusFilter()).toBe('Approved' as DocumentStatus);
      httpTesting.expectOne((r) => r.url.includes('/admin/documents/all')).flush(makePagedResponse());
    });

    it('should reset currentPage to 1 on status change', async () => {
      const { component, httpTesting } = await setup();
      component.currentPage.set(3);
      component.onStatusChange('Rejected');
      expect(component.currentPage()).toBe(1);
      httpTesting.expectOne((r) => r.url.includes('/admin/documents/all')).flush(makePagedResponse());
    });

    it('should trigger a loadPage call', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(component, 'loadPage');
      component.onStatusChange('Approved');
      expect(spy).toHaveBeenCalled();
      httpTesting.expectOne((r) => r.url.includes('/admin/documents/all')).flush(makePagedResponse());
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
      component.docsResponse.set(makePagedResponse([makeDoc()], { totalPages: 5 }));
      component.goToPage(3);
      expect(component.currentPage()).toBe(3);
      httpTesting.expectOne((r) => r.url.includes('/admin/documents/all')).flush(makePagedResponse());
    });
  });

  describe('pageNumbers getter', () => {
    it('should return all pages when totalPages is <= 7', async () => {
      const { component } = await setup();
      component.docsResponse.set(makePagedResponse([], { totalPages: 5 }));
      expect(component.pageNumbers).toEqual([1, 2, 3, 4, 5]);
    });

    it('should return empty array when totalPages is 0', async () => {
      const { component } = await setup();
      component.docsResponse.set(makePagedResponse([], { totalPages: 0 }));
      expect(component.pageNumbers).toEqual([]);
    });

    it('should return a window of 7 pages when totalPages exceeds 7', async () => {
      const { component } = await setup();
      component.docsResponse.set(makePagedResponse([], { totalPages: 20, pageNumber: 1 }));
      component.currentPage.set(1);
      expect(component.pageNumbers).toHaveLength(7);
    });

    it('should include the current page within the returned window', async () => {
      const { component } = await setup();
      component.docsResponse.set(makePagedResponse([], { totalPages: 20, pageNumber: 10 }));
      component.currentPage.set(10);
      expect(component.pageNumbers).toContain(10);
    });
  });

  describe('toggleExpand()', () => {
    it('should add a docId to expandedDocIds when not present', async () => {
      const { component } = await setup();
      component.toggleExpand(100);
      expect(component.expandedDocIds().has(100)).toBe(true);
    });

    it('should remove a docId from expandedDocIds when already present', async () => {
      const { component } = await setup();
      component.toggleExpand(100);
      component.toggleExpand(100);
      expect(component.expandedDocIds().has(100)).toBe(false);
    });

    it('should allow multiple different docIds to be expanded simultaneously', async () => {
      const { component } = await setup();
      component.toggleExpand(100);
      component.toggleExpand(200);
      expect(component.expandedDocIds().has(100)).toBe(true);
      expect(component.expandedDocIds().has(200)).toBe(true);
    });
  });

  describe('isExpanded()', () => {
    it('should return true when the docId is in expandedDocIds', async () => {
      const { component } = await setup();
      component.expandedDocIds.set(new Set([100]));
      expect(component.isExpanded(100)).toBe(true);
    });

    it('should return false when the docId is not in expandedDocIds', async () => {
      const { component } = await setup();
      expect(component.isExpanded(100)).toBe(false);
    });
  });

  describe('viewDocument()', () => {
    it('should open the modal and set the file name', async () => {
      const { component, httpTesting } = await setup();
      const version = makeVersion({ originalFileName: 'report.pdf' });
      component.viewDocument(100, version);
      expect(component.isModalOpen()).toBe(true);
      expect(component.modalFileName()).toBe('report.pdf');
      expect(component.isModalLoading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['pdf'], { type: 'application/pdf' }));
    });

    it('should set modalFileType to "pdf" for a PDF blob', async () => {
      const { component, httpTesting } = await setup();
      const version = makeVersion({ mimeType: 'application/pdf' });
      component.viewDocument(100, version);
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['pdf'], { type: 'application/pdf' }));
      expect(component.modalFileType()).toBe('pdf');
      expect(component.isModalLoading()).toBe(false);
    });

    it('should set modalFileType to "image" for an image blob', async () => {
      const { component, httpTesting } = await setup();
      const version = makeVersion({ mimeType: 'image/png' });
      component.viewDocument(100, version);
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['img'], { type: 'image/png' }));
      expect(component.modalFileType()).toBe('image');
    });

    it('should set modalFileType to "other" for an unrecognised MIME type', async () => {
      const { component, httpTesting } = await setup();
      const version = makeVersion({ mimeType: 'application/zip' });
      component.viewDocument(100, version);
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['zip'], { type: 'application/zip' }));
      expect(component.modalFileType()).toBe('other');
    });

    it('should set modalError and clear isModalLoading on error', async () => {
      const { component, httpTesting } = await setup();
      component.viewDocument(100, makeVersion());
      httpTesting
        .expectOne((r) => r.url.includes('/document/100/file'))
        .error(new ProgressEvent('error'));
      expect(component.modalError()).toBe('Could not load document preview. Please try again.');
      expect(component.isModalLoading()).toBe(false);
    });

    it('should send GET to the correct document file endpoint with versionId', async () => {
      const { component, httpTesting } = await setup();
      const version = makeVersion({ id: 42 });
      component.viewDocument(100, version);
      const req = httpTesting.expectOne((r) => r.url.includes('/document/100/file'));
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('versionId')).toBe('42');
      req.flush(new Blob(['pdf'], { type: 'application/pdf' }));
    });
  });

  describe('closeModal()', () => {
    it('should set isModalOpen to false', async () => {
      const { component, httpTesting } = await setup();
      component.viewDocument(100, makeVersion());
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['pdf'], { type: 'application/pdf' }));
      component.closeModal();
      expect(component.isModalOpen()).toBe(false);
    });

    it('should clear modalFileUrl, modalFileType, modalFileName, and modalError', async () => {
      const { component, httpTesting } = await setup();
      component.viewDocument(100, makeVersion());
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['pdf'], { type: 'application/pdf' }));
      component.closeModal();
      expect(component.modalFileUrl()).toBeNull();
      expect(component.modalFileType()).toBeNull();
      expect(component.modalFileName()).toBe('');
      expect(component.modalError()).toBeNull();
    });

    it('should clear rawFileUrl after close', async () => {
      const { component, httpTesting } = await setup();
      component.viewDocument(100, makeVersion());
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['pdf'], { type: 'application/pdf' }));
      component.closeModal();
      expect(component.rawFileUrl).toBeNull();
    });
  });

  describe('formatFileSize()', () => {
    it('should format bytes under 1024 as "N B"', async () => {
      const { component } = await setup();
      expect(component.formatFileSize(512)).toBe('512 B');
    });

    it('should format bytes in the KB range', async () => {
      const { component } = await setup();
      expect(component.formatFileSize(2048)).toBe('2.0 KB');
    });

    it('should format bytes in the MB range', async () => {
      const { component } = await setup();
      expect(component.formatFileSize(1024 * 1024 * 3)).toBe('3.0 MB');
    });
  });

  describe('statusClass()', () => {
    it('should return "badge-approved" for Approved', async () => {
      const { component } = await setup();
      expect(component.statusClass('Approved')).toBe('badge-approved');
    });

    it('should return "badge-rejected" for Rejected', async () => {
      const { component } = await setup();
      expect(component.statusClass('Rejected')).toBe('badge-rejected');
    });

    it('should return "badge-pending" for PendingApproval', async () => {
      const { component } = await setup();
      expect(component.statusClass('PendingApproval')).toBe('badge-pending');
    });

    it('should return "badge-withdrawn" for Withdrawn', async () => {
      const { component } = await setup();
      expect(component.statusClass('Withdrawn')).toBe('badge-withdrawn');
    });

    it('should return "badge-default" for an unknown status', async () => {
      const { component } = await setup();
      expect(component.statusClass('Unknown')).toBe('badge-default');
    });
  });

  describe('statusLabel()', () => {
    it('should return "Pending" for PendingApproval', async () => {
      const { component } = await setup();
      expect(component.statusLabel('PendingApproval')).toBe('Pending');
    });

    it('should return the status unchanged for other values', async () => {
      const { component } = await setup();
      expect(component.statusLabel('Approved')).toBe('Approved');
      expect(component.statusLabel('Rejected')).toBe('Rejected');
    });
  });

  describe('actionClass()', () => {
    it('should return "action-approved" for Approved', async () => {
      const { component } = await setup();
      expect(component.actionClass('Approved')).toBe('action-approved');
    });

    it('should return "action-rejected" for Rejected', async () => {
      const { component } = await setup();
      expect(component.actionClass('Rejected')).toBe('action-rejected');
    });

    it('should return "action-forwarded" for ForwardedToDepartment', async () => {
      const { component } = await setup();
      expect(component.actionClass('ForwardedToDepartment')).toBe('action-forwarded');
    });

    it('should return empty string for unknown action', async () => {
      const { component } = await setup();
      expect(component.actionClass('SomethingElse')).toBe('');
    });
  });

  describe('getInitials()', () => {
    it('should return the first letter of a single word uppercased', async () => {
      const { component } = await setup();
      expect(component.getInitials('Alice')).toBe('A');
    });

    it('should return two initials for a two-word name', async () => {
      const { component } = await setup();
      expect(component.getInitials('John Doe')).toBe('JD');
    });

    it('should return at most 2 characters for longer names', async () => {
      const { component } = await setup();
      expect(component.getInitials('Alice Bob Carol')).toBe('AB');
    });
  });

  describe('currentVersion()', () => {
    it('should return the version marked as current', async () => {
      const { component } = await setup();
      const v1 = makeVersion({ id: 1, isCurrentVersion: false });
      const v2 = makeVersion({ id: 2, isCurrentVersion: true });
      const doc = makeDoc({ versions: [v1, v2] });
      expect(component.currentVersion(doc)?.id).toBe(2);
    });

    it('should return the first version when none is marked as current', async () => {
      const { component } = await setup();
      const v1 = makeVersion({ id: 1, isCurrentVersion: false });
      const v2 = makeVersion({ id: 2, isCurrentVersion: false });
      const doc = makeDoc({ versions: [v1, v2] });
      expect(component.currentVersion(doc)?.id).toBe(1);
    });

    it('should return null when versions array is empty', async () => {
      const { component } = await setup();
      const doc = makeDoc({ versions: [] });
      expect(component.currentVersion(doc)).toBeNull();
    });
  });
});
