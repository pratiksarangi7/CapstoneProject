import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ToApprove } from './to-approve';

describe('ToApprove', () => {
  let component: ToApprove;
  let fixture: ComponentFixture<ToApprove>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ToApprove],
    }).compileComponents();

    fixture = TestBed.createComponent(ToApprove);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
