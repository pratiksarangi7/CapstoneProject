### auth
- login [x]
- register [x]

### admin
- get all users [x]
- set user approval level [x]
- update user department [x]
- add new departments [x]
- get all departments [x]

### user
- upload document
    - Validate the MIME type, not the extension in the file name
    - valid MIME types:
    - application/pdf (.pdf)
    - image (jpg/png)
    - Max size of file: 5 MB: enforced on client and server
    > convert image to webp

    - if same department, goes to manager, if different department, goes to another person of the chosen department at the same level of the uploader. (i am unsure as to how the person of the other department should be chosen. Give me a solid approach to this)

- view uploaded documents (basically the document history)
- modify document upload: say when user uploads a wrong document, wants to remove the doc from the db, and re upload
- upload new versions of documents 
    - when rejected by approver, shows on screen along with reason of rejection, and user can choose to reupload a new doc. New version will be created, status of doc will be changed again from rejected to pending

- Approve document: 
    - get all documents pending for approval
    - get doc action history (accepted, rejected etc)
    - approve document
        - approve document completely (no need of approval from upper levels)
        - approve document and pass it above in same department (to approver's manager)
        - approve document and transfer to other department
        > while approval, the approver will have the ability to see all past document versions by the uploader (for the current document which is to be approved), along with reasons of rejection 
    - reject document: mandatory reason of rejection in comments.

- *** implement rate limiting & throttling also. ***
- *** Postman Setup, with env variables and all ***
- *** use auto mapper to map dtos (to be done later stages) ***
- *** do unit testing ***
