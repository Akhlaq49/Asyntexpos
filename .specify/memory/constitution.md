<!--
Sync Impact Report
Version change: (template) → 1.0.0
Added principles: I. Database Naming, II. Module Codes, III. No Dead Code, IV. Database Changes,
                  V. Tests For Every Case, VI. Company Data Isolation, VII. Books That Balance
Templates: plan-template.md "Constitution Check" gates map to principles I-VII (no template edits needed).
Shared by: Asyntexpos (backend), Asyntexposfrontend (frontend) - keep both copies identical.
-->

# Asyntex POS Constitution

Applies to the backend (`Asyntexpos`, .NET 8, MySQL / MariaDB), the frontend (`Asyntexposfrontend`, React +
TypeScript) and the drawing reader (`Drawing2Quote`) where it reads the database. Both repositories keep an
identical copy of this file.

## Core Principles

### I. Database Naming (NON-NEGOTIABLE)

Every database object name is made of **segments of exactly 4 upper-case letters or digits, joined by `_`**
(regular expression `^[A-Z0-9]{4}(_[A-Z0-9]{4})+$`).

- **Tables**: `MODULE_ENTITY` — e.g. `ACCT_DETL` (accounting, journal detail lines), `SALE_HEAD` (sales header),
  `SALE_ITEM`, `INVT_PROD`. The first segment is the module code (principle II).
- **Views**: `MODULE_VIEW_NAME` — e.g. `CATL_VIEW_PKQT` (catalogue package quote), `ACCT_VIEW_UNBL`.
- **Stored procedures**: `MODULE_PROC_NAME` — e.g. `SYST_PROC_DLCO` (delete company).
- **Columns** keep PascalCase names (`Id`, `TenantId`, `Amount`); index / constraint names are `IX_`, `UX_`, `FK_`,
  `CK_` + table + columns and are not renamed when a table is.
- Abbreviations are chosen once and reused: `HEAD` header, `ITEM` line item, `DETL` detail, `PYMT` payment,
  `RTRN` return, `RTIT` return item, `MAST` master, `STNG` settings, `CATG` category.
- The single source of every name is `Data/DbNames.cs` (backend). Code never writes a table name as a literal:
  EF maps entities through `DbNames.Tables`; raw SQL concatenates `DbNames.*` constants; SQL scripts use the
  same names. A new entity without an entry in `DbNames.Tables` stops the app at start-up.
- Servers with `lower_case_table_names = 1` store the names in lower case; queries are case-insensitive there, so
  code always writes the upper-case form.

### II. Module Codes

Code, database objects and SQL scripts are grouped by the same 4-letter module codes:

| Code | Module | Code | Module |
|------|--------|------|--------|
| SYST | system, companies, settings | SALE | sales, quotations, sales returns |
| SECU | users, roles, permissions | PRCH | purchases, purchase orders, returns |
| PRTY | parties (customers, suppliers) | FINC | expenses, capital & loans, tax types, transactions |
| INVT | products, stock, brands, units | ACCT | double-entry accounting (ledger) |
| CATL | product catalogue, solution packages, drawings | RPRT | reports, dashboard |
| AGNT | AI agent | MESG | WhatsApp messaging |

- Backend: `Controllers/<CODE>/`, `Services/<CODE>/`, `Models/<CODE>/`, `DTOs/<CODE>/`, `Database/<CODE>/`.
  Namespaces stay by layer (`ReactPosApi.Controllers`, `ReactPosApi.Services` ...).
- Frontend: `src/pages/<CODE>/`, `src/services/<CODE>/`, `src/components/<CODE>/`; shared code stays in
  `components/common`, `components/layout`, `utils`, `context`, `config`, `i18n`, `data`.
- Class, file, function and variable names follow the language conventions (C# PascalCase, TypeScript camelCase /
  PascalCase components). A new module needs a new code added to this table first.

### III. No Dead Code

The repositories contain only code that runs.

- An API endpoint with no caller (frontend, drawing reader or a documented external client) is removed with its
  service methods and DTOs. A page no menu entry, link or redirect reaches is removed with its route.
- A table or column nothing reads or writes is dropped through a migration script (principle IV).
- Frontend: `npx knip` reports no unused files, exports, types or dependencies (except tool plugins listed in its
  config); `tsc` runs with `noUnusedLocals` / `noUnusedParameters`.
- Backend: no type or method that only its declaration references; the build has no errors.
- One-off data fixes and schema history are not kept in start-up code: they live in `Database/Migrations`.

### IV. Database Changes

- A schema change that can lose or move data (drop, rename, type change) is a script in
  `Database/Migrations/YYYY-MM-DD-NN-description.sql`, idempotent where possible, with a rollback script when the
  change is reversible (e.g. renames).
- Migration scripts are **run by a person**, never by the application: back up first (`mysqldump`), run on the local
  database, test, then run on live in the documented order together with the matching backend deployment.
- On start the backend only creates a new database (`EnsureCreated`) and runs the idempotent module scripts
  (`Database/SYST/Procedures.sql`, `Database/CATL/Schema.sql`, `Database/ACCT/Schema.sql`). It refuses to start on
  a database that still has legacy table names.
- Database credentials are read from configuration and never printed.

### V. Tests For Every Case (NON-NEGOTIABLE)

- Every business rule has a unit test for each of its cases: backend with xUnit (`Tests/UnitTests`, SQLite
  in-memory database), frontend with Vitest (`*.test.ts` next to the code).
- The conventions are tests too: the naming rules (I), the module folders (II) and the entity / table registry are
  checked by unit tests, so a violation fails the build.
- Flows across the API and a real MySQL database are covered by the acceptance tests (`Tests/*.py`,
  `Database/tests/*.sql`), run against a local database before a release.
- A bug fix starts with a failing test that shows the bug.

### VI. Company Data Isolation

- Every company table has `TenantId`; EF query filters add `TenantId = current company` and `IsDeleted = 0` to every
  query. Code that bypasses the filters (`IgnoreQueryFilters`) adds the `TenantId` condition itself.
- References between company tables are composite keys `(TenantId, x)` so a row can never point at another
  company's data. Names are unique per company.

### VII. Books That Balance

- Every document that moves money or stock posts one journal entry whose debits equal its credits, in the same
  database transaction as the document. Stock leaves at moving average cost.
- Entries dated on or before the company's lock date can't change; reports read only the ledger.
- A posting failure never blocks a sale or purchase: the ledger is rebuilt from the documents.

## Workflow

- New work follows Spec Kit: `/speckit-specify` → `/speckit-plan` (its Constitution Check lists principles I–VII)
  → `/speckit-tasks` → `/speckit-implement`.
- Before a change is committed: backend builds, unit tests pass, frontend `tsc` + `knip` + `vitest` pass.
- Commits go to the working branch only; nothing is pushed without the owner's go-ahead.

## Governance

This constitution overrides other conventions in these repositories. Amendments change this file in both
repositories in one change, with a version bump (MAJOR: a principle removed or redefined; MINOR: a principle or
section added; PATCH: wording) and an updated Sync Impact Report. Reviews check new code against principles I–VII.

**Version**: 1.0.0 | **Ratified**: 2026-11-07 | **Last Amended**: 2026-11-07
