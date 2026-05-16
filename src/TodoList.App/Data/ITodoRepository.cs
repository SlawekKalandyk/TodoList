using System.Collections.Generic;
using TodoList.App.Models;

namespace TodoList.App.Data;

public interface ITodoRepository
{
    IReadOnlyList<TodoItem> GetAll();

    long Add(string title, TodoPriority priority);

    void SetCompleted(long id, bool isCompleted);

    void Reject(long id);

    void Delete(long id);

    int DeleteCompleted();
}
