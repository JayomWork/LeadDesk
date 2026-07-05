# LeadDesk - ProjectPulse

LeadDesk now runs as a .NET 10 Blazor application for project and tech leads who need one workspace for accounts, tasks, standup, meetings, manager summaries, and release planning.

## Tech stack

- ASP.NET Core Blazor targeting `net10.0`
- SQL Server
- EF Core
- ASP.NET Core Identity
- ProjectPulse application/domain/infrastructure projects

## Prerequisites

- .NET 10 SDK
- SQL Server Developer/Express or LocalDB
- Visual Studio 2026 / Rider / VS Code

## Setup

Update the connection string in `ProjectPulse/appsettings.json`.

```json
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=LeadDeskDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Run the Blazor app:

```bash
dotnet restore
dotnet build LeadDesk.sln
dotnet run --project ProjectPulse/ProjectPulse.csproj
```

Manual SQL scripts are kept in `docs/` and should be reviewed/executed manually when needed.

Developer login credentials are documented in `docs/LOGIN_CREDENTIALS.md`.

## Modules

- Dashboard
- Accounts / Practices
- Team Members
- Tasks
- Meetings / Call Logs
- Daily Standup
- Account Progress Summary
- Manager Summary
- Release Planning
- Role-based access foundations

## Notes

The old MVC/Razor app has been removed. Active development should happen in the Blazor `ProjectPulse` app and the shared `ProjectPulse.Application`, `ProjectPulse.Domain`, and `ProjectPulse.Infrastructure` projects.
