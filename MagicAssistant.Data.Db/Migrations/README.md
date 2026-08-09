# Database Migrations

SQL scripts for manual database schema updates.

## Usage

Apply migrations in numerical order against your `cards.db`:

```bash
sqlite3 cards.db < Migrations/001_mig_1.sql
```

Or via the SQLite CLI:

```bash
sqlite3 cards.db ".read Migrations/001_mig_1.sql"
```
