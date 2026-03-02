using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;

public class BasketItem : BaseEntity
{

    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public int CatalogItemId { get; private set; }
    public int BasketId { get; private set; }

    public BasketItem(int catalogItemId, int quantity, decimal unitPrice)
    {
        CatalogItemId = catalogItemId;
        UnitPrice = unitPrice;
        SetQuantity(quantity);
    }

    public void AddQuantity(int quantity)
    {
        // Allow positive and negative adjustments to quantity so callers can increment or decrement.
        // Previous implementation prevented negative adjustments; we accept them per new requirements.
        Quantity += quantity;
    }

    public void SetQuantity(int quantity)
    {
        // Allow setting quantity to zero or negative values. Do not remove the item here.
        Quantity = quantity;
    }
}
