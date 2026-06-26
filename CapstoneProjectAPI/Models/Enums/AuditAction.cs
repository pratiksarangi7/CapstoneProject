namespace CapstoneProjectAPI.Models.Enums
{
    public enum AuditAction
    {
        UserRegistered,
        UserLoggedIn,
        DocumentUploaded,
        NewVersionUploaded,
        DocumentApproved,
        DocumentRejected,
        DocumentTransfered,
        DocumentDownloaded,
        DocumentForwarded,
        DocumentWithdrawn,
        UserDeactivated,
        UserReactivated,
        DepartmentDeleted,
        PasswordChanged
    }
}