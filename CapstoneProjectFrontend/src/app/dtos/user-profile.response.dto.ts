export interface UserProfileResponseDto {
    id: number;
    name: string;
    email: string;
    isAdmin: boolean;
    departmentId: number;
    departmentName: string;
    managerId: number;
    managerName: string;
    level: number;
    isActive: boolean;
}