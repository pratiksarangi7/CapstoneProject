import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdminDepartments } from './admin-departments';

describe('AdminDepartments', () => {
  let component: AdminDepartments;
  let fixture: ComponentFixture<AdminDepartments>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminDepartments],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminDepartments);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
