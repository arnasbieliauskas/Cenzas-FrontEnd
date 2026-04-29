namespace CenzasBackend.Models
{
    public class LoanApplication
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public decimal Amount { get; set; }
        public string? LoanTerm { get; set; }
        public string? PropertyType { get; set; }
        public string? PropertyAddress { get; set; }
        public string? Other { get; set; }
        public string? Status { get; set; }
    }
}