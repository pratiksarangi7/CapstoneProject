export interface DepartmentUserDto {
    id: number;
    name: string;
    email: string;
    isAdmin: boolean;
    managerId: number | null;
    managerName: string | null;
    level: number;
}

export interface DepartmentResponseDto {
    id: number;
    name: string;
    users: DepartmentUserDto[];
}