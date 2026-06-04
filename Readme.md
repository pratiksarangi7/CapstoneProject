## Functional Requirements

### Authentication
- register: name, email, password, department
- login: email, password
    - password hashed & stored in db
    - JWT token returned to user

### Doc validation
- Validate the MIME type, not the extension in the file name
- valid MIME types:
    - application/pdf (.pdf)
    - application/vnd.openxmlformats-officedocument.wordprocessingml.document (.docx)
    - application/msword (.doc)
    - application/vnd.openxmlformats-officedocument.spreadsheetml.sheet (.xlsx)
    - application/vnd.openxmlformats-officedocument.presentationml.presentation (.pptx)

### file limits
- Max size of file: 10 MB: enforced on client and server

### Doc Metadata
- When user uploads a document, has to mention title, description, and choose category(legal, HR, finance, technical, general etc) from dropdown
- server records: UploadedByUserId, UploadedAt, FileSize, OriginalFileName, StoredFileName (GUID-based, to prevent path traversal attacks), MimeType
- user screen should show uploaded, approved and rejected docs

### file storage
- azure blob (to do in future, now in system only)

### Document statuses
- Draft (created, but not posted for approval)
- PendingL1
- ApprovedL1
- PendingL2
- ApprovedL2
- PendingL3
- ApprovedL3

### Approver actions
- Accept: moves to next level
- Reject: Returns back to the Uploader. Mandatory comments required for rejection (so that approver can know what went wrong).

### Versioning
- Each document creation creates a document version
    - first upload: v1, subsequent v2, v3, .....
- versions are linked: each document version has a parent document id, referencing root document entity.
- old versions aren't deleted. Latest version is marked: isCurrentVersion= 'true' 
- Display Versions in the screen (v1, v2, etc)

### Audit logging
- Each action is logged. Only Create and Read, no update on this table
- Actions: 
    - User Registered
    - User LoggedIn
    - Document Uploaded
    - Document rejected (at level)
    - New version of document uploaded

### Additional
- When document is rejected and reuploaded, the approval process starts from the level at which it was rejected, not from the beginning

