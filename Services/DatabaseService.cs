using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
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

        using var command = new SqliteCommand(createProjectsTable, connection);
        command.ExecuteNonQuery();

        command.CommandText = createTaskSpecsTable;
        command.ExecuteNonQuery();

        command.CommandText = createDraftTasksTable;
        command.ExecuteNonQuery();
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
                LastUpdated = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                ClaudeMdPath = reader.IsDBNull(reader.GetOrdinal("ClaudeMdPath")) ? null : reader.GetString(reader.GetOrdinal("ClaudeMdPath")),
                Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata")) ? null : reader.GetString(reader.GetOrdinal("Metadata"))
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
                LastUpdated = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                ClaudeMdPath = reader.IsDBNull(reader.GetOrdinal("ClaudeMdPath")) ? null : reader.GetString(reader.GetOrdinal("ClaudeMdPath")),
                Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata")) ? null : reader.GetString(reader.GetOrdinal("Metadata"))
            };
        }

        return null;
    }

    public async Task<Project> CreateProjectAsync(string name, string? claudeMdPath = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            INSERT INTO Projects (Name, TaskCount, LastUpdated, ClaudeMdPath)
            VALUES (@Name, 0, @LastUpdated, @ClaudeMdPath);
            SELECT last_insert_rowid();";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@LastUpdated", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@ClaudeMdPath", claudeMdPath ?? (object)DBNull.Value);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());

        return new Project
        {
            Id = id,
            Name = name,
            TaskCount = 0,
            LastUpdated = DateTime.UtcNow,
            ClaudeMdPath = claudeMdPath
        };
    }

    public async Task UpdateProjectAsync(Project project)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            UPDATE Projects
            SET Name = @Name, TaskCount = @TaskCount, LastUpdated = @LastUpdated,
                ClaudeMdPath = @ClaudeMdPath, Metadata = @Metadata
            WHERE Id = @Id";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@Id", project.Id);
        command.Parameters.AddWithValue("@Name", project.Name);
        command.Parameters.AddWithValue("@TaskCount", project.TaskCount);
        command.Parameters.AddWithValue("@LastUpdated", project.LastUpdated.ToString("O"));
        command.Parameters.AddWithValue("@ClaudeMdPath", project.ClaudeMdPath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Metadata", project.Metadata ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> GetNextTaskNumberAsync(int projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Get current task count and increment
            var query = "SELECT TaskCount FROM Projects WHERE Id = @ProjectId";
            using var command = new SqliteCommand(query, connection, transaction);
            command.Parameters.AddWithValue("@ProjectId", projectId);

            var currentCount = Convert.ToInt32(await command.ExecuteScalarAsync());
            var nextNumber = currentCount + 1;

            // Update task count
            var updateQuery = "UPDATE Projects SET TaskCount = @TaskCount, LastUpdated = @LastUpdated WHERE Id = @ProjectId";
            command.CommandText = updateQuery;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@TaskCount", nextNumber);
            command.Parameters.AddWithValue("@LastUpdated", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@ProjectId", projectId);

            await command.ExecuteNonQueryAsync();
            transaction.Commit();

            return nextNumber;
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
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        if (taskSpec.Id == 0)
        {
            // Insert new task
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
            AddTaskSpecParameters(command, taskSpec);

            taskSpec.Id = Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        else
        {
            // Update existing task
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
            AddTaskSpecParameters(command, taskSpec);

            await command.ExecuteNonQueryAsync();
        }

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
            Created = DateTime.Parse(reader.GetString(reader.GetOrdinal("Created"))),
            Summary = reader.GetString(reader.GetOrdinal("Summary")),
            AcceptanceCriteria = reader.GetString(reader.GetOrdinal("AcceptanceCriteria")),
            NonGoals = reader.IsDBNull(reader.GetOrdinal("NonGoals")) ? null : reader.GetString(reader.GetOrdinal("NonGoals")),
            TestPlan = reader.GetString(reader.GetOrdinal("TestPlan")),
            ScopePaths = reader.GetString(reader.GetOrdinal("ScopePaths")),
            RequiredDocs = reader.GetString(reader.GetOrdinal("RequiredDocs")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            SuggestedTasks = reader.IsDBNull(reader.GetOrdinal("SuggestedTasks")) ? null : reader.GetString(reader.GetOrdinal("SuggestedTasks")),
            NextSteps = reader.IsDBNull(reader.GetOrdinal("NextSteps")) ? null : reader.GetString(reader.GetOrdinal("NextSteps"))
        };
    }
}