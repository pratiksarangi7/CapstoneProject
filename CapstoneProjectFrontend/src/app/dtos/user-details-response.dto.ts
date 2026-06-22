import { PaginatedResponse } from "../helpers";

export interface UserDetails {
    id: number;
    name: string;
    email: string;
    isAdmin: boolean;
    departmentId: number;
    departmentName: string;
    managerId: number | null;
    managerName: string | null;
    level: number;
    isActive: boolean
}
export type UserDetailsResponseDto = PaginatedResponse<UserDetails>;