using Logistics_Assistant.Services.Products;
using Logistics_Assistant.Services.Warehouses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;


namespace Logistics_Assistant.Pages.Products
{
    public class SearchModel : PageModel
    {
        private readonly ProductSearchService _search;

        public SearchModel(ProductSearchService search)
        {
            _search = search;
        }

        public async Task<IActionResult> OnGetByIdAsync(string? idStr)
        {
            if (!long.TryParse(idStr, out long searchId))
            {
                return BadRequest("ID должен быть числом.");
            }

            var products = await _search.SearchByIdAsync(
                searchId,
                HttpContext.RequestAborted);

            if (products.Count == 0)
            {
                return NotFound("Товар не найден.");
            }
            return new JsonResult(products);
        }

        public async Task<IActionResult> OnGetByNameAsync(
            string? name,
            string? offsetStr)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("Укажи название товара.");
            }

            int offset = 0;

            if (offsetStr is not null &&
                (!int.TryParse(offsetStr, out offset) || offset < 0))
            {
                return BadRequest(
                    "Смещение должно быть целым неотрицательным числом.");
            }

            var products = await _search.SearchByNameAsync(
                name,
                offset,
                HttpContext.RequestAborted);

            return new JsonResult(products);
        }

        public async Task<IActionResult> OnGetByUrlAsync(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return BadRequest("Укажи URL товара.");
            }

            var products = await _search.SearchByUrlAsync(
                url,
                HttpContext.RequestAborted);

            return new JsonResult(products);
        }
        public async Task<IActionResult> OnGetWarehouseStockByProductIdAsync(
            string? idStr,
            [FromServices] WarehouseStockService stockService)
        {
            if (!long.TryParse(idStr, out long productId) || productId <= 0)
            {
                return BadRequest(
                    "ID должен быть положительным целым числом.");
            }

            var result = await stockService.SearchProductStockByIdAsync(
                productId,
                HttpContext.RequestAborted);

            if (result is null)
            {
                return NotFound("Товар не найден.");
            }

            return new JsonResult(result);
        }
    }


}
