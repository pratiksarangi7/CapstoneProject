export interface PaginatedResponse<T> {
    items: T[];
    pageNumber: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
}

export const isTokenExpired = (token: string): boolean => {
    try {
        const tokenPayload = token.split('.')[1];
        const userClaims = JSON.parse(atob(tokenPayload));
        if (!userClaims || !userClaims.exp) {
            return false;
        }
        const expiryTime = userClaims.exp * 1000;
        return Date.now() > expiryTime;
    } catch (error) {
        console.error("Invalid token format", error);
        return true;
    }
};

export const isLoggedIn = (): boolean => {
    const token = localStorage.getItem("token");
    if (!token) return false;
    return !isTokenExpired(token);
}

export const isAdmin = (): boolean => {
    if (!isLoggedIn()) return false;
    const token = localStorage.getItem("token");
    if (!token) return false;
    try {
        const tokenPayload = token.split('.')[1];
        const userClaims = JSON.parse(atob(tokenPayload));
        const roleClaimKey = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
        const userRole = userClaims[roleClaimKey];
        console.log(userRole);
        return userRole === "Admin";
    } catch (error) {
        console.error("Invalid token format", error);
        return false;
    }
}


