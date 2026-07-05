# LeadDesk Login Credentials

Use these application users for local/dev testing when the database has been seeded.

| Role | Email | Password |
|---|---|---|
| Admin | admin@leaddesk.local | Admin@12345 |
| Project Lead | lead@leaddesk.local | Admin@12345 |
| Developer | dev@leaddesk.local | Admin@12345 |
| QA | qa@leaddesk.local | Admin@12345 |
| Manager | manager@leaddesk.local | Admin@12345 |
| Client Viewer | client@leaddesk.local | Admin@12345 |

Login UI:

- `ProjectPulse/Components/Pages/Login.razor`

Login handler:

- `ProjectPulse/Program.cs`

Identity user:

- `ProjectPulse.Infrastructure/ApplicationUser.cs`

Note: passwords are stored hashed in the `AspNetUsers` identity tables. This file is only a developer reference for seeded test accounts.
