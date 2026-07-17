namespace CapstoneProjectAPI.Interfaces
{
    public interface IEmailService
    {
        /// <summary>
        /// Sends an email to the approver informing them that a new document has been
        /// uploaded and is awaiting their review.
        /// </summary>
        Task SendDocumentUploadedToApproverAsync(
            string approverEmail,
            string approverName,
            string uploaderName,
            string documentTitle,
            string documentDescription,
            string targetDepartmentName,
            string fileName,
            long fileSize,
            string mimeType,
            int documentId,
            DateTime uploadedAt);

        /// <summary>
        /// Sends an email to the document uploader informing them that their document
        /// has been fully approved.
        /// </summary>
        Task SendDocumentApprovedToUploaderAsync(
            string uploaderEmail,
            string uploaderName,
            string approverName,
            string documentTitle,
            string documentDescription,
            string targetDepartmentName,
            int documentId,
            DateTimeOffset approvedAt);
    }
}
