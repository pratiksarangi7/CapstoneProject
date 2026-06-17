export const isLoggedIn = () => {
    const token = localStorage.getItem("token");
    return token ? true : false;
}
export const isAdmin = () => {
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