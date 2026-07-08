import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { MyUploads } from './my-uploads';
import { DocumentService } from '../../services/document.service';
import { UserDocumentResponseDto } from '../../dtos/user-document.response.dto';
import { DocumentVersionResponseDto } from '../../dtos/document-version.response.dto';
import { MyUploadsApiResponse } from '../../dtos/my-uploads.response.dto';
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
  overrides: Partial<MyUploadsApiResponse> = {}
): MyUploadsApiResponse => ({
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
    imports: [MyUploads],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      DocumentService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(MyUploads);
  const component = fixture.componentInstance;
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();
  httpTesting
    .expectOne((r) => r.url.includes('/document/my-uploads'))
    .flush(makePagedResponse([makeDoc()]));
  fixture.detectChanges();

  return { fixture, component, httpTesting };
}

describe('MyUploads Component', () => {
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

    it('should initialize searchQuery to empty string', async () => {
      const { component } = await setup();
      expect(component.searchQuery()).toBe('');
    });

    it('should initialize statusFilter to undefined', async () => {
      const { component } = await setup();
      expect(component.statusFilter()).toBeUndefined();
    });

    it('should initialize isLoading to false after data loads', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should initialize modal signals to defaults', async () => {
      const { component } = await setup();
      expect(component.isModalOpen()).toBe(false);
      expect(component.isModalLoading()).toBe(false);
      expect(component.modalFileUrl()).toBeNull();
      expect(component.modalFileType()).toBeNull();
      expect(component.modalFileName()).toBe('');
      expect(component.modalError()).toBeNull();
    });

    it('should initialize upload/reupload signals to defaults', async () => {
      const { component } = await setup();
      expect(component.isUploadOpen()).toBe(false);
      expect(component.isReuploadOpen()).toBe(false);
      expect(component.reuploadDocId()).toBeNull();
      expect(component.reuploadFile()).toBeNull();
      expect(component.reuploadError()).toBeNull();
      expect(component.reuploadLoading()).toBe(false);
      expect(component.reuploadSuccess()).toBe(false);
    });

    it('should load documents on init', async () => {
      const { component } = await setup();
      expect(component.myDocuments().items).toHaveLength(1);
    });
  });

  describe('loadPage()', () => {
    it('should set isLoading to true while fetching', async () => {
      await TestBed.configureTestingModule({
        imports: [MyUploads],
        providers: [provideHttpClient(), provideHttpClientTesting(), DocumentService],
      }).compileComponents();

      const fixture = TestBed.createComponent(MyUploads);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      expect(component.isLoading()).toBe(true);
      httpTesting.expectOne((r) => r.url.includes('/document/my-uploads')).flush(makePagedResponse());
    });

    it('should set isLoading to false after success', async () => {
      const { component } = await setup();
      expect(component.isLoading()).toBe(false);
    });

    it('should set isLoading to false after error', async () => {
      await TestBed.configureTestingModule({
        imports: [MyUploads],
        providers: [provideHttpClient(), provideHttpClientTesting(), DocumentService],
      }).compileComponents();

      const fixture = TestBed.createComponent(MyUploads);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);

      fixture.detectChanges();
      httpTesting
        .expectOne((r) => r.url.includes('/document/my-uploads'))
        .flush({ message: 'err' }, { status: 500, statusText: 'Server Error' });

      expect(component.isLoading()).toBe(false);
    });

    it('should populate myDocuments on success', async () => {
      const { component } = await setup();
      expect(component.myDocuments().totalCount).toBe(1);
    });

    it('should reset expandedDocIds on each load', async () => {
      const { component, httpTesting } = await setup();
      component.expandedDocIds.set(new Set([100]));
      component.loadPage();
      httpTesting.expectOne((r) => r.url.includes('/document/my-uploads')).flush(makePagedResponse());
      expect(component.expandedDocIds().size).toBe(0);
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

  describe('onStatusChange()', () => {
    it('should set statusFilter to undefined when status is empty string', async () => {
      const { component, httpTesting } = await setup();
      const event = { target: { value: '' } } as unknown as Event;
      component.onStatusChange(event);
      expect(component.statusFilter()).toBeUndefined();
      httpTesting.expectOne((r) => r.url.includes('/document/my-uploads')).flush(makePagedResponse());
    });

    it('should set statusFilter to the given DocumentStatus', async () => {
      const { component, httpTesting } = await setup();
      const event = { target: { value: 'Approved' } } as unknown as Event;
      component.onStatusChange(event);
      expect(component.statusFilter()).toBe('Approved' as DocumentStatus);
      httpTesting.expectOne((r) => r.url.includes('/document/my-uploads')).flush(makePagedResponse());
    });

    it('should reset currentPage to 1 on status change', async () => {
      const { component, httpTesting } = await setup();
      component.currentPage.set(3);
      const event = { target: { value: 'Rejected' } } as unknown as Event;
      component.onStatusChange(event);
      expect(component.currentPage()).toBe(1);
      httpTesting.expectOne((r) => r.url.includes('/document/my-uploads')).flush(makePagedResponse());
    });

    it('should trigger a loadPage call', async () => {
      const { component, httpTesting } = await setup();
      const spy = vi.spyOn(component, 'loadPage');
      const event = { target: { value: 'Approved' } } as unknown as Event;
      component.onStatusChange(event);
      expect(spy).toHaveBeenCalled();
      httpTesting.expectOne((r) => r.url.includes('/document/my-uploads')).flush(makePagedResponse());
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
      component.myDocuments.set(makePagedResponse([makeDoc()], { totalPages: 5 }));
      component.goToPage(3);
      expect(component.currentPage()).toBe(3);
      httpTesting.expectOne((r) => r.url.includes('/document/my-uploads')).flush(makePagedResponse());
    });
  });

  describe('pageNumbers getter', () => {
    it('should return all pages', async () => {
      const { component } = await setup();
      component.myDocuments.set(makePagedResponse([], { totalPages: 5 }));
      expect(component.pageNumbers).toEqual([1, 2, 3, 4, 5]);
    });

    it('should return empty array when totalPages is 0', async () => {
      const { component } = await setup();
      component.myDocuments.set(makePagedResponse([], { totalPages: 0 }));
      expect(component.pageNumbers).toEqual([]);
    });
  });

  describe('toggleExpand() / isExpanded()', () => {
    it('should add a docId to expandedDocIds when not present', async () => {
      const { component } = await setup();
      component.toggleExpand(100);
      expect(component.expandedDocIds().has(100)).toBe(true);
      expect(component.isExpanded(100)).toBe(true);
    });

    it('should remove a docId from expandedDocIds when already present', async () => {
      const { component } = await setup();
      component.toggleExpand(100);
      component.toggleExpand(100);
      expect(component.expandedDocIds().has(100)).toBe(false);
      expect(component.isExpanded(100)).toBe(false);
    });

    it('should allow multiple different docIds to be expanded simultaneously', async () => {
      const { component } = await setup();
      component.toggleExpand(100);
      component.toggleExpand(200);
      expect(component.expandedDocIds().has(100)).toBe(true);
      expect(component.expandedDocIds().has(200)).toBe(true);
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
      expect(component.modalError()).toBe('Could not load document preview. Please check your internet connection or try again later.');
      expect(component.isModalLoading()).toBe(false);
    });
  });

  describe('closeModal()', () => {
    it('should set isModalOpen to false and clear properties', async () => {
      const { component, httpTesting } = await setup();
      component.viewDocument(100, makeVersion());
      httpTesting.expectOne((r) => r.url.includes('/document/100/file')).flush(new Blob(['pdf'], { type: 'application/pdf' }));
      component.closeModal();
      expect(component.isModalOpen()).toBe(false);
      expect(component.modalFileUrl()).toBeNull();
      expect(component.modalFileType()).toBeNull();
      expect(component.modalFileName()).toBe('');
      expect(component.modalError()).toBeNull();
      expect(component.rawFileUrl).toBeNull();
    });
  });

  describe('openUploadModal() / closeUploadModal() / onUploadSuccess()', () => {
    it('should open and close upload modal', async () => {
      const { component } = await setup();
      component.openUploadModal();
      expect(component.isUploadOpen()).toBe(true);
      component.closeUploadModal();
      expect(component.isUploadOpen()).toBe(false);
    });

    it('should handle upload success and refresh list', async () => {
      const { component, httpTesting } = await setup();
      component.currentPage.set(3);
      component.onUploadSuccess();
      expect(component.isUploadOpen()).toBe(false);
      expect(component.currentPage()).toBe(1);
      httpTesting.expectOne((r) => r.url.includes('/document/my-uploads')).flush(makePagedResponse());
    });
  });

  describe('openReuploadModal() / closeReuploadModal()', () => {
    it('should open reupload modal and reset state', async () => {
      const { component } = await setup();
      component.openReuploadModal(100);
      expect(component.reuploadDocId()).toBe(100);
      expect(component.reuploadFile()).toBeNull();
      expect(component.reuploadError()).toBeNull();
      expect(component.reuploadLoading()).toBe(false);
      expect(component.reuploadSuccess()).toBe(false);
      expect(component.isReuploadOpen()).toBe(true);
    });

    it('should close reupload modal and reset state', async () => {
      const { component } = await setup();
      component.openReuploadModal(100);
      component.closeReuploadModal();
      expect(component.isReuploadOpen()).toBe(false);
      expect(component.reuploadDocId()).toBeNull();
    });
  });

  describe('onReuploadFileChange()', () => {
    it('should set reuploadFile for a valid file type and size', async () => {
      const { component } = await setup();
      const file = new File(['data'], 'test.png', { type: 'image/png' });
      const input = document.createElement('input') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: [file] });
      const event = { target: input } as unknown as Event;
      
      component.onReuploadFileChange(event);
      expect(component.reuploadError()).toBeNull();
      expect(component.reuploadFile()).toEqual(file);
    });

    it('should set error for an invalid file type', async () => {
      const { component } = await setup();
      const file = new File(['data'], 'test.txt', { type: 'text/plain' });
      const input = document.createElement('input') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: [file] });
      const event = { target: input } as unknown as Event;
      
      component.onReuploadFileChange(event);
      expect(component.reuploadError()).toBe('Only image files (JPEG, PNG, GIF, WebP, SVG) and PDF files are allowed.');
      expect(component.reuploadFile()).toBeNull();
    });

    it('should set error for a file exceeding 5MB', async () => {
      const { component } = await setup();
      const file = new File(['a'.repeat(6 * 1024 * 1024)], 'test.pdf', { type: 'application/pdf' });
      const input = document.createElement('input') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: [file] });
      const event = { target: input } as unknown as Event;
      
      component.onReuploadFileChange(event);
      expect(component.reuploadError()).toBe('File size must not exceed 5 MB.');
      expect(component.reuploadFile()).toBeNull();
    });

    it('should reset file if no files selected', async () => {
      const { component } = await setup();
      const input = document.createElement('input') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: [] });
      const event = { target: input } as unknown as Event;
      
      component.onReuploadFileChange(event);
      expect(component.reuploadFile()).toBeNull();
    });
  });

  describe('submitReupload()', () => {
    it('should return early if docId or file is null', async () => {
      const { component } = await setup();
      component.reuploadDocId.set(null);
      component.submitReupload();
      expect(component.reuploadError()).toBe('Please select a valid file before submitting.');
    });

    it('should set reuploadLoading and call api on success', async () => {
      const { component, httpTesting } = await setup();
      component.reuploadDocId.set(100);
      component.reuploadFile.set(new File(['data'], 'test.pdf', { type: 'application/pdf' }));
      
      component.submitReupload();
      expect(component.reuploadLoading()).toBe(true);
      
      const req = httpTesting.expectOne((r) => r.url.includes('/document/100/reupload'));
      expect(req.request.method).toBe('POST');
      req.flush({});
      
      expect(component.reuploadLoading()).toBe(false);
      expect(component.reuploadSuccess()).toBe(true);
    });

    it('should handle api error', async () => {
      const { component, httpTesting } = await setup();
      component.reuploadDocId.set(100);
      component.reuploadFile.set(new File(['data'], 'test.pdf', { type: 'application/pdf' }));
      
      component.submitReupload();
      
      httpTesting.expectOne((r) => r.url.includes('/document/100/reupload')).flush(
        { message: 'err' }, { status: 500, statusText: 'Server Error' }
      );
      
      expect(component.reuploadLoading()).toBe(false);
      expect(component.reuploadError()).toBe('Re-upload failed. Please try again.');
    });
  });

  describe('withdrawDocument()', () => {
    it('should call api if confirmed', async () => {
      const { component, httpTesting } = await setup();
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      
      component.withdrawDocument(100);
      
      const req = httpTesting.expectOne((r) => r.url.includes('/document/100'));
      expect(req.request.method).toBe('DELETE');
      req.flush({});
      
      httpTesting.expectOne((r) => r.url.includes('/document/my-uploads')).flush(makePagedResponse());
      
      expect(component.currentPage()).toBe(1);
      confirmSpy.mockRestore();
    });

    it('should do nothing if not confirmed', async () => {
      const { component, httpTesting } = await setup();
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
      
      component.withdrawDocument(100);
      
      httpTesting.verify(); // Ensures no request was made
      confirmSpy.mockRestore();
    });

    it('should handle api error and alert', async () => {
      const { component, httpTesting } = await setup();
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      
      component.withdrawDocument(100);
      
      httpTesting.expectOne((r) => r.url.includes('/document/100')).flush(
        { message: 'err' }, { status: 500, statusText: 'Server Error' }
      );
      
      expect(alertSpy).toHaveBeenCalledWith('Failed to withdraw the document. Please try again later.');
      confirmSpy.mockRestore();
      alertSpy.mockRestore();
    });
  });

  describe('formatters', () => {
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

    it('should return proper status class', async () => {
      const { component } = await setup();
      expect(component.statusClass('Approved')).toBe('badge-approved');
      expect(component.statusClass('Rejected')).toBe('badge-rejected');
      expect(component.statusClass('PendingApproval')).toBe('badge-pending');
      expect(component.statusClass('Unknown')).toBe('badge-default');
    });

    it('should return proper action class', async () => {
      const { component } = await setup();
      expect(component.actionClass('Approved')).toBe('action-approved');
      expect(component.actionClass('Rejected')).toBe('action-rejected');
      expect(component.actionClass('Forwarded')).toBe('action-forwarded');
      expect(component.actionClass('Unknown')).toBe('');
    });
  });
});
