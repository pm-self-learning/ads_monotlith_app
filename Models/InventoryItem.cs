namespace RetailMonolith.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }
        public required string Sku { get; set; }

        public int Quantity { get; set; }

    }
}
