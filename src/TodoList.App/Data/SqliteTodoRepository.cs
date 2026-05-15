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
            SELECT Id, Title, IsCompleted, CreatedAtUtc, CompletedAtUtc
            FROM Todos
            ORDER BY IsCompleted ASC, CreatedAtUtc DESC, Id DESC;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var completedAtUnix = reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4);

            todos.Add(new TodoItem
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                IsCompleted = reader.GetInt64(2) == 1,
                CreatedAtUtc = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
                CompletedAtUtc = completedAtUnix.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(completedAtUnix.Value)
                    : null,
            });
        }

        return todos;
    }

    public long Add(string title)
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
            INSERT INTO Todos (Title, IsCompleted, CreatedAtUtc, CompletedAtUtc)
            VALUES ($title, 0, $createdAtUtc, NULL);
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$title", title.Trim());
        command.Parameters.AddWithValue("$createdAtUtc", createdAtUnix);

        var result = command.ExecuteScalar();
        return Convert.ToInt64(result);
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
            WHERE Id = $id;
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
        command.CommandText = "DELETE FROM Todos WHERE IsCompleted = 1;";
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
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                CreatedAtUtc INTEGER NOT NULL,
                CompletedAtUtc INTEGER NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Todos_IsCompleted_CreatedAtUtc
                ON Todos (IsCompleted, CreatedAtUtc DESC);
            """;

        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
