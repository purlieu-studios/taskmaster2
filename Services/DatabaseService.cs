using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System.Linq;
using TaskMaster.Models;

namespace TaskMaster.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                  "TaskMaster", "taskmaster.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath}";

        LoggingService.LogInfo($"DatabaseService initialized with path: {dbPath}", "DatabaseService");
    }

    public void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var createProjectsTable = @"
            CREATE TABLE IF NOT EXISTS Projects (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                TaskCount INTEGER NOT NULL DEFAULT 0,
                NextNumber INTEGER NOT NULL DEFAULT 1,
                LastUpdated TEXT NOT NULL,
                ClaudeMdPath TEXT,
                Metadata TEXT
            )";

        var createTaskSpecsTable = @"
            CREATE TABLE IF NOT EXISTS TaskSpecs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProjectId INTEGER NOT NULL,
                Number INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Slug TEXT NOT NULL,
                Type TEXT NOT NULL DEFAULT 'feature',
                Status INTEGER NOT NULL DEFAULT 0,
                Priority INTEGER NOT NULL DEFAULT 50,
                Created TEXT NOT NULL,
                Summary TEXT NOT NULL,
                AcceptanceCriteria TEXT NOT NULL DEFAULT '[]',
                NonGoals TEXT,
                TestPlan TEXT NOT NULL DEFAULT '[]',
                ScopePaths TEXT NOT NULL DEFAULT '[]',
                RequiredDocs TEXT NOT NULL DEFAULT '[]',
                Notes TEXT,
                SuggestedTasks TEXT,
                NextSteps TEXT,
                FOREIGN KEY (ProjectId) REFERENCES Projects (Id),
                UNIQUE(ProjectId, Number)
            )";

        var createDraftTasksTable = @"
            CREATE TABLE IF NOT EXISTS DraftTasks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProjectId INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Summary TEXT NOT NULL,
                Type TEXT NOT NULL DEFAULT 'feature',
                Created TEXT NOT NULL,
                IsNextStep INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (ProjectId) REFERENCES Projects (Id)
            )";

        var createProjectContextTable = @"
            CREATE TABLE IF NOT EXISTS ProjectContext (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProjectId INTEGER NOT NULL,
                FilePath TEXT NOT NULL,
                FileType TEXT NOT NULL,
                Content TEXT,
                LastAnalyzed TEXT NOT NULL,
                Relationships TEXT,
                FileSize INTEGER NOT NULL DEFAULT 0,
                LastModified TEXT NOT NULL,
                FileHash TEXT,
                IsRelevant INTEGER NOT NULL DEFAULT 1,
                RelevanceScore INTEGER NOT NULL DEFAULT 50,
                FOREIGN KEY (ProjectId) REFERENCES Projects (Id),
                UNIQUE(ProjectId, FilePath)
            )";

        using var command = new SqliteCommand(createProjectsTable, connection);
        command.ExecuteNonQuery();

        command.CommandText = createTaskSpecsTable;
        command.ExecuteNonQuery();

        command.CommandText = createDraftTasksTable;
        command.ExecuteNonQuery();

        command.CommandText = createProjectContextTable;
        command.ExecuteNonQuery();

        // Add basic indexes for performance (excluding Priority which may not exist yet)
        var createBasicIndexes = @"
            CREATE INDEX IF NOT EXISTS idx_taskspecs_project_number ON TaskSpecs(ProjectId, Number);
            CREATE INDEX IF NOT EXISTS idx_taskspecs_status ON TaskSpecs(Status);
            CREATE INDEX IF NOT EXISTS idx_drafttasks_project ON DraftTasks(ProjectId);
            CREATE INDEX IF NOT EXISTS idx_projectcontext_project ON ProjectContext(ProjectId);
            CREATE INDEX IF NOT EXISTS idx_projectcontext_filepath ON ProjectContext(FilePath);
            CREATE INDEX IF NOT EXISTS idx_projectcontext_filetype ON ProjectContext(FileType);
            CREATE INDEX IF NOT EXISTS idx_projectcontext_relevance ON ProjectContext(RelevanceScore);
            CREATE INDEX IF NOT EXISTS idx_projectcontext_lastmodified ON ProjectContext(LastModified);
        ";
        command.CommandText = createBasicIndexes;
        command.ExecuteNonQuery();

        // Migrate existing data: add NextNumber column if it doesn't exist
        MigrateToNextNumber(connection);

        // Migrate existing data: add Priority column if it doesn't exist
        MigrateToPriority(connection);

        // Migrate existing data: add Task Decomposition columns if they don't exist
        MigrateToTaskDecomposition(connection);

        // Migrate existing data: add ProjectDirectory column if it doesn't exist
        MigrateToProjectDirectory(connection);

        // Migrate existing data: add analysis statistics columns if they don't exist
        MigrateToAnalysisStatistics(connection);
    }

    private void MigrateToNextNumber(SqliteConnection connection)
    {
        try
        {
            // Check if NextNumber column exists
            var checkColumnQuery = "PRAGMA table_info(Projects)";
            using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
            using var reader = checkCommand.ExecuteReader();

            bool hasNextNumber = false;
            while (reader.Read())
            {
                if (reader.GetString(1) == "NextNumber")
                {
                    hasNextNumber = true;
                    break;
                }
            }
            reader.Close();

            if (!hasNextNumber)
            {
                // Add NextNumber column and initialize it
                var addColumnQuery = "ALTER TABLE Projects ADD COLUMN NextNumber INTEGER NOT NULL DEFAULT 1";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();

                // Initialize NextNumber based on existing TaskCount + 1
                var updateNextNumberQuery = "UPDATE Projects SET NextNumber = TaskCount + 1";
                using var updateCommand = new SqliteCommand(updateNextNumberQuery, connection);
                updateCommand.ExecuteNonQuery();

                LoggingService.LogInfo("Migrated database to include NextNumber field", "DatabaseService");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to migrate NextNumber field", ex, "DatabaseService");
        }
    }

    private void MigrateToPriority(SqliteConnection connection)
    {
        try
        {
            // Check if Priority column exists
            var checkColumnQuery = "PRAGMA table_info(TaskSpecs)";
            using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
            using var reader = checkCommand.ExecuteReader();

            bool hasPriority = false;
            while (reader.Read())
            {
                if (reader.GetString(1) == "Priority")
                {
                    hasPriority = true;
                    break;
                }
            }
            reader.Close();

            if (!hasPriority)
            {
                // Add Priority column with default value
                var addColumnQuery = "ALTER TABLE TaskSpecs ADD COLUMN Priority INTEGER NOT NULL DEFAULT 50";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();

                // Create index for Priority
                var createIndexQuery = "CREATE INDEX IF NOT EXISTS idx_taskspecs_priority ON TaskSpecs(Priority)";
                using var indexCommand = new SqliteCommand(createIndexQuery, connection);
                indexCommand.ExecuteNonQuery();

                LoggingService.LogInfo("Migrated database to include Priority field", "DatabaseService");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to migrate Priority field", ex, "DatabaseService");
        }
    }

    private void MigrateToTaskDecomposition(SqliteConnection connection)
    {
        try
        {
            // Check if ParentTaskId column exists
            var checkColumnQuery = "PRAGMA table_info(TaskSpecs)";
            using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
            using var reader = checkCommand.ExecuteReader();

            bool hasParentTaskId = false;
            bool hasEstimatedEffort = false;
            bool hasActualEffort = false;
            bool hasComplexityScore = false;
            bool hasIsDecomposed = false;
            bool hasDecompositionStrategy = false;

            while (reader.Read())
            {
                var columnName = reader.GetString(1);
                switch (columnName)
                {
                    case "ParentTaskId": hasParentTaskId = true; break;
                    case "EstimatedEffort": hasEstimatedEffort = true; break;
                    case "ActualEffort": hasActualEffort = true; break;
                    case "ComplexityScore": hasComplexityScore = true; break;
                    case "IsDecomposed": hasIsDecomposed = true; break;
                    case "DecompositionStrategy": hasDecompositionStrategy = true; break;
                }
            }
            reader.Close();

            // Add missing columns
            if (!hasParentTaskId)
            {
                var addColumnQuery = "ALTER TABLE TaskSpecs ADD COLUMN ParentTaskId INTEGER";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();
            }

            if (!hasEstimatedEffort)
            {
                var addColumnQuery = "ALTER TABLE TaskSpecs ADD COLUMN EstimatedEffort INTEGER NOT NULL DEFAULT 0";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();
            }

            if (!hasActualEffort)
            {
                var addColumnQuery = "ALTER TABLE TaskSpecs ADD COLUMN ActualEffort INTEGER NOT NULL DEFAULT 0";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();
            }

            if (!hasComplexityScore)
            {
                var addColumnQuery = "ALTER TABLE TaskSpecs ADD COLUMN ComplexityScore INTEGER NOT NULL DEFAULT 0";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();
            }

            if (!hasIsDecomposed)
            {
                var addColumnQuery = "ALTER TABLE TaskSpecs ADD COLUMN IsDecomposed INTEGER NOT NULL DEFAULT 0";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();
            }

            if (!hasDecompositionStrategy)
            {
                var addColumnQuery = "ALTER TABLE TaskSpecs ADD COLUMN DecompositionStrategy TEXT";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();
            }

            // Create indexes for new columns
            var createIndexes = @"
                CREATE INDEX IF NOT EXISTS idx_taskspecs_parent ON TaskSpecs(ParentTaskId);
                CREATE INDEX IF NOT EXISTS idx_taskspecs_complexity ON TaskSpecs(ComplexityScore);
                CREATE INDEX IF NOT EXISTS idx_taskspecs_decomposed ON TaskSpecs(IsDecomposed);
            ";
            using var indexCommand = new SqliteCommand(createIndexes, connection);
            indexCommand.ExecuteNonQuery();

            LoggingService.LogInfo("Migrated database to include Task Decomposition fields", "DatabaseService");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to migrate Task Decomposition fields", ex, "DatabaseService");
        }
    }

    private void MigrateToProjectDirectory(SqliteConnection connection)
    {
        try
        {
            // Check if ProjectDirectory column exists
            var checkColumnQuery = "PRAGMA table_info(Projects)";
            using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
            using var reader = checkCommand.ExecuteReader();

            bool hasProjectDirectory = false;
            while (reader.Read())
            {
                if (reader.GetString(1) == "ProjectDirectory")
                {
                    hasProjectDirectory = true;
                    break;
                }
            }
            reader.Close();

            if (!hasProjectDirectory)
            {
                // Add ProjectDirectory column
                var addColumnQuery = "ALTER TABLE Projects ADD COLUMN ProjectDirectory TEXT";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();

                LoggingService.LogInfo("Migrated database to include ProjectDirectory field", "DatabaseService");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to migrate ProjectDirectory field", ex, "DatabaseService");
        }
    }

    private void MigrateToAnalysisStatistics(SqliteConnection connection)
    {
        try
        {
            // Check which analysis statistics columns exist
            var checkColumnQuery = "PRAGMA table_info(Projects)";
            using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
            using var reader = checkCommand.ExecuteReader();

            var existingColumns = new HashSet<string>();
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1));
            }
            reader.Close();

            // Add missing analysis statistics columns
            if (!existingColumns.Contains("LastAnalysisDate"))
            {
                var addColumnQuery = "ALTER TABLE Projects ADD COLUMN LastAnalysisDate TEXT";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();
            }

            if (!existingColumns.Contains("FilesAnalyzedCount"))
            {
                var addColumnQuery = "ALTER TABLE Projects ADD COLUMN FilesAnalyzedCount INTEGER NOT NULL DEFAULT 0";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();
            }

            if (!existingColumns.Contains("LastDirectoryAnalysis"))
            {
                var addColumnQuery = "ALTER TABLE Projects ADD COLUMN LastDirectoryAnalysis TEXT";
                using var addCommand = new SqliteCommand(addColumnQuery, connection);
                addCommand.ExecuteNonQuery();
            }

            LoggingService.LogInfo("Migrated database to include analysis statistics fields", "DatabaseService");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to migrate analysis statistics fields", ex, "DatabaseService");
        }
    }

    public async Task<List<Project>> GetProjectsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var projects = new List<Project>();
        var query = "SELECT * FROM Projects ORDER BY Name";

        using var command = new SqliteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            projects.Add(new Project
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                TaskCount = reader.GetInt32(reader.GetOrdinal("TaskCount")),
                NextNumber = reader.GetInt32(reader.GetOrdinal("NextNumber")),
                LastUpdated = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                ClaudeMdPath = reader.IsDBNull(reader.GetOrdinal("ClaudeMdPath")) ? null : reader.GetString(reader.GetOrdinal("ClaudeMdPath")),
                ProjectDirectory = HasColumn(reader, "ProjectDirectory") && !reader.IsDBNull(reader.GetOrdinal("ProjectDirectory")) ? reader.GetString(reader.GetOrdinal("ProjectDirectory")) : null,
                Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata")) ? null : reader.GetString(reader.GetOrdinal("Metadata")),
                LastAnalysisDate = HasColumn(reader, "LastAnalysisDate") && !reader.IsDBNull(reader.GetOrdinal("LastAnalysisDate")) ? DateTime.Parse(reader.GetString(reader.GetOrdinal("LastAnalysisDate"))) : null,
                FilesAnalyzedCount = HasColumn(reader, "FilesAnalyzedCount") ? reader.GetInt32(reader.GetOrdinal("FilesAnalyzedCount")) : 0,
                LastDirectoryAnalysis = HasColumn(reader, "LastDirectoryAnalysis") && !reader.IsDBNull(reader.GetOrdinal("LastDirectoryAnalysis")) ? DateTime.Parse(reader.GetString(reader.GetOrdinal("LastDirectoryAnalysis"))) : null
            });
        }

        return projects;
    }

    public async Task<Project?> GetProjectByIdAsync(int projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = "SELECT * FROM Projects WHERE Id = @Id";
        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@Id", projectId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Project
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                TaskCount = reader.GetInt32(reader.GetOrdinal("TaskCount")),
                NextNumber = reader.GetInt32(reader.GetOrdinal("NextNumber")),
                LastUpdated = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                ClaudeMdPath = reader.IsDBNull(reader.GetOrdinal("ClaudeMdPath")) ? null : reader.GetString(reader.GetOrdinal("ClaudeMdPath")),
                ProjectDirectory = HasColumn(reader, "ProjectDirectory") && !reader.IsDBNull(reader.GetOrdinal("ProjectDirectory")) ? reader.GetString(reader.GetOrdinal("ProjectDirectory")) : null,
                Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata")) ? null : reader.GetString(reader.GetOrdinal("Metadata")),
                LastAnalysisDate = HasColumn(reader, "LastAnalysisDate") && !reader.IsDBNull(reader.GetOrdinal("LastAnalysisDate")) ? DateTime.Parse(reader.GetString(reader.GetOrdinal("LastAnalysisDate"))) : null,
                FilesAnalyzedCount = HasColumn(reader, "FilesAnalyzedCount") ? reader.GetInt32(reader.GetOrdinal("FilesAnalyzedCount")) : 0,
                LastDirectoryAnalysis = HasColumn(reader, "LastDirectoryAnalysis") && !reader.IsDBNull(reader.GetOrdinal("LastDirectoryAnalysis")) ? DateTime.Parse(reader.GetString(reader.GetOrdinal("LastDirectoryAnalysis"))) : null
            };
        }

        return null;
    }

    public async Task<Project> CreateProjectAsync(string name, string? projectDirectory = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Check if ProjectDirectory column exists
        var checkColumnQuery = "PRAGMA table_info(Projects)";
        using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
        using var reader = await checkCommand.ExecuteReaderAsync();

        bool hasProjectDirectory = false;
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1) == "ProjectDirectory")
            {
                hasProjectDirectory = true;
                break;
            }
        }
        reader.Close();

        string query;
        if (hasProjectDirectory)
        {
            query = @"
                INSERT INTO Projects (Name, TaskCount, NextNumber, LastUpdated, ProjectDirectory)
                VALUES (@Name, 0, 1, @LastUpdated, @ProjectDirectory);
                SELECT last_insert_rowid();";
        }
        else
        {
            // Legacy compatibility - use ClaudeMdPath column
            query = @"
                INSERT INTO Projects (Name, TaskCount, NextNumber, LastUpdated, ClaudeMdPath)
                VALUES (@Name, 0, 1, @LastUpdated, @ClaudeMdPath);
                SELECT last_insert_rowid();";
        }

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@LastUpdated", DateTime.UtcNow.ToString("O"));

        if (hasProjectDirectory)
        {
            command.Parameters.AddWithValue("@ProjectDirectory", projectDirectory ?? (object)DBNull.Value);
        }
        else
        {
            command.Parameters.AddWithValue("@ClaudeMdPath", projectDirectory ?? (object)DBNull.Value);
        }

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());

        return new Project
        {
            Id = id,
            Name = name,
            TaskCount = 0,
            LastUpdated = DateTime.UtcNow,
            ProjectDirectory = projectDirectory,
            ClaudeMdPath = hasProjectDirectory ? null : projectDirectory // Legacy compatibility
        };
    }

    public async Task UpdateProjectAsync(Project project)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Check which columns exist
        var checkColumnQuery = "PRAGMA table_info(Projects)";
        using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
        using var reader = await checkCommand.ExecuteReaderAsync();

        var existingColumns = new HashSet<string>();
        while (await reader.ReadAsync())
        {
            existingColumns.Add(reader.GetString(1));
        }
        reader.Close();

        // Check for specific columns
        var hasProjectDirectory = existingColumns.Contains("ProjectDirectory");
        var hasLastAnalysisDate = existingColumns.Contains("LastAnalysisDate");
        var hasFilesAnalyzedCount = existingColumns.Contains("FilesAnalyzedCount");
        var hasLastDirectoryAnalysis = existingColumns.Contains("LastDirectoryAnalysis");

        string query;
        if (hasProjectDirectory && hasLastAnalysisDate && hasFilesAnalyzedCount && hasLastDirectoryAnalysis)
        {
            query = @"
                UPDATE Projects
                SET Name = @Name, TaskCount = @TaskCount, LastUpdated = @LastUpdated,
                    ClaudeMdPath = @ClaudeMdPath, ProjectDirectory = @ProjectDirectory, Metadata = @Metadata,
                    LastAnalysisDate = @LastAnalysisDate, FilesAnalyzedCount = @FilesAnalyzedCount, LastDirectoryAnalysis = @LastDirectoryAnalysis
                WHERE Id = @Id";
        }
        else if (hasProjectDirectory)
        {
            query = @"
                UPDATE Projects
                SET Name = @Name, TaskCount = @TaskCount, LastUpdated = @LastUpdated,
                    ClaudeMdPath = @ClaudeMdPath, ProjectDirectory = @ProjectDirectory, Metadata = @Metadata
                WHERE Id = @Id";
        }
        else
        {
            query = @"
                UPDATE Projects
                SET Name = @Name, TaskCount = @TaskCount, LastUpdated = @LastUpdated,
                    ClaudeMdPath = @ClaudeMdPath, Metadata = @Metadata
                WHERE Id = @Id";
        }

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@Id", project.Id);
        command.Parameters.AddWithValue("@Name", project.Name);
        command.Parameters.AddWithValue("@TaskCount", project.TaskCount);
        command.Parameters.AddWithValue("@LastUpdated", project.LastUpdated.ToString("O"));
        command.Parameters.AddWithValue("@ClaudeMdPath", project.ClaudeMdPath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Metadata", project.Metadata ?? (object)DBNull.Value);

        if (hasProjectDirectory)
        {
            command.Parameters.AddWithValue("@ProjectDirectory", project.ProjectDirectory ?? (object)DBNull.Value);
        }

        if (hasLastAnalysisDate)
        {
            command.Parameters.AddWithValue("@LastAnalysisDate", project.LastAnalysisDate?.ToString("O") ?? (object)DBNull.Value);
        }

        if (hasFilesAnalyzedCount)
        {
            command.Parameters.AddWithValue("@FilesAnalyzedCount", project.FilesAnalyzedCount);
        }

        if (hasLastDirectoryAnalysis)
        {
            command.Parameters.AddWithValue("@LastDirectoryAnalysis", project.LastDirectoryAnalysis?.ToString("O") ?? (object)DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> GetNextTaskNumberAsync(int projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Get current NextNumber and increment it atomically
            var query = "SELECT NextNumber FROM Projects WHERE Id = @ProjectId";
            using var command = new SqliteCommand(query, connection, transaction);
            command.Parameters.AddWithValue("@ProjectId", projectId);

            var currentNextNumber = Convert.ToInt32(await command.ExecuteScalarAsync());
            var reservedNumber = currentNextNumber;

            // Update NextNumber, TaskCount, and LastUpdated atomically
            var updateQuery = "UPDATE Projects SET NextNumber = @NextNumber, TaskCount = TaskCount + 1, LastUpdated = @LastUpdated WHERE Id = @ProjectId";
            command.CommandText = updateQuery;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@NextNumber", currentNextNumber + 1);
            command.Parameters.AddWithValue("@LastUpdated", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@ProjectId", projectId);

            await command.ExecuteNonQueryAsync();
            transaction.Commit();

            return reservedNumber;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<List<TaskSpec>> GetRecentTasksAsync(int projectId, int limit = 10)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            SELECT * FROM TaskSpecs
            WHERE ProjectId = @ProjectId
            ORDER BY Created DESC
            LIMIT @Limit";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@ProjectId", projectId);
        command.Parameters.AddWithValue("@Limit", limit);

        var tasks = new List<TaskSpec>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tasks.Add(MapTaskSpecFromReader(reader));
        }

        return tasks;
    }

    public async Task<TaskSpec> SaveTaskSpecAsync(TaskSpec taskSpec)
    {
        LoggingService.LogInfo($"SaveTaskSpecAsync called for task: {taskSpec.Title} (Project ID: {taskSpec.ProjectId})", "DatabaseService");

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        LoggingService.LogInfo($"Database connection opened successfully", "DatabaseService");

        if (taskSpec.Id == 0)
        {
            LoggingService.LogInfo($"Inserting new task spec #{taskSpec.Number}", "DatabaseService");

            // Insert new task (without Priority for backward compatibility)
            var query = @"
                INSERT INTO TaskSpecs (
                    ProjectId, Number, Title, Slug, Type, Status, Created, Summary,
                    AcceptanceCriteria, NonGoals, TestPlan, ScopePaths, RequiredDocs,
                    Notes, SuggestedTasks, NextSteps
                ) VALUES (
                    @ProjectId, @Number, @Title, @Slug, @Type, @Status, @Created, @Summary,
                    @AcceptanceCriteria, @NonGoals, @TestPlan, @ScopePaths, @RequiredDocs,
                    @Notes, @SuggestedTasks, @NextSteps
                );
                SELECT last_insert_rowid();";

            using var command = new SqliteCommand(query, connection);
            AddTaskSpecParametersWithoutPriority(command, taskSpec);

            taskSpec.Id = Convert.ToInt32(await command.ExecuteScalarAsync());
            LoggingService.LogInfo($"Task spec inserted with ID: {taskSpec.Id}", "DatabaseService");

            // Set Priority separately if column exists
            await SetTaskPriorityIfExists(connection, taskSpec.Id, taskSpec.Priority);

            // Set decomposition fields separately if columns exist
            await SetTaskDecompositionFieldsIfExist(connection, taskSpec.Id, taskSpec);
        }
        else
        {
            // Update existing task (without Priority for backward compatibility)
            var query = @"
                UPDATE TaskSpecs SET
                    ProjectId = @ProjectId, Number = @Number, Title = @Title, Slug = @Slug,
                    Type = @Type, Status = @Status, Summary = @Summary,
                    AcceptanceCriteria = @AcceptanceCriteria, NonGoals = @NonGoals,
                    TestPlan = @TestPlan, ScopePaths = @ScopePaths, RequiredDocs = @RequiredDocs,
                    Notes = @Notes, SuggestedTasks = @SuggestedTasks, NextSteps = @NextSteps
                WHERE Id = @Id";

            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@Id", taskSpec.Id);
            AddTaskSpecParametersWithoutPriority(command, taskSpec);

            await command.ExecuteNonQueryAsync();
            LoggingService.LogInfo($"Task spec updated with ID: {taskSpec.Id}", "DatabaseService");

            // Set Priority separately if column exists
            await SetTaskPriorityIfExists(connection, taskSpec.Id, taskSpec.Priority);

            // Set decomposition fields separately if columns exist
            await SetTaskDecompositionFieldsIfExist(connection, taskSpec.Id, taskSpec);
        }

        LoggingService.LogInfo($"SaveTaskSpecAsync completed successfully for task: {taskSpec.Title}", "DatabaseService");
        return taskSpec;
    }

    private static void AddTaskSpecParameters(SqliteCommand command, TaskSpec taskSpec)
    {
        command.Parameters.AddWithValue("@ProjectId", taskSpec.ProjectId);
        command.Parameters.AddWithValue("@Number", taskSpec.Number);
        command.Parameters.AddWithValue("@Title", taskSpec.Title);
        command.Parameters.AddWithValue("@Slug", taskSpec.Slug);
        command.Parameters.AddWithValue("@Type", taskSpec.Type);
        command.Parameters.AddWithValue("@Status", (int)taskSpec.Status);
        command.Parameters.AddWithValue("@Priority", taskSpec.Priority);
        command.Parameters.AddWithValue("@Created", taskSpec.Created.ToString("O"));
        command.Parameters.AddWithValue("@Summary", taskSpec.Summary);
        command.Parameters.AddWithValue("@AcceptanceCriteria", taskSpec.AcceptanceCriteria);
        command.Parameters.AddWithValue("@NonGoals", taskSpec.NonGoals ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@TestPlan", taskSpec.TestPlan);
        command.Parameters.AddWithValue("@ScopePaths", taskSpec.ScopePaths);
        command.Parameters.AddWithValue("@RequiredDocs", taskSpec.RequiredDocs);
        command.Parameters.AddWithValue("@Notes", taskSpec.Notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SuggestedTasks", taskSpec.SuggestedTasks ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@NextSteps", taskSpec.NextSteps ?? (object)DBNull.Value);
    }

    private static void AddTaskSpecParametersWithoutPriority(SqliteCommand command, TaskSpec taskSpec)
    {
        command.Parameters.AddWithValue("@ProjectId", taskSpec.ProjectId);
        command.Parameters.AddWithValue("@Number", taskSpec.Number);
        command.Parameters.AddWithValue("@Title", taskSpec.Title);
        command.Parameters.AddWithValue("@Slug", taskSpec.Slug);
        command.Parameters.AddWithValue("@Type", taskSpec.Type);
        command.Parameters.AddWithValue("@Status", (int)taskSpec.Status);
        command.Parameters.AddWithValue("@Created", taskSpec.Created.ToString("O"));
        command.Parameters.AddWithValue("@Summary", taskSpec.Summary);
        command.Parameters.AddWithValue("@AcceptanceCriteria", taskSpec.AcceptanceCriteria);
        command.Parameters.AddWithValue("@NonGoals", taskSpec.NonGoals ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@TestPlan", taskSpec.TestPlan);
        command.Parameters.AddWithValue("@ScopePaths", taskSpec.ScopePaths);
        command.Parameters.AddWithValue("@RequiredDocs", taskSpec.RequiredDocs);
        command.Parameters.AddWithValue("@Notes", taskSpec.Notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SuggestedTasks", taskSpec.SuggestedTasks ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@NextSteps", taskSpec.NextSteps ?? (object)DBNull.Value);
    }

    private async Task SetTaskPriorityIfExists(SqliteConnection connection, int taskId, int priority)
    {
        try
        {
            // Check if Priority column exists first
            var checkColumnQuery = "PRAGMA table_info(TaskSpecs)";
            using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
            using var reader = await checkCommand.ExecuteReaderAsync();

            bool hasPriority = false;
            while (await reader.ReadAsync())
            {
                if (reader.GetString(1) == "Priority")
                {
                    hasPriority = true;
                    break;
                }
            }
            reader.Close();

            if (hasPriority)
            {
                // Priority column exists, update it
                var updateQuery = "UPDATE TaskSpecs SET Priority = @Priority WHERE Id = @Id";
                using var updateCommand = new SqliteCommand(updateQuery, connection);
                updateCommand.Parameters.AddWithValue("@Priority", priority);
                updateCommand.Parameters.AddWithValue("@Id", taskId);
                await updateCommand.ExecuteNonQueryAsync();

                LoggingService.LogInfo($"Priority {priority} set for task ID {taskId}", "DatabaseService");
            }
            else
            {
                LoggingService.LogInfo($"Priority column not found, skipping priority update for task ID {taskId}", "DatabaseService");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to set priority for task ID {taskId}", ex, "DatabaseService");
        }
    }

    private async Task SetTaskDecompositionFieldsIfExist(SqliteConnection connection, int taskId, TaskSpec taskSpec)
    {
        try
        {
            // Check which decomposition columns exist
            var checkColumnQuery = "PRAGMA table_info(TaskSpecs)";
            using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
            using var reader = await checkCommand.ExecuteReaderAsync();

            var existingColumns = new HashSet<string>();
            while (await reader.ReadAsync())
            {
                existingColumns.Add(reader.GetString(1));
            }
            reader.Close();

            // Update each decomposition field if its column exists
            var updateStatements = new List<string>();
            var parameters = new Dictionary<string, object>();

            if (existingColumns.Contains("ParentTaskId"))
            {
                updateStatements.Add("ParentTaskId = @ParentTaskId");
                parameters.Add("@ParentTaskId", taskSpec.ParentTaskId ?? (object)DBNull.Value);
            }

            if (existingColumns.Contains("EstimatedEffort"))
            {
                updateStatements.Add("EstimatedEffort = @EstimatedEffort");
                parameters.Add("@EstimatedEffort", taskSpec.EstimatedEffort);
            }

            if (existingColumns.Contains("ActualEffort"))
            {
                updateStatements.Add("ActualEffort = @ActualEffort");
                parameters.Add("@ActualEffort", taskSpec.ActualEffort);
            }

            if (existingColumns.Contains("ComplexityScore"))
            {
                updateStatements.Add("ComplexityScore = @ComplexityScore");
                parameters.Add("@ComplexityScore", taskSpec.ComplexityScore);
            }

            if (existingColumns.Contains("IsDecomposed"))
            {
                updateStatements.Add("IsDecomposed = @IsDecomposed");
                parameters.Add("@IsDecomposed", taskSpec.IsDecomposed ? 1 : 0);
            }

            if (existingColumns.Contains("DecompositionStrategy"))
            {
                updateStatements.Add("DecompositionStrategy = @DecompositionStrategy");
                parameters.Add("@DecompositionStrategy", taskSpec.DecompositionStrategy ?? (object)DBNull.Value);
            }

            // Execute update if we have any decomposition fields to update
            if (updateStatements.Count > 0)
            {
                var updateQuery = $"UPDATE TaskSpecs SET {string.Join(", ", updateStatements)} WHERE Id = @Id";
                using var updateCommand = new SqliteCommand(updateQuery, connection);

                updateCommand.Parameters.AddWithValue("@Id", taskId);
                foreach (var param in parameters)
                {
                    updateCommand.Parameters.AddWithValue(param.Key, param.Value);
                }

                await updateCommand.ExecuteNonQueryAsync();
                LoggingService.LogInfo($"Decomposition fields updated for task ID {taskId}", "DatabaseService");
            }
            else
            {
                LoggingService.LogInfo($"No decomposition columns found, skipping decomposition update for task ID {taskId}", "DatabaseService");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to set decomposition fields for task ID {taskId}", ex, "DatabaseService");
        }
    }

    private static bool HasColumn(SqliteDataReader reader, string columnName)
    {
        try
        {
            reader.GetOrdinal(columnName);
            return true;
        }
        catch (IndexOutOfRangeException)
        {
            return false;
        }
    }

    private static TaskSpec MapTaskSpecFromReader(SqliteDataReader reader)
    {
        return new TaskSpec
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            ProjectId = reader.GetInt32(reader.GetOrdinal("ProjectId")),
            Number = reader.GetInt32(reader.GetOrdinal("Number")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Slug = reader.GetString(reader.GetOrdinal("Slug")),
            Type = reader.GetString(reader.GetOrdinal("Type")),
            Status = (Models.TaskStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            Priority = HasColumn(reader, "Priority") ? reader.GetInt32(reader.GetOrdinal("Priority")) : 50,
            Created = DateTime.Parse(reader.GetString(reader.GetOrdinal("Created"))),
            Summary = reader.GetString(reader.GetOrdinal("Summary")),
            AcceptanceCriteria = reader.GetString(reader.GetOrdinal("AcceptanceCriteria")),
            NonGoals = reader.IsDBNull(reader.GetOrdinal("NonGoals")) ? null : reader.GetString(reader.GetOrdinal("NonGoals")),
            TestPlan = reader.GetString(reader.GetOrdinal("TestPlan")),
            ScopePaths = reader.GetString(reader.GetOrdinal("ScopePaths")),
            RequiredDocs = reader.GetString(reader.GetOrdinal("RequiredDocs")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            SuggestedTasks = reader.IsDBNull(reader.GetOrdinal("SuggestedTasks")) ? null : reader.GetString(reader.GetOrdinal("SuggestedTasks")),
            NextSteps = reader.IsDBNull(reader.GetOrdinal("NextSteps")) ? null : reader.GetString(reader.GetOrdinal("NextSteps")),

            // Task Decomposition fields (with backward compatibility)
            ParentTaskId = HasColumn(reader, "ParentTaskId") && !reader.IsDBNull(reader.GetOrdinal("ParentTaskId"))
                ? reader.GetInt32(reader.GetOrdinal("ParentTaskId")) : null,
            EstimatedEffort = HasColumn(reader, "EstimatedEffort") ? reader.GetInt32(reader.GetOrdinal("EstimatedEffort")) : 0,
            ActualEffort = HasColumn(reader, "ActualEffort") ? reader.GetInt32(reader.GetOrdinal("ActualEffort")) : 0,
            ComplexityScore = HasColumn(reader, "ComplexityScore") ? reader.GetInt32(reader.GetOrdinal("ComplexityScore")) : 0,
            IsDecomposed = HasColumn(reader, "IsDecomposed") ? reader.GetInt32(reader.GetOrdinal("IsDecomposed")) == 1 : false,
            DecompositionStrategy = HasColumn(reader, "DecompositionStrategy") && !reader.IsDBNull(reader.GetOrdinal("DecompositionStrategy"))
                ? reader.GetString(reader.GetOrdinal("DecompositionStrategy")) : null
        };
    }

    public async Task<List<TaskSpec>> GetTasksByProjectIdAsync(int projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var tasks = new List<TaskSpec>();
        var query = "SELECT * FROM TaskSpecs WHERE ProjectId = @ProjectId ORDER BY Number DESC";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@ProjectId", projectId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tasks.Add(MapTaskSpecFromReader(reader));
        }

        // Sort by Priority (descending) then Number (descending) in memory
        // This works whether Priority column exists or not (HasColumn handles missing columns)
        return tasks.OrderByDescending(t => t.Priority).ThenByDescending(t => t.Number).ToList();
    }

    /// <summary>
    /// Gets all tasks for a project with parent/child relationships populated
    /// </summary>
    public async Task<List<TaskSpec>> GetTasksWithHierarchyAsync(int projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var allTasks = new Dictionary<int, TaskSpec>();
        var query = "SELECT * FROM TaskSpecs WHERE ProjectId = @ProjectId ORDER BY Number";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@ProjectId", projectId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var task = MapTaskSpecFromReader(reader);
            allTasks[task.Id] = task;
        }

        // Build parent/child relationships
        foreach (var task in allTasks.Values)
        {
            if (task.ParentTaskId.HasValue && allTasks.TryGetValue(task.ParentTaskId.Value, out var parent))
            {
                task.ParentTask = parent;
                parent.ChildTasks.Add(task);
            }
        }

        // Return only root tasks (tasks without parents) - children are accessible via ChildTasks
        return allTasks.Values
            .Where(t => !t.ParentTaskId.HasValue)
            .OrderByDescending(t => t.Priority)
            .ThenByDescending(t => t.Number)
            .ToList();
    }

    /// <summary>
    /// Gets all subtasks for a given parent task
    /// </summary>
    public async Task<List<TaskSpec>> GetSubtasksAsync(int parentTaskId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var subtasks = new List<TaskSpec>();
        var query = "SELECT * FROM TaskSpecs WHERE ParentTaskId = @ParentTaskId ORDER BY Number";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@ParentTaskId", parentTaskId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            subtasks.Add(MapTaskSpecFromReader(reader));
        }

        return subtasks;
    }

    /// <summary>
    /// Gets the complexity distribution for tasks in a project
    /// </summary>
    public async Task<Dictionary<string, int>> GetComplexityDistributionAsync(int projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var distribution = new Dictionary<string, int>
        {
            ["Low (0-30)"] = 0,
            ["Medium (31-60)"] = 0,
            ["High (61-100)"] = 0
        };

        // Check if ComplexityScore column exists
        var checkColumnQuery = "PRAGMA table_info(TaskSpecs)";
        using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
        using var checkReader = await checkCommand.ExecuteReaderAsync();

        bool hasComplexityScore = false;
        while (await checkReader.ReadAsync())
        {
            if (checkReader.GetString(1) == "ComplexityScore")
            {
                hasComplexityScore = true;
                break;
            }
        }
        checkReader.Close();

        if (!hasComplexityScore)
        {
            LoggingService.LogInfo("ComplexityScore column not found, returning empty distribution", "DatabaseService");
            return distribution;
        }

        var query = "SELECT ComplexityScore FROM TaskSpecs WHERE ProjectId = @ProjectId";
        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@ProjectId", projectId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var complexity = reader.GetInt32(0);
            if (complexity <= 30)
                distribution["Low (0-30)"]++;
            else if (complexity <= 60)
                distribution["Medium (31-60)"]++;
            else
                distribution["High (61-100)"]++;
        }

        return distribution;
    }


    public async Task<bool> ImportCatalogAsync(string catalogJson)
    {
        try
        {
            LoggingService.LogInfo("Starting catalog import", "DatabaseService");

            var catalogData = Newtonsoft.Json.JsonConvert.DeserializeAnonymousType(catalogJson, new
            {
                version = "",
                exportedAt = DateTime.MinValue,
                projects = new[]
                {
                    new
                    {
                        id = 0,
                        name = "",
                        claudeMdPath = "",
                        tasks = new[]
                        {
                            new
                            {
                                id = 0,
                                title = "",
                                slug = "",
                                type = "",
                                status = "",
                                created = DateTime.MinValue,
                                summary = "",
                                nonGoals = "",
                                notes = "",
                                acceptanceCriteria = new object[0],
                                testPlan = new object[0],
                                scopePaths = new object[0],
                                requiredDocs = new object[0]
                            }
                        }
                    }
                }
            });

            if (catalogData == null || catalogData.projects == null)
            {
                LoggingService.LogError("Invalid catalog format", null, "DatabaseService");
                return false;
            }

            LoggingService.LogInfo($"Importing catalog with {catalogData.projects.Length} projects", "DatabaseService");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var projectData in catalogData.projects)
            {
                // Check if project already exists
                var existingProject = await GetProjectByNameAsync(projectData.name);
                int projectId;

                if (existingProject == null)
                {
                    // Create new project
                    var newProject = await CreateProjectAsync(projectData.name, projectData.claudeMdPath);
                    projectId = newProject.Id;
                    LoggingService.LogInfo($"Created new project: {projectData.name}", "DatabaseService");
                }
                else
                {
                    projectId = existingProject.Id;
                    LoggingService.LogInfo($"Using existing project: {projectData.name}", "DatabaseService");
                }

                // Import tasks for this project
                if (projectData.tasks != null)
                {
                    foreach (var taskData in projectData.tasks)
                    {
                        await ImportTaskAsync(connection, projectId, taskData);
                    }
                }
            }

            LoggingService.LogInfo("Catalog import completed successfully", "DatabaseService");
            return true;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to import catalog", ex, "DatabaseService");
            return false;
        }
    }

    private async Task<Project?> GetProjectByNameAsync(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Projects WHERE Name = @Name";
        command.Parameters.AddWithValue("@Name", name);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Project
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                TaskCount = reader.GetInt32(reader.GetOrdinal("TaskCount")),
                LastUpdated = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                ClaudeMdPath = reader.IsDBNull(reader.GetOrdinal("ClaudeMdPath")) ? null : reader.GetString(reader.GetOrdinal("ClaudeMdPath")),
                Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata")) ? null : reader.GetString(reader.GetOrdinal("Metadata"))
            };
        }

        return null;
    }

    private async Task ImportTaskAsync(SqliteConnection connection, int projectId, dynamic taskData)
    {
        try
        {
            // Check if task already exists (by project + number)
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM Tasks WHERE ProjectId = @ProjectId AND Number = @Number";
            checkCommand.Parameters.AddWithValue("@ProjectId", projectId);
            checkCommand.Parameters.AddWithValue("@Number", taskData.id);

            var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
            if (exists)
            {
                LoggingService.LogInfo($"Skipping existing task #{taskData.id}: {taskData.title}", "DatabaseService");
                return;
            }

            // Create new task
            var taskSpec = new TaskSpec
            {
                ProjectId = projectId,
                Number = taskData.id,
                Title = taskData.title ?? "",
                Slug = taskData.slug ?? "",
                Type = taskData.type ?? "feature",
                Status = Enum.TryParse<TaskMaster.Models.TaskStatus>(taskData.status?.ToString(), true, out TaskMaster.Models.TaskStatus status) ? status : TaskMaster.Models.TaskStatus.Todo,
                Created = taskData.created != DateTime.MinValue ? taskData.created : DateTime.UtcNow,
                Summary = taskData.summary ?? "",
                NonGoals = taskData.nonGoals?.ToString(),
                Notes = taskData.notes?.ToString(),
                AcceptanceCriteria = Newtonsoft.Json.JsonConvert.SerializeObject(taskData.acceptanceCriteria ?? new object[0]),
                TestPlan = Newtonsoft.Json.JsonConvert.SerializeObject(taskData.testPlan ?? new object[0]),
                ScopePaths = Newtonsoft.Json.JsonConvert.SerializeObject(taskData.scopePaths ?? new object[0]),
                RequiredDocs = Newtonsoft.Json.JsonConvert.SerializeObject(taskData.requiredDocs ?? new object[0])
            };

            await SaveTaskSpecAsync(taskSpec);
            LoggingService.LogInfo($"Imported task #{taskData.id}: {taskData.title}", "DatabaseService");
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to import task #{taskData.id}: {taskData.title}", ex, "DatabaseService");
        }
    }

    public async Task<bool> DeleteTaskAsync(int taskId)
    {
        LoggingService.LogInfo($"DeleteTaskAsync called for task ID: {taskId}", "DatabaseService");

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Delete the task
            var query = "DELETE FROM TaskSpecs WHERE Id = @Id";
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@Id", taskId);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                LoggingService.LogInfo($"Task #{taskId} deleted successfully", "DatabaseService");
                return true;
            }
            else
            {
                LoggingService.LogWarning($"No task found with ID: {taskId}", "DatabaseService");
                return false;
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to delete task #{taskId}", ex, "DatabaseService");
            return false;
        }
    }

    public async Task<bool> UpdateTaskPriorityAsync(int taskId, int newPriority)
    {
        LoggingService.LogInfo($"UpdateTaskPriorityAsync called for task ID: {taskId}, new priority: {newPriority}", "DatabaseService");

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Check if Priority column exists first
            var checkColumnQuery = "PRAGMA table_info(TaskSpecs)";
            using var checkCommand = new SqliteCommand(checkColumnQuery, connection);
            using var reader = await checkCommand.ExecuteReaderAsync();

            bool hasPriority = false;
            while (await reader.ReadAsync())
            {
                if (reader.GetString(1) == "Priority")
                {
                    hasPriority = true;
                    break;
                }
            }
            reader.Close();

            if (!hasPriority)
            {
                LoggingService.LogInfo($"Priority column not found, cannot update priority for task ID {taskId}", "DatabaseService");
                return false;
            }

            var query = "UPDATE TaskSpecs SET Priority = @Priority WHERE Id = @Id";
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@Priority", newPriority);
            command.Parameters.AddWithValue("@Id", taskId);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                LoggingService.LogInfo($"Task #{taskId} priority updated to {newPriority}", "DatabaseService");
                return true;
            }
            else
            {
                LoggingService.LogWarning($"No task found with ID: {taskId}", "DatabaseService");
                return false;
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to update priority for task #{taskId}", ex, "DatabaseService");
            return false;
        }
    }

    // ProjectContext CRUD operations

    public async Task<List<ProjectContext>> GetProjectContextsAsync(int projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var contexts = new List<ProjectContext>();
        var query = "SELECT * FROM ProjectContext WHERE ProjectId = @ProjectId ORDER BY FilePath";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@ProjectId", projectId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            contexts.Add(MapProjectContextFromReader(reader));
        }

        return contexts;
    }

    public async Task<ProjectContext> SaveProjectContextAsync(ProjectContext context)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        if (context.Id == 0)
        {
            // Insert new context
            var query = @"
                INSERT INTO ProjectContext (
                    ProjectId, FilePath, FileType, Content, LastAnalyzed,
                    Relationships, FileSize, LastModified, FileHash, IsRelevant, RelevanceScore
                ) VALUES (
                    @ProjectId, @FilePath, @FileType, @Content, @LastAnalyzed,
                    @Relationships, @FileSize, @LastModified, @FileHash, @IsRelevant, @RelevanceScore
                );
                SELECT last_insert_rowid();";

            using var command = new SqliteCommand(query, connection);
            AddProjectContextParameters(command, context);

            context.Id = Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        else
        {
            // Update existing context
            var query = @"
                UPDATE ProjectContext SET
                    ProjectId = @ProjectId, FilePath = @FilePath, FileType = @FileType,
                    Content = @Content, LastAnalyzed = @LastAnalyzed, Relationships = @Relationships,
                    FileSize = @FileSize, LastModified = @LastModified, FileHash = @FileHash,
                    IsRelevant = @IsRelevant, RelevanceScore = @RelevanceScore
                WHERE Id = @Id";

            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@Id", context.Id);
            AddProjectContextParameters(command, context);

            await command.ExecuteNonQueryAsync();
        }

        return context;
    }

    public async Task SaveProjectContextBatchAsync(List<ProjectContext> contexts)
    {
        if (contexts.Count == 0) return;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var context in contexts)
            {
                // Use INSERT OR REPLACE to handle duplicates
                var query = @"
                    INSERT OR REPLACE INTO ProjectContext (
                        Id, ProjectId, FilePath, FileType, Content, LastAnalyzed,
                        Relationships, FileSize, LastModified, FileHash, IsRelevant, RelevanceScore
                    ) VALUES (
                        COALESCE((SELECT Id FROM ProjectContext WHERE ProjectId = @ProjectId AND FilePath = @FilePath), NULL),
                        @ProjectId, @FilePath, @FileType, @Content, @LastAnalyzed,
                        @Relationships, @FileSize, @LastModified, @FileHash, @IsRelevant, @RelevanceScore
                    )";

                using var command = new SqliteCommand(query, connection, transaction);
                AddProjectContextParameters(command, context);
                await command.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteProjectContextByPathAsync(int projectId, string filePath)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = "DELETE FROM ProjectContext WHERE ProjectId = @ProjectId AND FilePath = @FilePath";
        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@ProjectId", projectId);
        command.Parameters.AddWithValue("@FilePath", filePath);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> ClearProjectContextAsync(int projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = "DELETE FROM ProjectContext WHERE ProjectId = @ProjectId";
        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@ProjectId", projectId);

        await command.ExecuteNonQueryAsync();
        return true;
    }

    public async Task<ProjectContext?> GetProjectContextByPathAsync(int projectId, string filePath)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = "SELECT * FROM ProjectContext WHERE ProjectId = @ProjectId AND FilePath = @FilePath";
        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@ProjectId", projectId);
        command.Parameters.AddWithValue("@FilePath", filePath);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapProjectContextFromReader(reader);
        }

        return null;
    }

    public async Task<List<ProjectContext>> GetProjectContextsByRelevanceAsync(int projectId, int minRelevanceScore = 0, int limit = 100)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            SELECT * FROM ProjectContext
            WHERE ProjectId = @ProjectId AND IsRelevant = 1 AND RelevanceScore >= @MinRelevanceScore
            ORDER BY RelevanceScore DESC, FilePath
            LIMIT @Limit";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@ProjectId", projectId);
        command.Parameters.AddWithValue("@MinRelevanceScore", minRelevanceScore);
        command.Parameters.AddWithValue("@Limit", limit);

        var contexts = new List<ProjectContext>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            contexts.Add(MapProjectContextFromReader(reader));
        }

        return contexts;
    }

    private static void AddProjectContextParameters(SqliteCommand command, ProjectContext context)
    {
        command.Parameters.AddWithValue("@ProjectId", context.ProjectId);
        command.Parameters.AddWithValue("@FilePath", context.FilePath);
        command.Parameters.AddWithValue("@FileType", context.FileType);
        command.Parameters.AddWithValue("@Content", context.Content ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LastAnalyzed", context.LastAnalyzed.ToString("O"));
        command.Parameters.AddWithValue("@Relationships", context.Relationships ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FileSize", context.FileSize);
        command.Parameters.AddWithValue("@LastModified", context.LastModified.ToString("O"));
        command.Parameters.AddWithValue("@FileHash", context.FileHash ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsRelevant", context.IsRelevant ? 1 : 0);
        command.Parameters.AddWithValue("@RelevanceScore", context.RelevanceScore);
    }

    private static ProjectContext MapProjectContextFromReader(SqliteDataReader reader)
    {
        return new ProjectContext
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            ProjectId = reader.GetInt32(reader.GetOrdinal("ProjectId")),
            FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
            FileType = reader.GetString(reader.GetOrdinal("FileType")),
            Content = reader.IsDBNull(reader.GetOrdinal("Content")) ? null : reader.GetString(reader.GetOrdinal("Content")),
            LastAnalyzed = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastAnalyzed"))),
            Relationships = reader.IsDBNull(reader.GetOrdinal("Relationships")) ? null : reader.GetString(reader.GetOrdinal("Relationships")),
            FileSize = reader.GetInt64(reader.GetOrdinal("FileSize")),
            LastModified = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastModified"))),
            FileHash = reader.IsDBNull(reader.GetOrdinal("FileHash")) ? null : reader.GetString(reader.GetOrdinal("FileHash")),
            IsRelevant = reader.GetInt32(reader.GetOrdinal("IsRelevant")) == 1,
            RelevanceScore = reader.GetInt32(reader.GetOrdinal("RelevanceScore"))
        };
    }
}