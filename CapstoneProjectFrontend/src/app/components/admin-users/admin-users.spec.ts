import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminUsers } from './admin-users';
import { AdminService } from '../../services/admin.service';
import { of } from 'rxjs';
import { UserDetailsResponseDto } from '../../dtos/user-details-response.dto';

describe('AdminUsers', () => {
  let component: AdminUsers;
  let fixture: ComponentFixture<AdminUsers>;
  let mockAdminService: jasmine.SpyObj<AdminService>;

  const mockUsersResponse: UserDetailsResponseDto = {
    items: [
      {
        id: 1,
        name: 'Pratik',
        email: 'Pratik@admin.com',
        isAdmin: true,
        departmentId: 1,
        departmentName: 'Admin',
        managerId: null,
        managerName: null,
        level: 1,
        isActive: true,
      },
    ],
    pageNumber: 1,
    pageSize: 10,
    totalCount: 1,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  };

  beforeEach(async () => {
    mockAdminService = jasmine.createSpyObj('AdminService', ['getUsersApiCall']);
    mockAdminService.getUsersApiCall.and.returnValue(of(mockUsersResponse));

    await TestBed.configureTestingModule({
      imports: [AdminUsers],
      providers: [
        { provide: AdminService, useValue: mockAdminService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminUsers);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load users on init', () => {
    fixture.detectChanges();
    expect(mockAdminService.getUsersApiCall).toHaveBeenCalledWith(1, 10);
    expect(component.usersResponse().items.length).toBe(1);
    expect(component.usersResponse().items[0].name).toBe('Pratik');
  });

  it('should change page and call API again', () => {
    component.usersResponse.set({
      items: [],
      pageNumber: 1,
      pageSize: 10,
      totalCount: 15,
      totalPages: 2,
      hasPreviousPage: false,
      hasNextPage: true
    });
    
    component.goToPage(2);
    expect(component.currentPage()).toBe(2);
    expect(mockAdminService.getUsersApiCall).toHaveBeenCalledWith(2, 10);
  });

  it('should open and close modal dialog', () => {
    const user = mockUsersResponse.items[0];
    component.openUserDetails(user);
    
    expect(component.isModalOpen()).toBe(true);
    expect(component.selectedUser()).toEqual(user);

    component.closeUserDetails();
    expect(component.isModalOpen()).toBe(false);
    expect(component.selectedUser()).toBeNull();
  });

  it('should trigger placeholder changes correctly', () => {
    const user = mockUsersResponse.items[0];
    component.selectedUser.set(user);
    
    component.changeLevel(user.id, 3);
    expect(component.placeholderMessage()).toContain('Action triggered successfully');

    component.deactivateUser(user.id);
    expect(component.selectedUser()?.isActive).toBe(false);

    component.reactivateUser(user.id);
    expect(component.selectedUser()?.isActive).toBe(true);
  });
});
