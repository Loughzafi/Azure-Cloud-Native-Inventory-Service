namespace Inventory.Api.Models;

public sealed record InventoryItem(
    Guid Id,
    string Sku,
    string Name,
    int Quantity,
    DateTimeOffset UpdatedAtUtc);
