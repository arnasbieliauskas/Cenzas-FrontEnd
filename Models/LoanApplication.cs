using System.ComponentModel.DataAnnotations;

namespace CenzasBackend.Models
{
    public class LoanApplication
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vardas privalomas.")]
        [RegularExpression(@"^[a-zA-ZąčęėįšųūžĄČĘĖĮŠŲŪŽ\s]+$", ErrorMessage = "Vardas gali turėti tik raides ir tarpus.")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "El. paštas privalomas.")]
        [EmailAddress(ErrorMessage = "Neteisingas el. pašto formatas.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Telefonas privalomas.")]
        [RegularExpression(@"^\+?\d+$", ErrorMessage = "Neteisingas telefono formatas.")]
        public string? Phone { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Suma turi būti teigiama.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Terminas privalomas.")]
        public string? LoanTerm { get; set; }

        [Required(ErrorMessage = "Turto tipas privalomas.")]
        public string? PropertyType { get; set; }

        [Required(ErrorMessage = "Turto adresas privalomas.")]
        public string? PropertyAddress { get; set; }

        public string? Other { get; set; }
        public string? Status { get; set; }
    }
}