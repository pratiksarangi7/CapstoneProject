import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PastApprovals } from './past-approvals';

describe('PastApprovals', () => {
  let component: PastApprovals;
  let fixture: ComponentFixture<PastApprovals>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PastApprovals],
    }).compileComponents();

    fixture = TestBed.createComponent(PastApprovals);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
