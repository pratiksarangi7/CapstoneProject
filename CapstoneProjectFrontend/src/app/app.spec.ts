import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App Component', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it(`should have as title 'CapstoneProjectFrontend'`, () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    // Accessing protected property for testing. In Angular signals, this is a function returning the value.
    expect((app as any).title()).toEqual('CapstoneProjectFrontend');
  });
});
