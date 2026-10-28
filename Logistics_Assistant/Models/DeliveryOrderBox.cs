using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;

namespace Logistics_Assistant.Models
{
    public enum DeliveryBoxStatus : long
    {
        [Display(Name = "Загрузка")]
        Loading = 1,

        [Display(Name = "В пути")]
        InTransit = 2,

        [Display(Name = "Выгрузка")]
        Unloading = 3,

        [Display(Name = "Доставлено")]
        Delivered = 4
    }

    [Table("delivery_order_boxes")]
    [Index(nameof(PointId), Name = "idx_delivery_order_boxes_point_id")]
    public class DeliveryOrderBox
    {
        [Column("order_id")]
        public long OrderId { get; set; }

        [Column("physical_box_id")]
        public long PhysicalBoxId { get; set; }

        [Column("point_id")]
        public long PointId { get; set; }

        [Column("status", TypeName = "bigint")]
        public DeliveryBoxStatus Status { get; set; }
            = DeliveryBoxStatus.Loading;
    }
}