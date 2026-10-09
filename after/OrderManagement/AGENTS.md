# Order Management reference implementation
<!-- agentdocs:start -->
**Read `.agentdocs/README.md` now.** The path is relative to this instruction file (repository-root path: `.agentdocs/README.md`).
<!-- agentdocs:end -->


This is the completed Order Management lab, not the template's Todo sample.
It targets .NET 10 and Trellis 3.0.0-alpha.557.

## Read before changing code

Read `.agentdocs/README.md`, then the required
`.agentdocs/packages/trellis.core/trellis/trellis-start-here.md` in full.
Keep that router resident and open the selected cookbook bodies/package references
yourself. Do not guess APIs or delegate signature reading. References are authoritative.

Load [ServiceDefaults](.agentdocs/packages/trellis.core/trellis/trellis-api-servicedefaults.md) for composition changes. ASP work also needs the
Mediator reference; Mediator work also needs EF Core and Authorization for pipeline ordering.
Do not redeclare generated scalar factories/equality/converters or aggregate identity,
ETag, timestamps and event plumbing.

## Source tour

- `Domain\src`: generated scalar value objects, composite ShippingAddress, Order/Product/Customer aggregates.
- `Application\src\Orders`: actor-aware draft creation, actor/resource-aware cancellation,
  typed page queries, state transitions and domain-to-integration translators.
- `Acl\src`: SQLite, typed seek definitions, shared resource loader, transactional outbox/inbox
  and the in-memory broker's transport envelope.
- `Api\src\2026-11-12`: versioned MVC endpoints, Result-to-HTTP mapping and raw pagination parsing.

Keep domain rules and expected failures on the Result track. Repositories normally stage
writes; the unit-of-work pipeline commits. Translators run in the outbox relay after the
original order commit. Preserve transport MessageId and lineage across retries; business
EventId is not the inbox deduplication key.

Use protected actor/resource `Handle` hooks rather than provider re-resolution or fallback
reloads. Test them through real Mediator dispatch with fake providers/loaders. No public
actor/resource authorization-bypass overload exists.

Follow local naming/style, use ConfigureAwait(false) in library source, and keep files UTF-8
with BOM. Add a failing regression before fixing a bug or changing behavior.

## Build and run

From this solution directory:

```powershell
dotnet build OrderManagement.slnx -c Release
dotnet test --solution OrderManagement.slnx -c Release
dotnet run --project Api\src
```

Tests use Microsoft.Testing.Platform; do not use VSTest `--filter` or `--nologo`.
The Development host simulates payment after submission. Wait for payment before approval.
Production actor/broker configuration is intentionally application-owned.

## Package guidance in this training repository

AgentDocs preview.20 operates at the Git root, not at a nested solution directory.
Do not initialize/sync it directly here: that would target the Training repository root.
From the Training root, run:

```powershell
.\scripts\Test-TrainingGuidance.ps1 -Snapshot after
.\scripts\Test-TrainingGuidance.ps1 -Snapshot after -Sync
```

The script exports this snapshot into an isolated temporary Git root, restores its actual
package graph, and checks or regenerates package-managed guidance there. Sync copies only
the snapshot's managed guidance back; it never updates Training-root `.github`.
