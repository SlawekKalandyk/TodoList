using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TodoList.App.Models;

namespace TodoList.App.Data;

public sealed class SqliteTodoRepository : ITodoRepository
{
    private readonly string _connectionString;

    public SqliteTodoRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path cannot be empty.", nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        EnsureSchema();
    }

    public IReadOnlyList<TodoItem> GetAll()
    {
        using var connection = OpenConnection();
        const string sql =
            """
            SELECT Id, Title, Notes, Priority, IsCompleted, IsRejected, CreatedAtUtc, CompletedAtUtc, DueAtUtc
            FROM Todos
            ORDER BY IsRejected ASC, IsCompleted ASC, Priority DESC, CreatedAtUtc DESC, Id DESC;
            """;

        var rows = connection.Query<TodoRow>(sql);
        var todos = new List<TodoItem>();

        foreach (var row in rows)
        {
            todos.Add(MapToTodoItem(row));
        }

        return todos;
    }

    public IReadOnlyList<long> QueryIds(TodoQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        using var connection = OpenConnection();

        var parameters = new DynamicParameters();
        var predicates = new List<string>();

        ApplySmartViewPredicates(options, predicates, parameters);
        ApplyStatusPredicates(options, predicates);
        ApplyPriorityPredicates(options, predicates, parameters);
        ApplySearchPredicates(options, predicates, parameters);

        var sql = BuildFilteredIdQuery(options, predicates);
        return connection.Query<long>(sql, parameters).AsList();
    }

    public long Add(string title, TodoPriority priority)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Todo title cannot be empty.", nameof(title));
        }

        var createdAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var connection = OpenConnection();

        connection.Execute(
            """
            INSERT INTO Todos (Title, Notes, Priority, IsCompleted, IsRejected, CreatedAtUtc, CompletedAtUtc, DueAtUtc)
            VALUES (@title, '', @priority, 0, 0, @createdAtUtc, NULL, NULL);
            """,
            new
            {
                title = title.Trim(),
                priority = (int)priority,
                createdAtUtc = createdAtUnix,
            });

        return connection.QuerySingle<long>("SELECT last_insert_rowid();");
    }

    public void Rename(long id, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Todo title cannot be empty.", nameof(title));
        }

        using var connection = OpenConnection();
        connection.Execute(
            """
            UPDATE Todos
            SET Title = @title
            WHERE Id = @id;
            """,
            new
            {
                id,
                title = title.Trim(),
            });
    }

    public void UpdateNotes(long id, string notes)
    {
        using var connection = OpenConnection();
        connection.Execute(
            """
            UPDATE Todos
            SET Notes = @notes
            WHERE Id = @id;
            """,
            new
            {
                id,
                notes = notes ?? string.Empty,
            });
    }

    public void UpdatePriority(long id, TodoPriority priority)
    {
        using var connection = OpenConnection();
        connection.Execute(
            """
            UPDATE Todos
            SET Priority = @priority
            WHERE Id = @id;
            """,
            new
            {
                id,
                priority = (int)priority,
            });
    }

    public void UpdateDueAtUtc(long id, DateTimeOffset? dueAtUtc)
    {
        var dueAtUtcUnix = dueAtUtc?.ToUniversalTime().ToUnixTimeSeconds();

        using var connection = OpenConnection();
        connection.Execute(
            """
            UPDATE Todos
            SET DueAtUtc = @dueAtUtc
            WHERE Id = @id;
            """,
            new
            {
                id,
                dueAtUtc = dueAtUtcUnix,
            });
    }

    public void SetCompleted(long id, bool isCompleted)
    {
        var completionTimestamp = isCompleted
            ? DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            : (long?)null;

        using var connection = OpenConnection();
        connection.Execute(
            """
            UPDATE Todos
            SET IsCompleted = @isCompleted,
                CompletedAtUtc = CASE WHEN @isCompleted = 1 THEN @completedAtUtc ELSE NULL END
            WHERE Id = @id
              AND IsRejected = 0;
            """,
            new
            {
                id,
                isCompleted = isCompleted ? 1 : 0,
                completedAtUtc = completionTimestamp,
            });
    }

    public void Reject(long id)
    {
        using var connection = OpenConnection();
        connection.Execute(
            """
            UPDATE Todos
            SET IsRejected = 1,
                IsCompleted = 0,
                CompletedAtUtc = NULL
            WHERE Id = @id;
            """,
            new { id });
    }

    public void Restore(long id)
    {
        using var connection = OpenConnection();
        connection.Execute(
            """
            UPDATE Todos
            SET IsRejected = 0,
                IsCompleted = 0,
                CompletedAtUtc = NULL
            WHERE Id = @id;
            """,
            new { id });
    }

    public void Delete(long id)
    {
        using var connection = OpenConnection();
        connection.Execute("DELETE FROM Todos WHERE Id = @id;", new { id });
    }

    public int DeleteCompleted()
    {
        using var connection = OpenConnection();
        return connection.Execute("DELETE FROM Todos WHERE IsCompleted = 1 AND IsRejected = 0;");
    }

    private static void ApplySmartViewPredicates(
        TodoQueryOptions options,
        ICollection<string> predicates,
        DynamicParameters parameters)
    {
        switch (options.SmartView)
        {
            case TodoSmartView.Today:
                predicates.Add("IsCompleted = 0");
                predicates.Add("IsRejected = 0");
                predicates.Add("DueAtUtc IS NOT NULL");
                predicates.Add("DueAtUtc >= @todayStartUtc");
                predicates.Add("DueAtUtc < @tomorrowStartUtc");
                parameters.Add("todayStartUtc", options.TodayStartUtcUnix);
                parameters.Add("tomorrowStartUtc", options.TomorrowStartUtcUnix);
                break;

            case TodoSmartView.Overdue:
                predicates.Add("IsCompleted = 0");
                predicates.Add("IsRejected = 0");
                predicates.Add("DueAtUtc IS NOT NULL");
                predicates.Add("DueAtUtc < @todayStartUtc");
                parameters.Add("todayStartUtc", options.TodayStartUtcUnix);
                break;

            case TodoSmartView.DueSoon:
                predicates.Add("IsCompleted = 0");
                predicates.Add("IsRejected = 0");
                predicates.Add("DueAtUtc IS NOT NULL");
                predicates.Add("DueAtUtc >= @tomorrowStartUtc");
                predicates.Add("DueAtUtc < @dueSoonEndExclusiveUtc");
                parameters.Add("tomorrowStartUtc", options.TomorrowStartUtcUnix);
                parameters.Add("dueSoonEndExclusiveUtc", options.DueSoonEndExclusiveUtcUnix);
                break;
        }
    }

    private static void ApplyStatusPredicates(
        TodoQueryOptions options,
        ICollection<string> predicates)
    {
        if (options.SmartView != TodoSmartView.None)
        {
            return;
        }

        switch (options.StatusFilter)
        {
            case TodoFilter.Active:
                predicates.Add("IsCompleted = 0");
                predicates.Add("IsRejected = 0");
                break;

            case TodoFilter.Completed:
                predicates.Add("IsCompleted = 1");
                predicates.Add("IsRejected = 0");
                break;

            case TodoFilter.Rejected:
                predicates.Add("IsRejected = 1");
                break;
        }
    }

    private static void ApplyPriorityPredicates(
        TodoQueryOptions options,
        ICollection<string> predicates,
        DynamicParameters parameters)
    {
        if (!TryMapPriorityFilter(options.PriorityFilter, out var priority))
        {
            return;
        }

        predicates.Add("Priority = @priority");
        parameters.Add("priority", (int)priority);
    }

    private static void ApplySearchPredicates(
        TodoQueryOptions options,
        ICollection<string> predicates,
        DynamicParameters parameters)
    {
        var normalizedSearchQuery = (options.SearchQuery ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearchQuery))
        {
            return;
        }

        parameters.Add("searchPattern", BuildLikePattern(normalizedSearchQuery));

        if (options.IncludeNotesInSearch)
        {
            predicates.Add("(Title LIKE @searchPattern ESCAPE '\\' COLLATE NOCASE OR Notes LIKE @searchPattern ESCAPE '\\' COLLATE NOCASE)");
            return;
        }

        predicates.Add("Title LIKE @searchPattern ESCAPE '\\' COLLATE NOCASE");
    }

    private static string BuildFilteredIdQuery(
        TodoQueryOptions options,
        IReadOnlyCollection<string> predicates)
    {
        var sql = new StringBuilder();
        sql.AppendLine("SELECT Id");
        sql.AppendLine("FROM Todos");

        if (predicates.Count > 0)
        {
            sql.AppendLine("WHERE " + string.Join(" AND ", predicates));
        }

        sql.AppendLine("ORDER BY " + BuildOrderByClause(options));
        sql.Append(';');
        return sql.ToString();
    }

    private static string BuildOrderByClause(TodoQueryOptions options)
    {
        return options.Ordering switch
        {
            TodoOrdering.DueDate => options.OrderAscending
                ? "CASE WHEN DueAtUtc IS NULL THEN 1 ELSE 0 END ASC, DueAtUtc ASC, CreatedAtUtc DESC, Id DESC"
                : "CASE WHEN DueAtUtc IS NULL THEN 1 ELSE 0 END ASC, DueAtUtc DESC, CreatedAtUtc DESC, Id DESC",
            TodoOrdering.Priority => options.OrderAscending
                ? "Priority ASC, CreatedAtUtc DESC, Id DESC"
                : "Priority DESC, CreatedAtUtc DESC, Id DESC",
            _ => options.OrderAscending
                ? "CreatedAtUtc ASC, Id ASC"
                : "CreatedAtUtc DESC, Id DESC",
        };
    }

    private static bool TryMapPriorityFilter(TodoPriorityFilter filter, out TodoPriority priority)
    {
        switch (filter)
        {
            case TodoPriorityFilter.Minor:
                priority = TodoPriority.Minor;
                return true;

            case TodoPriorityFilter.Normal:
                priority = TodoPriority.Normal;
                return true;

            case TodoPriorityFilter.Major:
                priority = TodoPriority.Major;
                return true;

            case TodoPriorityFilter.Critical:
                priority = TodoPriority.Critical;
                return true;

            default:
                priority = default;
                return false;
        }
    }

    private static string BuildLikePattern(string value)
    {
        return $"%{EscapeLike(value)}%";
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        connection.Execute(
            """
            CREATE TABLE IF NOT EXISTS Todos
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Notes TEXT NOT NULL DEFAULT '',
                Priority INTEGER NOT NULL DEFAULT 1,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                IsRejected INTEGER NOT NULL DEFAULT 0,
                CreatedAtUtc INTEGER NOT NULL,
                CompletedAtUtc INTEGER NULL,
                DueAtUtc INTEGER NULL
            );
            """);

        EnsureColumnExists(
            connection,
            tableName: "Todos",
            columnName: "IsRejected",
            alterStatement: "ALTER TABLE Todos ADD COLUMN IsRejected INTEGER NOT NULL DEFAULT 0;");

        EnsureColumnExists(
            connection,
            tableName: "Todos",
            columnName: "Priority",
            alterStatement: "ALTER TABLE Todos ADD COLUMN Priority INTEGER NOT NULL DEFAULT 1;");

        EnsureColumnExists(
            connection,
            tableName: "Todos",
            columnName: "Notes",
            alterStatement: "ALTER TABLE Todos ADD COLUMN Notes TEXT NOT NULL DEFAULT '';");

        EnsureColumnExists(
            connection,
            tableName: "Todos",
            columnName: "DueAtUtc",
            alterStatement: "ALTER TABLE Todos ADD COLUMN DueAtUtc INTEGER NULL;");

        EnsureIndexes(connection);
    }

    private static void EnsureIndexes(SqliteConnection connection)
    {
        connection.Execute(
            """
            CREATE INDEX IF NOT EXISTS IX_Todos_IsRejected_IsCompleted_CreatedAtUtc
                ON Todos (IsRejected, IsCompleted, CreatedAtUtc DESC);

            CREATE INDEX IF NOT EXISTS IX_Todos_IsCompleted_CreatedAtUtc
                ON Todos (IsCompleted, CreatedAtUtc DESC);

            CREATE INDEX IF NOT EXISTS IX_Todos_IsRejected_IsCompleted_DueAtUtc_CreatedAtUtc
                ON Todos (IsRejected, IsCompleted, DueAtUtc, CreatedAtUtc DESC, Id DESC);

            CREATE INDEX IF NOT EXISTS IX_Todos_IsRejected_IsCompleted_Priority_CreatedAtUtc
                ON Todos (IsRejected, IsCompleted, Priority, CreatedAtUtc DESC, Id DESC);

            CREATE INDEX IF NOT EXISTS IX_Todos_DueAtUtc_CreatedAtUtc
                ON Todos (DueAtUtc, CreatedAtUtc DESC, Id DESC);

            CREATE INDEX IF NOT EXISTS IX_Todos_Priority_CreatedAtUtc
                ON Todos (Priority, CreatedAtUtc DESC, Id DESC);
            """);
    }

    private static TodoPriority ToPriority(long rawValue)
    {
        if (Enum.IsDefined(typeof(TodoPriority), (int)rawValue))
        {
            return (TodoPriority)rawValue;
        }

        return TodoPriority.Normal;
    }

    private static TodoItem MapToTodoItem(TodoRow row)
    {
        return new TodoItem
        {
            Id = row.Id,
            Title = row.Title,
            Notes = row.Notes ?? string.Empty,
            Priority = ToPriority(row.Priority),
            IsCompleted = row.IsCompleted == 1,
            IsRejected = row.IsRejected == 1,
            CreatedAtUtc = DateTimeOffset.FromUnixTimeSeconds(row.CreatedAtUtc),
            CompletedAtUtc = row.CompletedAtUtc.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(row.CompletedAtUtc.Value)
                : null,
            DueAtUtc = row.DueAtUtc.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(row.DueAtUtc.Value)
                : null,
        };
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void EnsureColumnExists(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string alterStatement)
    {
        if (ColumnExists(connection, tableName, columnName))
        {
            return;
        }

        connection.Execute(alterStatement);
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        foreach (var column in connection.Query<TableInfoRow>($"PRAGMA table_info({tableName});"))
        {
            if (string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class TodoRow
    {
        public long Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string? Notes { get; init; }

        public long Priority { get; init; }

        public long IsCompleted { get; init; }

        public long IsRejected { get; init; }

        public long CreatedAtUtc { get; init; }

        public long? CompletedAtUtc { get; init; }

        public long? DueAtUtc { get; init; }
    }

    private sealed class TableInfoRow
    {
        public string Name { get; init; } = string.Empty;
    }
}
