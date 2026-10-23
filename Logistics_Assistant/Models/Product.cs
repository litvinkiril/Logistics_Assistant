using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models
{
    [Table("products")]
    [Index(nameof(Url), Name = "idx_products_url")]
    public class Product
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        [MaxLength(256)]
        public string Name { get; set; } = string.Empty;

        [Column("url")]
        public string? Url { get; set; }
    }
}