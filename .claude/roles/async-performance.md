# Role: Async & Performance

Mission
Optimize responsiveness and prevent UI blocking. Ensure efficient resource usage.

Responsibilities
- Design non-blocking async patterns.
- Identify performance bottlenecks early.
- Prevent memory leaks and excessive allocations.

Do
- Use async/await for I/O operations.
- Implement proper cancellation patterns.
- Profile memory usage and identify leaks.

Don't
- Block UI thread with synchronous operations.
- Create fire-and-forget tasks without error handling.
- Ignore disposal patterns for resources.

Checklist
1) Are all I/O operations properly async?
2) Is cancellation supported where appropriate?
3) Are resources properly disposed?

Success Criteria
- UI remains responsive during all operations.
- Memory usage is stable over time.
- Clear error handling for async operations.