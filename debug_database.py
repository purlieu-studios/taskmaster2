#!/usr/bin/env python3
"""
Simple database debugging utility to check TaskMaster SQLite database.
Run this script to see what's in your database.
"""
import sqlite3
import os
from pathlib import Path

def find_database():
    """Find the TaskMaster database file."""
    # Common locations for the database
    local_app_data = os.environ.get('LOCALAPPDATA')
    if local_app_data:
        db_path = Path(local_app_data) / "TaskMaster" / "taskmaster.db"
        if db_path.exists():
            return str(db_path)

    # Check current directory
    if Path("taskmaster.db").exists():
        return "taskmaster.db"

    return None

def examine_database(db_path):
    """Examine the TaskMaster database."""
    print(f"Examining database: {db_path}")
    print("=" * 60)

    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()

    # Check database schema
    print("\n📋 DATABASE SCHEMA:")
    cursor.execute("SELECT name FROM sqlite_master WHERE type='table'")
    tables = cursor.fetchall()
    for table in tables:
        table_name = table[0]
        print(f"\n  📁 Table: {table_name}")
        cursor.execute(f"PRAGMA table_info({table_name})")
        columns = cursor.fetchall()
        for col in columns:
            print(f"    - {col[1]} ({col[2]})")

    # Check Projects table
    print("\n📊 PROJECTS:")
    try:
        cursor.execute("SELECT Id, Name, TaskCount, NextNumber, LastUpdated FROM Projects")
        projects = cursor.fetchall()
        if projects:
            print("  ID | Name                 | TaskCount | NextNumber | LastUpdated")
            print("  ---|----------------------|-----------|------------|-------------")
            for project in projects:
                print(f"  {project[0]:2} | {project[1]:20} | {project[2]:9} | {project[3]:10} | {project[4]}")
        else:
            print("  No projects found.")
    except sqlite3.OperationalError as e:
        print(f"  Error reading projects: {e}")

    # Check TaskSpecs table
    print("\n📝 TASK SPECS:")
    try:
        cursor.execute("SELECT Id, ProjectId, Number, Title, Type, Status, Created FROM TaskSpecs ORDER BY ProjectId, Number")
        tasks = cursor.fetchall()
        if tasks:
            print("  ID | ProjID | Num | Title                | Type    | Status | Created")
            print("  ---|--------|-----|----------------------|---------|--------|----------")
            for task in tasks:
                print(f"  {task[0]:2} | {task[1]:6} | {task[2]:3} | {task[3]:20} | {task[4]:7} | {task[5]:6} | {task[6][:10]}")
        else:
            print("  No task specs found.")
    except sqlite3.OperationalError as e:
        print(f"  Error reading tasks: {e}")

    conn.close()

def main():
    """Main function."""
    print("🔍 TaskMaster Database Debugger")
    print("=" * 40)

    db_path = find_database()
    if not db_path:
        print("❌ Could not find taskmaster.db database file.")
        print("   Expected locations:")
        print(f"   - {os.environ.get('LOCALAPPDATA', 'LOCALAPPDATA')}/TaskMaster/taskmaster.db")
        print("   - ./taskmaster.db")
        return

    examine_database(db_path)

    print("\n✅ Database examination complete!")
    print("\nIf you see issues:")
    print("1. No projects: Create a project in TaskMaster first")
    print("2. Missing NextNumber column: Database migration may have failed")
    print("3. No task specs: Try saving a spec and check again")

if __name__ == "__main__":
    main()