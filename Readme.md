## ER Diagram
![ER DIAGRAM](ER_Diagram.png)
## 
## User Flow Diagram

```mermaid
flowchart TD
    A["User uploads doc<br/>(Dept X, Level 2)"] --> B{"Target dept<br/>== own dept?"}
    
    B -->|Yes| C["Assign to user's<br/>direct manager"]
    B -->|No| D["Find equivalent-level<br/>approver in Dept Y"]
    
    C --> E{"Reviewer reviews"}
    D --> E
    
    E -->|Reject| G["❌ Back to uploader"]
    E -->|Complete Approve| J["✅ Fully Approved"]
    
    E -->|Approve & Move Up| I["Escalate to next<br/>level approver"]
    E -->|Forward to Dept Z| H["Find entry approver<br/>in Dept Z"]
    
    I --> E
    H --> E     
    H --> J
```

---
## Prerequisites

To run and develop this project locally, ensure you have the following prerequisites installed:

### Backend (API)
- **.NET SDK**: `10.0` (Local version: `10.0.201`)
- **PostgreSQL Database**: `18.x` (Local version: `18.3`)

### Frontend
- **Node.js**: `v24.15.x` (Local version: `v24.15.0`)
- **npm**: `11.12.x` (Local version: `11.12.1`)
- **Angular CLI**: `21.2.x` (Local version: `21.2.7`)

### Testing
- **NUnit**: `4.3.2` (used for running backend unit and service tests)
- **Vitest**: `4.0.8` (used for running frontend tests)

