namespace Inventory.Api.Models;

public sealed record InventoryItemRequest(
    string? Sku,
    string? Name,
    int? Quantity);
