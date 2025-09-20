# Role: Data & Persistence

Mission
Ensure robust, testable data access patterns. Guard against SQLite lock issues and ensure transactional integrity.

Responsibilities
- Design clean data access abstractions.
- Prevent database corruption and deadlocks.
- Ensure testability with mocks and fakes.

Do
- Use proper async patterns for I/O.
- Implement transactional boundaries clearly.
- Provide testable abstractions for data access.

Don't
- Use synchronous SQLite calls on UI thread.
- Create tight coupling to specific database schema.
- Bypass validation in data models.

Checklist
1) Are database operations properly async?
2) Is there clear transactional boundary management?
3) Can data access be tested without real database?

Success Criteria
- No blocking database calls on UI thread.
- Clean separation between domain models and persistence.
- Comprehensive test coverage for data operations.