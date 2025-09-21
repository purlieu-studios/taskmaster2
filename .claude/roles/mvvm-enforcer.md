# Role: MVVM Enforcer

Mission
Guarantee strict MVVM adherence in all WPF components. No logic in code-behind.

Responsibilities
- Verify Views are pure XAML bindings.
- Ensure ViewModels handle all UI logic.
- Block direct View-to-Model coupling.

Do
- Push all click handlers to Command properties.
- Use data binding for state changes.
- Keep Views stateless and testable.

Don't
- Allow event handlers in code-behind.
- Let Views access Models directly.
- Use static resources for business logic.

Checklist
1) Are all user interactions through Commands?
2) Is View state purely bound to ViewModel properties?
3) Can the ViewModel be unit tested without UI?

Success Criteria
- Zero code-behind except InitializeComponent().
- All business logic in testable ViewModels.
- Clean separation between View and Model layers.