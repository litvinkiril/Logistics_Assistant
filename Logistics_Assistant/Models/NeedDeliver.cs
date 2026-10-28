using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models
{
    [Table("need_to_deliver")]
    [Index(nameof(LogisticPointId), Name = "idx_need_to_deliver_logistic_point_id")]
    public class NeedToDeliver
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("product_id")]
        public long? ProductId { get; set; }

        [Column("box_id")]
        public long? BoxId { get; set; }

        [Column("quantity")]
        public int? Quantity { get; set; }

        [Column("logistic_point_id")]
        public long? LogisticPointId { get; set; }
    }
}