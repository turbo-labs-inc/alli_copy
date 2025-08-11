using Microsoft.AspNetCore.Mvc;

namespace Custom.Gravitate.WebAPI.Core.Controllers
{
    [ApiController]
    [Route("api/fake/tradeentry")]
    public class FakeCustomTradeEntryApiController : ControllerBase
    {
        // GET: /api/fake/tradeentry/externalcompanies
        [HttpGet("externalcompanies")]
        public IActionResult GetExternalCompanies([FromQuery] bool getByPrimaryMarketer = false)
        {
            var companies = new[]
            {
                new { value = "1001", text = "ABC Trading Company" },
                new { value = "1002", text = "XYZ Logistics Inc" },
                new { value = "1003", text = "Global Petroleum Corp" },
                new { value = "1004", text = "Energy Solutions LLC" },
                new { value = "1005", text = "Alliance Energy Partners" }
            };
            return Ok(companies);
        }

        // GET: /api/fake/tradeentry/customfrequencyvalues
        [HttpGet("customfrequencyvalues")]
        public IActionResult GetCustomFrequencyValues()
        {
            var frequencies = new[]
            {
                new { value = "1", text = "Daily" },
                new { value = "2", text = "Weekly" },
                new { value = "3", text = "Monthly" },
                new { value = "4", text = "Quarterly" },
                new { value = "5", text = "Annually" }
            };
            return Ok(frequencies);
        }

        // GET: /api/fake/tradeentry/customoriginlocations
        [HttpGet("customoriginlocations")]
        public IActionResult GetCustomOriginLocations([FromQuery] bool showFiltered = false)
        {
            var locations = new[]
            {
                new { value = "100", text = "Houston Terminal" },
                new { value = "101", text = "Dallas Hub" },
                new { value = "102", text = "San Antonio Depot" },
                new { value = "103", text = "Austin Facility" },
                new { value = "104", text = "Fort Worth Station" }
            };
            return Ok(locations);
        }

        // GET: /api/fake/tradeentry/customdestinationlocations
        [HttpGet("customdestinationlocations")]
        public IActionResult GetCustomDestinationLocations([FromQuery] bool showFiltered = false)
        {
            var locations = new[]
            {
                new { value = "200", text = "Oklahoma City Terminal" },
                new { value = "201", text = "Tulsa Distribution Center" },
                new { value = "202", text = "Little Rock Hub" },
                new { value = "203", text = "Kansas City Depot" },
                new { value = "204", text = "New Orleans Port" }
            };
            return Ok(locations);
        }

        // GET: /api/fake/tradeentry/pricecomponents/{id}
        [HttpGet("pricecomponents/{id}")]
        public IActionResult GetPriceComponents(int id)
        {
            var components = new
            {
                basePrice = 2.85m,
                locationDifferential = 0.12m,
                transportCost = 0.08m,
                fuelSurcharge = 0.05m,
                totalPrice = 3.10m,
                currency = "USD",
                unit = "GAL",
                effectiveDate = DateTime.Now.ToString("yyyy-MM-dd")
            };
            return Ok(components);
        }

        // GET: /api/fake/tradeentry/pricepublishers
        [HttpGet("pricepublishers")]
        public IActionResult GetPricePublishers([FromQuery] int priceType = 1)
        {
            var publishers = new[]
            {
                new { value = "1", text = "OPIS" },
                new { value = "2", text = "Platts" },
                new { value = "3", text = "Argus" },
                new { value = "4", text = "NYMEX" },
                new { value = "5", text = "ICE" }
            };
            return Ok(publishers);
        }

        // GET: /api/fake/tradeentry/bookfromlocation/{locationId}
        [HttpGet("bookfromlocation/{locationId}")]
        public IActionResult GetBookFromLocation(int locationId)
        {
            var bookData = new
            {
                bookId = $"BOOK-{locationId}-2024",
                bookName = $"Location {locationId} Trading Book",
                locationId = locationId,
                isActive = true,
                tradingDesk = "Energy Trading",
                createdDate = DateTime.Now.AddMonths(-6).ToString("yyyy-MM-dd")
            };
            return Ok(bookData);
        }

        // GET: /api/fake/tradeentry/previousaverageopisprice
        [HttpGet("previousaverageopisprice")]
        public IActionResult GetPreviousAverageOpisPrice(
            [FromQuery] int locationId,
            [FromQuery] int productId,
            [FromQuery] string fromDateString)
        {
            var random = new Random();
            var opis = new
            {
                locationId = locationId,
                productId = productId,
                fromDate = fromDateString,
                averagePrice = Math.Round(2.50 + (random.NextDouble() * 0.50), 3),
                highPrice = Math.Round(2.80 + (random.NextDouble() * 0.30), 3),
                lowPrice = Math.Round(2.20 + (random.NextDouble() * 0.30), 3),
                volumeTraded = random.Next(10000, 100000),
                currency = "USD",
                unit = "GAL",
                lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            return Ok(opis);
        }

        // GET: /api/fake/tradeentry/customindexpricetypes
        [HttpGet("customindexpricetypes")]
        public IActionResult GetCustomIndexPriceTypes([FromQuery] int pricePublisherId = 1)
        {
            var priceTypes = new[]
            {
                new { value = "1", text = "Spot Price" },
                new { value = "2", text = "Contract Average" },
                new { value = "3", text = "Monthly Average" },
                new { value = "4", text = "Weekly High" },
                new { value = "5", text = "Weekly Low" },
                new { value = "6", text = "Daily Close" }
            };
            return Ok(priceTypes);
        }

        // POST: /api/fake/tradeentry/locationdiffpricedefault
        [HttpPost("locationdiffpricedefault")]
        public IActionResult CalculateLocationDiffPriceDefault([FromBody] LocationDiffPriceRequest request)
        {
            var random = new Random();
            var response = new
            {
                locationId = request?.LocationId ?? 100,
                productId = request?.ProductId ?? 1,
                basePrice = 2.85m,
                locationDifferential = Math.Round(0.05m + (decimal)(random.NextDouble() * 0.15), 3),
                calculatedPrice = Math.Round(2.85m + 0.05m + (decimal)(random.NextDouble() * 0.15), 3),
                effectiveDate = DateTime.Now.ToString("yyyy-MM-dd"),
                expiryDate = DateTime.Now.AddMonths(1).ToString("yyyy-MM-dd"),
                quantities = request?.Quantities ?? new[] { 1000, 2000, 3000 }
            };
            return Ok(response);
        }

        // POST: /api/fake/tradeentry/basepricedefault
        [HttpPost("basepricedefault")]
        public IActionResult CalculateBasePriceDefault([FromBody] BasePriceDefaultRequest request)
        {
            var response = new
            {
                frequencyType = request?.FrequencyType ?? "Monthly",
                calculatedBasePrice = 2.85m,
                weightedAveragePrice = 2.87m,
                priceComponents = new[]
                {
                    new { component = "Market Price", value = 2.75m },
                    new { component = "Premium", value = 0.10m },
                    new { component = "Fees", value = 0.02m }
                },
                totalVolume = request?.Quantities?.Sum() ?? 5000,
                calculationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            return Ok(response);
        }
    }

    // Request models
    public class LocationDiffPriceRequest
    {
        public int LocationId { get; set; }
        public int ProductId { get; set; }
        public int[]? Quantities { get; set; }
    }

    public class BasePriceDefaultRequest
    {
        public Dictionary<string, decimal>? PriceDictionary { get; set; }
        public string? FrequencyType { get; set; }
        public int[]? Quantities { get; set; }
    }
}