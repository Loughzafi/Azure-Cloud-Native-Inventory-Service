using Inventory.Api.Models;

namespace Inventory.Api.Tests;

public sealed class InventoryRequestValidatorTests
{
    [Fact]
    public void Validate_AcceptsNonemptySkuNameAndZeroQuantity()
    {
        var errors = InventoryRequestValidator.Validate(
            new InventoryItemRequest(" SKU-1 ", " Item ", 0));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RejectsMissingFieldsAndNegativeQuantity()
    {
        var errors = InventoryRequestValidator.Validate(
            new InventoryItemRequest(" ", null, null));

        Assert.Contains("sku", errors.Keys);
        Assert.Contains("name", errors.Keys);
        Assert.Contains("quantity", errors.Keys);
    }

    [Fact]
    public void Validate_RejectsNegativeQuantity()
    {
        var errors = InventoryRequestValidator.Validate(
            new InventoryItemRequest("SKU-1", "Item", -1));

        Assert.Contains("quantity", errors.Keys);
    }

    [Fact]
    public void Validate_RejectsValuesOverTheDatabaseLimits()
    {
        var errors = InventoryRequestValidator.Validate(
            new InventoryItemRequest(new string('s', 65), new string('n', 201), 1));

        Assert.Contains("sku", errors.Keys);
        Assert.Contains("name", errors.Keys);
    }
}
