import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { UploadDoc } from './upload-doc';
import { DepartmentService } from '../../services/department.service';
import { DocumentService } from '../../services/document.service';
import { Department } from '../../models/department.model';

const makeDepartments = (): Department[] => [
  { id: 1, name: 'Engineering' },
  { id: 2, name: 'HR' }
];

async function setup() {
  await TestBed.configureTestingModule({
    imports: [UploadDoc],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      DepartmentService,
      DocumentService,
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(UploadDoc);
  const component = fixture.componentInstance;
  const httpTesting = TestBed.inject(HttpTestingController);

  fixture.detectChanges();
  httpTesting
    .expectOne((r) => r.url.includes('/User/departments'))
    .flush(makeDepartments());
  fixture.detectChanges();

  return { fixture, component, httpTesting };
}

describe('UploadDoc Component', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
  });

  describe('Component Initialization', () => {
    it('should create the component', async () => {
      const { component } = await setup();
      expect(component).toBeTruthy();
    });

    it('should load departments on init', async () => {
      const { component } = await setup();
      expect(component.departments()).toHaveLength(2);
      expect(component.departments()[0].name).toBe('Engineering');
    });
    
    it('should handle department load error', async () => {
      await TestBed.configureTestingModule({
        imports: [UploadDoc],
        providers: [provideHttpClient(), provideHttpClientTesting(), DepartmentService, DocumentService],
      }).compileComponents();

      const fixture = TestBed.createComponent(UploadDoc);
      const component = fixture.componentInstance;
      const httpTesting = TestBed.inject(HttpTestingController);
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      fixture.detectChanges();
      httpTesting
        .expectOne((r) => r.url.includes('/User/departments'))
        .flush('Error', { status: 500, statusText: 'Server Error' });
        
      expect(component.departments()).toEqual([]);
      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });
  });

  describe('onFileSelected', () => {
    it('should set fileError if file size exceeds 5 MB', async () => {
      const { component } = await setup();
      
      const file = new File([''], 'test.pdf', { type: 'application/pdf' });
      Object.defineProperty(file, 'size', { value: 6 * 1024 * 1024 }); // 6 MB
      const event = { target: { files: [file] } } as unknown as Event;
      
      component.onFileSelected(event);
      
      expect(component.fileError()).toBe('File size exceeds 5 MB limit.');
      expect(component.uploadModel().file).toBeNull();
    });

    it('should set fileError if file type is not allowed', async () => {
      const { component } = await setup();
      
      const file = new File([''], 'test.txt', { type: 'text/plain' });
      const event = { target: { files: [file] } } as unknown as Event;
      
      component.onFileSelected(event);
      
      expect(component.fileError()).toBe('Only PDF and image files (PNG, JPG, GIF, WEBP) are allowed.');
      expect(component.uploadModel().file).toBeNull();
    });

    it('should accept valid file and clear fileError', async () => {
      const { component } = await setup();
      
      const file = new File(['dummy'], 'test.pdf', { type: 'application/pdf' });
      const event = { target: { files: [file] } } as unknown as Event;
      
      component.fileError.set('Previous error');
      component.onFileSelected(event);
      
      expect(component.fileError()).toBeNull();
      expect(component.uploadModel().file).toEqual(file);
    });

    it('should reset file if input is cleared', async () => {
      const { component } = await setup();
      component.uploadModel.update(m => ({ ...m, file: new File([''], 't.pdf') }));
      
      const event = { target: { files: [] } } as unknown as Event;
      component.onFileSelected(event);
      
      expect(component.uploadModel().file).toBeNull();
    });
  });

  describe('handleUploadClick', () => {
    it('should alert if form is invalid', async () => {
      const { component } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      
      component.uploadModel.set({
        title: '',
        description: '',
        targetDepartmentId: '',
        file: null
      });
      
      component.handleUploadClick();
      
      expect(alertSpy).toHaveBeenCalledWith('Please fix the errors in the form before submitting.');
      alertSpy.mockRestore();
    });

    it('should call api and emit events on success', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      const uploadedSpy = vi.spyOn(component.uploaded, 'emit');
      const closeSpy = vi.spyOn(component.close, 'emit');
      
      const file = new File(['dummy content'], 'doc.pdf', { type: 'application/pdf' });
      component.uploadModel.set({
        title: 'Title',
        description: 'Desc',
        targetDepartmentId: '1',
        file
      });
      
      component.handleUploadClick();
      expect(component.progress()).toBe(true);
      
      const req = httpTesting.expectOne((r) => r.url.includes('/document'));
      expect(req.request.method).toBe('POST');
      expect(req.request.body instanceof FormData).toBe(true);
      req.flush({});
      
      expect(alertSpy).toHaveBeenCalledWith('Document uploaded successfully!');
      expect(component.progress()).toBe(false);
      expect(uploadedSpy).toHaveBeenCalled();
      expect(closeSpy).toHaveBeenCalled();
      
      alertSpy.mockRestore();
    });

    it('should alert and clear progress on api error', async () => {
      const { component, httpTesting } = await setup();
      const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
      
      const file = new File(['dummy content'], 'doc.pdf', { type: 'application/pdf' });
      component.uploadModel.set({
        title: 'Title',
        description: 'Desc',
        targetDepartmentId: '1',
        file
      });
      
      component.handleUploadClick();
      
      httpTesting.expectOne((r) => r.url.includes('/document')).flush(
        { message: 'Upload failed remotely' }, 
        { status: 400, statusText: 'Bad Request' }
      );
      
      expect(alertSpy).toHaveBeenCalledWith('Upload failed. Upload failed remotely');
      expect(component.progress()).toBe(false);
      
      alertSpy.mockRestore();
    });
  });

  describe('handleCloseClick', () => {
    it('should emit close event', async () => {
      const { component } = await setup();
      const spy = vi.spyOn(component.close, 'emit');
      component.handleCloseClick();
      expect(spy).toHaveBeenCalled();
    });
  });
});
