# LeadDesk - Project Lead Command Center

LeadDesk is a .NET 10 MVC/Razor application for project/tech leads who need one place to manage account calls, requirements, work items, developer and QA assignments, standup updates, manager summaries, client progress, and release queues.

## Tech stack

- ASP.NET Core MVC/Razor targeting `net10.0`
- SQL Server
- EF Core Code First
- ASP.NET Core Identity
- Bootstrap 5 UI

## Prerequisites

- .NET 10 SDK
- SQL Server Developer/Express or LocalDB
- Visual Studio 2026 / Rider / VS Code

## Setup

Update the connection string in `src/LeadDesk.Web/appsettings.json`.

```json
"DefaultConnection": "Server=(localdb)\MSSQLLocalDB;Database=LeadDeskDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Then run:

```bash
cd src/LeadDesk.Web
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

Default seeded users:

| Role | Email | Password |
|---|---|---|
| Admin | admin@leaddesk.local | Admin@12345 |
| Project Lead | lead@leaddesk.local | Admin@12345 |
| Developer | dev@leaddesk.local | Admin@12345 |
| QA | qa@leaddesk.local | Admin@12345 |
| Manager | manager@leaddesk.local | Admin@12345 |
| Client Viewer | client@leaddesk.local | Admin@12345 |

## MVP modules

- Dashboard
- Accounts / Practices
- Team Members
- Work Items / Tasks
- Work Item Updates
- Meetings / Call Logs
- Standup View
- Account Progress Summary
- Manager Summary
- Release Queue
- Role-based access foundations

## Notes

This is a starter solution generated for rapid development. It intentionally keeps business logic simple in MVC controllers so Codex or your team can extend it quickly. For production SaaS, split into Clean Architecture projects later.
