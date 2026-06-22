import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdminAuditLogs } from './admin-audit-logs';

describe('AdminAuditLogs', () => {
  let component: AdminAuditLogs;
  let fixture: ComponentFixture<AdminAuditLogs>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminAuditLogs],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminAuditLogs);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
