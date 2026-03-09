using System;
using Microsoft.Data.SqlClient;
using JsonFileInstanceStore;

namespace WorkflowRunner.Sample.InstanceStore;

public sealed class SqlServerInstanceStore : XmlWorkflowInstanceStore
{
    private readonly string _connectionString;

    public SqlServerInstanceStore(Guid storeId, string connectionString) : base(storeId)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("connectionString is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
        EnsureSchema();
    }

    public override void Save(Guid instanceId, string doc)
    {
        var now = DateTime.UtcNow;
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM [dbo].[wf_instances] WHERE [instance_id] = @id)
            BEGIN
                UPDATE [dbo].[wf_instances]
                SET [state_json] = @json,
                    [updated_utc] = @updated
                WHERE [instance_id] = @id;
            END
            ELSE
            BEGIN
                INSERT INTO [dbo].[wf_instances] ([instance_id], [state_json], [created_utc], [updated_utc])
                VALUES (@id, @json, @created, @updated);
            END
            """;
        command.Parameters.AddWithValue("@id", instanceId);
        command.Parameters.AddWithValue("@json", doc);
        command.Parameters.AddWithValue("@created", now);
        command.Parameters.AddWithValue("@updated", now);
        command.ExecuteNonQuery();
    }

    public override string Load(Guid instanceId)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT [state_json] FROM [dbo].[wf_instances] WHERE [instance_id] = @id;";
        command.Parameters.AddWithValue("@id", instanceId);

        var json = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"Workflow instance not found: {instanceId}");
        }

        return json;
    }

    public override bool Clean(Guid instanceId)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM [dbo].[wf_instances] WHERE [instance_id] = @id;";
        command.Parameters.AddWithValue("@id", instanceId);
        command.ExecuteNonQuery();
        return true;
    }

    private void EnsureSchema()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'[dbo].[wf_instances]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[wf_instances]
                (
                    [instance_id] uniqueidentifier NOT NULL,
                    [state_json] nvarchar(max) NOT NULL,
                    [created_utc] datetime2 NOT NULL,
                    [updated_utc] datetime2 NOT NULL,
                    CONSTRAINT [PK_wf_instances] PRIMARY KEY ([instance_id])
                );
            END
            """;
        command.ExecuteNonQuery();
    }
}

