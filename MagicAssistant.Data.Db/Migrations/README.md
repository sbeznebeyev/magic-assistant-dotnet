# Database Migrations

SQL scripts for manual database schema updates.

**Policy (Ron, 2026-11-22):** migrations are files only and run manually. The application never
executes migration logic over an existing DB (it refuses to start on an outdated schema and names
the file to apply); tests never execute migrations. Running a migration file is a manual action —
by the user, or by Kira only on explicit request.

## Usage

Apply migrations in numerical order against your `cards.db`:

```bash
sqlite3 cards.db < Migrations/001_mig_1.sql
```

Or via the SQLite CLI:

```bash
sqlite3 cards.db ".read Migrations/001_mig_1.sql"
```
