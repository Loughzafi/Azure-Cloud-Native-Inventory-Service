using Inventory.Api.Models;
using Npgsql;

namespace Inventory.Api.Endpoints;

public static class InventoryEndpoints
{
    public static RouteGroupBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/inventory");

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT id, sku, name, quantity, updated_at
            FROM inventory_items
            ORDER BY sku
            LIMIT 500
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<InventoryItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }

        return Results.Ok(items);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT id, sku, name, quantity, updated_at
            FROM inventory_items
            WHERE id = @id
            """,
            connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? Results.Ok(ReadItem(reader))
            : Results.NotFound();
    }

    private static async Task<IResult> CreateAsync(
        InventoryItemRequest? request,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "A JSON request body is required." });
        }

        var errors = InventoryRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO inventory_items (id, sku, name, quantity)
            VALUES (@id, @sku, @name, @quantity)
            RETURNING id, sku, name, quantity, updated_at
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        AddRequestParameters(command, request);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return Results.Created($"/api/inventory/{reader.GetGuid(0)}", ReadItem(reader));
        }
        catch (PostgresException exception) when (IsSkuConflict(exception))
        {
            return Results.Conflict(new { message = "An item with that SKU already exists." });
        }
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        InventoryItemRequest? request,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "A JSON request body is required." });
        }

        var errors = InventoryRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            UPDATE inventory_items
            SET sku = @sku, name = @name, quantity = @quantity, updated_at = now()
            WHERE id = @id
            RETURNING id, sku, name, quantity, updated_at
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        AddRequestParameters(command, request);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? Results.Ok(ReadItem(reader))
                : Results.NotFound();
        }
        catch (PostgresException exception) when (IsSkuConflict(exception))
        {
            return Results.Conflict(new { message = "An item with that SKU already exists." });
        }
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "DELETE FROM inventory_items WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected == 0 ? Results.NotFound() : Results.NoContent();
    }

    private static void AddRequestParameters(NpgsqlCommand command, InventoryItemRequest request)
    {
        command.Parameters.AddWithValue("sku", request.Sku!.Trim());
        command.Parameters.AddWithValue("name", request.Name!.Trim());
        command.Parameters.AddWithValue("quantity", request.Quantity!.Value);
    }

    private static InventoryItem ReadItem(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetFieldValue<DateTimeOffset>(4));

    private static bool IsSkuConflict(PostgresException exception) =>
        exception.SqlState == PostgresErrorCodes.UniqueViolation
        && exception.ConstraintName == "ux_inventory_items_sku_normalized";
}
