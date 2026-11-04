# ChopChop Service Solution

This solution contains the assigned Milestone 3 AI Recipe Generation and
Interactive Review services and the React browser client. See the repository's
[local development guide](../docs/local-development.md),
[API contracts](../docs/api-contracts.md), and
[Milestone 3 evidence](../docs/milestone-3-ai-review-evidence.md).

```powershell
dotnet restore ChopChop.sln
dotnet build ChopChop.sln -c Release --no-restore
dotnet test ChopChop.sln -c Release
```

The services are independently runnable on ports 5081 and 5082. Each owns its
schema and migrations in the shared SQL Server database.
