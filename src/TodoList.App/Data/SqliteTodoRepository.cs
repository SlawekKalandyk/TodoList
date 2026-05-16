using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
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
        var todos = new List<TodoItem>();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Title, Priority, IsCompleted, IsRejected, CreatedAtUtc, CompletedAtUtc
            FROM Todos
            ORDER BY IsRejected ASC, IsCompleted ASC, Priority DESC, CreatedAtUtc DESC, Id DESC;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var completedAtUnix = reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6);

            todos.Add(new TodoItem
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                Priority = ToPriority(reader.GetInt64(2)),
                IsCompleted = reader.GetInt64(3) == 1,
                IsRejected = reader.GetInt64(4) == 1,
                CreatedAtUtc = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)),
                CompletedAtUtc = completedAtUnix.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(completedAtUnix.Value)
                    : null,
            });
        }

        return todos;
    }

    public long Add(string title, TodoPriority priority)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Todo title cannot be empty.", nameof(title));
        }

        var createdAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Todos (Title, Priority, IsCompleted, IsRejected, CreatedAtUtc, CompletedAtUtc)
            VALUES ($title, $priority, 0, 0, $createdAtUtc, NULL);
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$title", title.Trim());
        command.Parameters.AddWithValue("$priority", (int)priority);
        command.Parameters.AddWithValue("$createdAtUtc", createdAtUnix);

        var result = command.ExecuteScalar();
        return Convert.ToInt64(result);
    }

    public void Rename(long id, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Todo title cannot be empty.", nameof(title));
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Todos
            SET Title = $title
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$title", title.Trim());
        command.ExecuteNonQuery();
    }

    public void SetCompleted(long id, bool isCompleted)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Todos
            SET IsCompleted = $isCompleted,
                CompletedAtUtc = CASE WHEN $isCompleted = 1 THEN $completedAtUtc ELSE NULL END
            WHERE Id = $id
              AND IsRejected = 0;
            """;

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$isCompleted", isCompleted ? 1 : 0);
        command.Parameters.AddWithValue(
            "$completedAtUtc",
            isCompleted
                ? DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                : DBNull.Value);

        command.ExecuteNonQuery();
    }

    public void Reject(long id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Todos
            SET IsRejected = 1,
                IsCompleted = 0,
                CompletedAtUtc = NULL
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Todos WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public int DeleteCompleted()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Todos WHERE IsCompleted = 1 AND IsRejected = 0;";
        return command.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Todos
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Priority INTEGER NOT NULL DEFAULT 1,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                IsRejected INTEGER NOT NULL DEFAULT 0,
                CreatedAtUtc INTEGER NOT NULL,
                CompletedAtUtc INTEGER NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Todos_IsRejected_IsCompleted_CreatedAtUtc
                ON Todos (IsRejected, IsCompleted, CreatedAtUtc DESC);

            CREATE INDEX IF NOT EXISTS IX_Todos_IsCompleted_CreatedAtUtc
                ON Todos (IsCompleted, CreatedAtUtc DESC);
            """;

        command.ExecuteNonQuery();

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
    }

    private static TodoPriority ToPriority(long rawValue)
    {
        if (Enum.IsDefined(typeof(TodoPriority), (int)rawValue))
        {
            return (TodoPriority)rawValue;
        }

        return TodoPriority.Normal;
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

        using var alter = connection.CreateCommand();
        alter.CommandText = alterStatement;
        alter.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
