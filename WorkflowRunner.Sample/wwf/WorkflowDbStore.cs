using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

public sealed class WorkflowDbStore
{
    private readonly string _connectionString;

    public WorkflowDbStore(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            throw new ArgumentException("dbPath is required.", nameof(dbPath));
        }

        var fullPath = Path.GetFullPath(dbPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        EnsureSchema();
    }

    public void CreateProcessInstance(Guid instanceId, string workflowName, IDictionary<string, object> dataFields)
    {
        var now = DateTime.UtcNow.ToString("O");
        var status = GetField(dataFields, "Status");
        var currentState = GetField(dataFields, "CurrentState");

        if (string.IsNullOrWhiteSpace(status))
        {
            status = "Created";
        }

        if (string.IsNullOrWhiteSpace(currentState))
        {
            currentState = "Start";
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO process_instances
    (instance_id, request_id, workflow_name, initiator_login, direct_manager_login, hr_approver_login,
     status, current_state, is_closed, final_status, created_utc, updated_utc, data_json)
VALUES
    ($instance_id, $request_id, $workflow_name, $initiator_login, $direct_manager_login, $hr_approver_login,
     $status, $current_state, $is_closed, $final_status, $created_utc, $updated_utc, $data_json);";
        command.Parameters.AddWithValue("$instance_id", instanceId.ToString());
        command.Parameters.AddWithValue("$request_id", GetField(dataFields, "RequestId"));
        command.Parameters.AddWithValue("$workflow_name", workflowName ?? "Workflow");
        command.Parameters.AddWithValue("$initiator_login", GetField(dataFields, "InitiatorLogin"));
        command.Parameters.AddWithValue("$direct_manager_login", GetField(dataFields, "DirectManagerLogin"));
        command.Parameters.AddWithValue("$hr_approver_login", GetField(dataFields, "HrApproverLogin"));
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$current_state", currentState);
        command.Parameters.AddWithValue("$is_closed", 0);
        command.Parameters.AddWithValue("$final_status", DBNull.Value);
        command.Parameters.AddWithValue("$created_utc", now);
        command.Parameters.AddWithValue("$updated_utc", now);
        command.Parameters.AddWithValue("$data_json", SerializeData(dataFields));
        command.ExecuteNonQuery();
    }

    public void UpdateProcessInstanceState(Guid instanceId, IDictionary<string, object> dataFields, bool? isClosed = null, string? finalStatus = null)
    {
        var status = GetField(dataFields, "Status");
        var currentState = GetField(dataFields, "CurrentState");
        if (finalStatus == null)
        {
            finalStatus = GetField(dataFields, "FinalStatus");
        }

        UpdateProcessInstance(instanceId, status, currentState, isClosed, finalStatus, SerializeData(dataFields));
    }

    public ProcessInstanceInfo? GetProcessInstance(Guid instanceId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT instance_id, request_id, is_closed
FROM process_instances
WHERE instance_id = $instance_id;";
        command.Parameters.AddWithValue("$instance_id", instanceId.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ProcessInstanceInfo
        {
            InstanceId = Guid.Parse(reader.GetString(0)),
            RequestId = reader.GetString(1),
            IsClosed = reader.GetInt32(2) == 1
        };
    }

    public void CreateTask(Guid taskId, Guid instanceId, string requestId, string assignee, string title, string body, string stepName, IDictionary<string, object>? dataFields = null)
    {
        var now = DateTime.UtcNow.ToString("O");
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO process_tasks
    (task_id, process_instance_id, request_id, assignee_login, title, body, step_name,
     status, created_utc, closed_utc, outcome, comments, data_json)
VALUES
    ($task_id, $process_instance_id, $request_id, $assignee_login, $title, $body, $step_name,
     $status, $created_utc, $closed_utc, $outcome, $comments, $data_json);";
        command.Parameters.AddWithValue("$task_id", taskId.ToString());
        command.Parameters.AddWithValue("$process_instance_id", instanceId.ToString());
        command.Parameters.AddWithValue("$request_id", requestId);
        command.Parameters.AddWithValue("$assignee_login", assignee);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$body", body);
        command.Parameters.AddWithValue("$step_name", stepName);
        command.Parameters.AddWithValue("$status", "Open");
        command.Parameters.AddWithValue("$created_utc", now);
        command.Parameters.AddWithValue("$closed_utc", DBNull.Value);
        command.Parameters.AddWithValue("$outcome", DBNull.Value);
        command.Parameters.AddWithValue("$comments", DBNull.Value);
        command.Parameters.AddWithValue("$data_json", SerializeData(dataFields));
        command.ExecuteNonQuery();
    }

    public ProcessTaskInfo? GetTask(Guid taskId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT task_id, process_instance_id, step_name, status
FROM process_tasks
WHERE task_id = $task_id;";
        command.Parameters.AddWithValue("$task_id", taskId.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ProcessTaskInfo
        {
            TaskId = Guid.Parse(reader.GetString(0)),
            ProcessInstanceId = Guid.Parse(reader.GetString(1)),
            StepName = reader.GetString(2),
            IsClosed = string.Equals(reader.GetString(3), "Closed", StringComparison.OrdinalIgnoreCase)
        };
    }

    public bool CloseTask(Guid taskId, string outcome, string? comments)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE process_tasks
SET status = $status,
    outcome = $outcome,
    comments = $comments,
    closed_utc = $closed_utc
WHERE task_id = $task_id
  AND status = 'Open';";
        command.Parameters.AddWithValue("$status", "Closed");
        command.Parameters.AddWithValue("$outcome", outcome);
        command.Parameters.AddWithValue("$comments", comments ?? string.Empty);
        command.Parameters.AddWithValue("$closed_utc", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$task_id", taskId.ToString());
        return command.ExecuteNonQuery() > 0;
    }

    public bool CloseLatestOpenTask(Guid instanceId, string stepName, string outcome, string? comments)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE process_tasks
SET status = $status,
    outcome = $outcome,
    comments = $comments,
    closed_utc = $closed_utc
WHERE task_id = (
    SELECT task_id
    FROM process_tasks
    WHERE process_instance_id = $process_instance_id
      AND step_name = $step_name
      AND status = 'Open'
    ORDER BY created_utc DESC
    LIMIT 1
);";
        command.Parameters.AddWithValue("$status", "Closed");
        command.Parameters.AddWithValue("$outcome", outcome);
        command.Parameters.AddWithValue("$comments", comments ?? string.Empty);
        command.Parameters.AddWithValue("$closed_utc", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$process_instance_id", instanceId.ToString());
        command.Parameters.AddWithValue("$step_name", stepName);
        return command.ExecuteNonQuery() > 0;
    }

    private void UpdateProcessInstance(Guid instanceId, string? status, string? currentState, bool? isClosed, string? finalStatus, string? dataJson)
    {
        var updates = new List<string>();

        if (!string.IsNullOrWhiteSpace(status))
        {
            updates.Add("status = $status");
        }

        if (!string.IsNullOrWhiteSpace(currentState))
        {
            updates.Add("current_state = $current_state");
        }

        if (isClosed.HasValue)
        {
            updates.Add("is_closed = $is_closed");
        }

        if (finalStatus != null)
        {
            updates.Add("final_status = $final_status");
        }

        if (!string.IsNullOrWhiteSpace(dataJson))
        {
            updates.Add("data_json = $data_json");
        }

        updates.Add("updated_utc = $updated_utc");

        if (updates.Count == 0)
        {
            return;
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE process_instances SET {string.Join(", ", updates)} WHERE instance_id = $instance_id;";
        command.Parameters.AddWithValue("$instance_id", instanceId.ToString());
        command.Parameters.AddWithValue("$updated_utc", DateTime.UtcNow.ToString("O"));

        if (!string.IsNullOrWhiteSpace(status))
        {
            command.Parameters.AddWithValue("$status", status);
        }

        if (!string.IsNullOrWhiteSpace(currentState))
        {
            command.Parameters.AddWithValue("$current_state", currentState);
        }

        if (isClosed.HasValue)
        {
            command.Parameters.AddWithValue("$is_closed", isClosed.Value ? 1 : 0);
        }

        if (finalStatus != null)
        {
            command.Parameters.AddWithValue("$final_status", finalStatus);
        }

        if (!string.IsNullOrWhiteSpace(dataJson))
        {
            command.Parameters.AddWithValue("$data_json", dataJson);
        }

        command.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS process_instances (
    instance_id          TEXT PRIMARY KEY,
    request_id           TEXT NOT NULL,
    workflow_name        TEXT NOT NULL,
    initiator_login      TEXT NOT NULL,
    direct_manager_login TEXT NOT NULL,
    hr_approver_login    TEXT NOT NULL,
    status               TEXT NOT NULL,
    current_state        TEXT NOT NULL,
    is_closed            INTEGER NOT NULL,
    final_status         TEXT NULL,
    created_utc          TEXT NOT NULL,
    updated_utc          TEXT NOT NULL,
    data_json            TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS process_tasks (
    task_id              TEXT PRIMARY KEY,
    process_instance_id  TEXT NOT NULL,
    request_id           TEXT NOT NULL,
    assignee_login       TEXT NOT NULL,
    title                TEXT NOT NULL,
    body                 TEXT NOT NULL,
    step_name            TEXT NOT NULL,
    status               TEXT NOT NULL,
    created_utc          TEXT NOT NULL,
    closed_utc           TEXT NULL,
    outcome              TEXT NULL,
    comments             TEXT NULL,
    data_json            TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_process_tasks_instance
    ON process_tasks(process_instance_id);";
        command.ExecuteNonQuery();
    }

    private static string GetField(IDictionary<string, object> dataFields, string key)
    {
        if (dataFields.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string SerializeData(IDictionary<string, object>? dataFields)
    {
        if (dataFields == null)
        {
            return "{}";
        }

        try
        {
            return JsonConvert.SerializeObject(dataFields);
        }
        catch
        {
            return "{}";
        }
    }
}

public sealed class ProcessInstanceInfo
{
    public Guid InstanceId { get; init; }
    public string RequestId { get; init; } = string.Empty;
    public bool IsClosed { get; init; }
}

public sealed class ProcessTaskInfo
{
    public Guid TaskId { get; init; }
    public Guid ProcessInstanceId { get; init; }
    public string StepName { get; init; } = string.Empty;
    public bool IsClosed { get; init; }
}
