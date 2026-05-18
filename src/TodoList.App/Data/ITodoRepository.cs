using System;
using System.Collections.Generic;
using TodoList.App.Models;

namespace TodoList.App.Data;

public interface ITodoRepository
{
    IReadOnlyList<TodoItem> QueryPage(TodoQueryOptions options, int limit, int offset);

    int QueryCount(TodoQueryOptions options);

    TodoStatusCounts GetSummaryCounts();

    long Add(string title, TodoPriority priority);

    void Rename(long id, string title);

    void UpdateNotes(long id, string notes);

    void UpdatePriority(long id, TodoPriority priority);

    void UpdateDueAtUtc(long id, DateTimeOffset? dueAtUtc);

    void SetCompleted(long id, bool isCompleted);

    void Reject(long id);

    void Restore(long id);

    void Delete(long id);

    int DeleteCompleted();
}
