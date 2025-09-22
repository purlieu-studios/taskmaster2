using System.Windows;
using TaskMaster.Services;

namespace TaskMaster;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize enhanced logging system first
        EnhancedLoggingService.LogInfo("TaskMaster application starting up", "Application");

        // Initialize database on startup
        var dbService = new DatabaseService();
        dbService.InitializeDatabase();

        EnhancedLoggingService.LogInfo("Application startup completed successfully", "Application");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        EnhancedLoggingService.LogInfo("TaskMaster application shutting down", "Application");
        EnhancedLoggingService.Shutdown();
        base.OnExit(e);
    }
}