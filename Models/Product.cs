namespace RetailMonolith.Models
{
    public class Product
    {
        public int Id { get; set; }
        public required string Sku { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public required string Currency { get; set; }
        public bool IsActive { get; set; }
        public string? Category { get; set; }
    }
}
