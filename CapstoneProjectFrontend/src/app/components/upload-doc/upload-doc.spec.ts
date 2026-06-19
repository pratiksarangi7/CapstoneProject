import { ComponentFixture, TestBed } from '@angular/core/testing';

import { UploadDoc } from './upload-doc';

describe('UploadDoc', () => {
  let component: UploadDoc;
  let fixture: ComponentFixture<UploadDoc>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UploadDoc],
    }).compileComponents();

    fixture = TestBed.createComponent(UploadDoc);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
