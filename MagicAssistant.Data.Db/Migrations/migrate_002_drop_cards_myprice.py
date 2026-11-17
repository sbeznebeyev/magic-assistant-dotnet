#!/usr/bin/env python3
"""Migration 002 — cards.db schema fix.

Operation (idempotent, safe to re-run; DDL in one transaction):
  1. Cards: DROP COLUMN MyPrice   (card-level price removed; price now lives only in Deck.MyPrice)

Manual execution only:
    python3 migrate_002_drop_cards_myprice.py [path/to/cards.db]

Default DB path (relative to this script):
    ../../MagicAssistant.App/bin/Debug/net10.0/data/db/cards.db
"""
import os
import sqlite3
import sys

# DROP COLUMN requires SQLite >= 3.35
MIN_SQLITE = (3, 35)


def column_names(cur, table):
    return [r[1] for r in cur.execute(f"PRAGMA table_info({table})")]


def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    default_db = os.path.normpath(os.path.join(
        script_dir, "..", "..", "MagicAssistant.App", "bin", "Debug",
        "net10.0", "data", "db", "cards.db"))
    db_path = sys.argv[1] if len(sys.argv) > 1 else default_db

    if not os.path.isfile(db_path):
        print(f"ERROR: database not found: {db_path}")
        return 1

    version = sqlite3.sqlite_version_info
    if version < MIN_SQLITE:
        print(f"ERROR: SQLite >= {'.'.join(map(str, MIN_SQLITE))} required (DROP COLUMN), "
              f"found {'.'.join(map(str, version))}")
        return 1

    con = sqlite3.connect(db_path)
    try:
        cur = con.cursor()
        cur.execute("BEGIN")

        cards_cols = column_names(cur, "Cards")
        if "MyPrice" in cards_cols:
            cur.execute("ALTER TABLE Cards DROP COLUMN MyPrice")
            print("Cards: dropped column MyPrice")
        else:
            print("Cards: MyPrice not present — nothing to drop")

        con.commit()
    except Exception as exc:
        con.rollback()
        print(f"ERROR: migration failed, rolled back: {exc}")
        return 1
    finally:
        con.close()

    print(f"Migration 002 applied to: {db_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
