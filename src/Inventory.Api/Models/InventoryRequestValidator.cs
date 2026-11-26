namespace Inventory.Api.Models;

public static class InventoryRequestValidator
{
    public static Dictionary<string, string[]> Validate(InventoryItemRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Sku) || request.Sku.Trim().Length > 64)
        {
            errors["sku"] = ["SKU is required and must be 64 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            errors["name"] = ["Name is required and must be 200 characters or fewer."];
        }

        if (request.Quantity is null or < 0)
        {
            errors["quantity"] = ["Quantity is required and must be zero or greater."];
        }

        return errors;
    }
}
