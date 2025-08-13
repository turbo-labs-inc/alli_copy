using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Custom.Gravitate.WebAPI.Core.Models;

namespace Custom.Gravitate.WebAPI.Core.Controllers
{
    /// <summary>
    /// FAKE/MOCK CONTROLLER - Deal management endpoints (retrieve, update, cancel)
    /// </summary>
    [ApiController]
    [Route("api/fake/deals")]
    [Tags("FAKE Deal Management - Mock Data")]
    public class FakeDealManagementApiController : ControllerBase
    {
        // Mock data storage (in production this would be database)
        private static readonly List<MockDeal> _mockDeals = new List<MockDeal>
        {
            new MockDeal
            {
                TradeEntryId = 12345,
                InternalContractNumber = "RA-12345",
                ExternalContractNumber = "EXT-2024-001",
                OrderStatus = "Active",
                TradeInstrumentId = 1,
                InternalCounterPartyId = 100,
                ExternalCounterPartyId = 200,
                FromDateTime = new DateTime(2024, 3, 1),
                ToDateTime = new DateTime(2024, 5, 31),
                TotalVolume = 275000,
                Products = new[] { "Propane: 185,000 gals", "Butane: 90,000 gals" },
                CreatedDate = DateTime.Now.AddDays(-5),
                ModifiedDate = DateTime.Now.AddDays(-1)
            },
            new MockDeal
            {
                TradeEntryId = 12346,
                InternalContractNumber = null,
                ExternalContractNumber = "EXT-2024-002",
                OrderStatus = "Draft",
                TradeInstrumentId = 2,
                InternalCounterPartyId = 100,
                ExternalCounterPartyId = 201,
                FromDateTime = new DateTime(2024, 4, 1),
                ToDateTime = new DateTime(2024, 6, 30),
                TotalVolume = 150000,
                Products = new[] { "Ethane: 150,000 gals" },
                CreatedDate = DateTime.Now.AddDays(-3),
                ModifiedDate = DateTime.Now.AddDays(-3)
            }
        };

        [HttpGet("{tradeEntryId}")]
        public IActionResult GetDeal(int tradeEntryId)
        {
            var deal = _mockDeals.FirstOrDefault(d => d.TradeEntryId == tradeEntryId);
            
            if (deal == null)
            {
                return NotFound(new { message = $"Deal with ID {tradeEntryId} not found" });
            }

            // Return full deal details
            var response = new
            {
                deal.TradeEntryId,
                deal.InternalContractNumber,
                deal.ExternalContractNumber,
                deal.OrderStatus,
                deal.TradeInstrumentId,
                TradeInstrumentName = "Physical Fixed Price",
                deal.InternalCounterPartyId,
                InternalCounterPartyName = "Internal Company ABC",
                deal.ExternalCounterPartyId,
                ExternalCounterPartyName = "External Customer XYZ",
                deal.FromDateTime,
                deal.ToDateTime,
                deal.TotalVolume,
                deal.Products,
                deal.CreatedDate,
                deal.ModifiedDate,
                TradeEntryDetails = GenerateMockDetails(deal)
            };

            return Ok(response);
        }

        [HttpGet("list")]
        public IActionResult GetDealsList(
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? counterPartyId = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = _mockDeals.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(d => d.OrderStatus.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(d => d.FromDateTime >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(d => d.ToDateTime <= toDate.Value);
            }

            if (counterPartyId.HasValue)
            {
                query = query.Where(d => d.ExternalCounterPartyId == counterPartyId.Value);
            }

            // Pagination
            var totalCount = query.Count();
            var deals = query
                .OrderByDescending(d => d.ModifiedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Deals = deals.Select(d => new
                {
                    d.TradeEntryId,
                    d.InternalContractNumber,
                    d.ExternalContractNumber,
                    d.OrderStatus,
                    d.FromDateTime,
                    d.ToDateTime,
                    d.TotalVolume,
                    ProductSummary = string.Join(", ", d.Products),
                    d.ModifiedDate
                })
            };

            return Ok(response);
        }

        [HttpPut("{tradeEntryId}/status")]
        public IActionResult UpdateDealStatus(int tradeEntryId, [FromBody] UpdateStatusRequest request)
        {
            var deal = _mockDeals.FirstOrDefault(d => d.TradeEntryId == tradeEntryId);
            
            if (deal == null)
            {
                return NotFound(new { message = $"Deal with ID {tradeEntryId} not found" });
            }

            // Validate status transition
            var validStatuses = new[] { "Draft", "Active", "Cancelled", "Completed" };
            if (!validStatuses.Contains(request.NewStatus))
            {
                return BadRequest(new { message = $"Invalid status: {request.NewStatus}" });
            }

            // Business rule: Can't activate without internal contract number
            if (request.NewStatus == "Active" && string.IsNullOrEmpty(deal.InternalContractNumber))
            {
                // Simulate RightAngle activation
                deal.InternalContractNumber = $"RA-{tradeEntryId}";
            }

            var oldStatus = deal.OrderStatus;
            deal.OrderStatus = request.NewStatus;
            deal.ModifiedDate = DateTime.Now;

            return Ok(new
            {
                Success = true,
                TradeEntryId = tradeEntryId,
                OldStatus = oldStatus,
                NewStatus = request.NewStatus,
                InternalContractNumber = deal.InternalContractNumber,
                Message = $"Deal status updated from {oldStatus} to {request.NewStatus}"
            });
        }

        [HttpDelete("{tradeEntryId}")]
        public IActionResult CancelDeal(int tradeEntryId, [FromBody] CancelDealRequest? request = null)
        {
            var deal = _mockDeals.FirstOrDefault(d => d.TradeEntryId == tradeEntryId);
            
            if (deal == null)
            {
                return NotFound(new { message = $"Deal with ID {tradeEntryId} not found" });
            }

            // Business rule: Can't cancel if already completed
            if (deal.OrderStatus == "Completed")
            {
                return BadRequest(new { message = "Cannot cancel a completed deal" });
            }

            deal.OrderStatus = "Cancelled";
            deal.ModifiedDate = DateTime.Now;

            return Ok(new
            {
                Success = true,
                TradeEntryId = tradeEntryId,
                Message = $"Deal {tradeEntryId} has been cancelled",
                CancellationReason = request?.Reason ?? "No reason provided",
                CancelledBy = request?.CancelledBy ?? "System",
                CancelledDate = DateTime.Now
            });
        }

        [HttpGet("{tradeEntryId}/history")]
        public IActionResult GetDealHistory(int tradeEntryId)
        {
            var deal = _mockDeals.FirstOrDefault(d => d.TradeEntryId == tradeEntryId);
            
            if (deal == null)
            {
                return NotFound(new { message = $"Deal with ID {tradeEntryId} not found" });
            }

            // Mock audit history
            var history = new[]
            {
                new
                {
                    Date = deal.CreatedDate,
                    Action = "Created",
                    User = "John Smith",
                    Details = "Deal created as Draft"
                },
                new
                {
                    Date = deal.CreatedDate.AddHours(2),
                    Action = "Updated",
                    User = "John Smith",
                    Details = "Added product details and pricing"
                },
                new
                {
                    Date = deal.ModifiedDate,
                    Action = "Status Changed",
                    User = "Jane Doe",
                    Details = $"Status changed to {deal.OrderStatus}"
                }
            };

            return Ok(new
            {
                TradeEntryId = tradeEntryId,
                History = history.OrderByDescending(h => h.Date)
            });
        }

        [HttpGet("{tradeEntryId}/validation")]
        public IActionResult ValidateDeal(int tradeEntryId)
        {
            var deal = _mockDeals.FirstOrDefault(d => d.TradeEntryId == tradeEntryId);
            
            if (deal == null)
            {
                return NotFound(new { message = $"Deal with ID {tradeEntryId} not found" });
            }

            // Mock validation results
            var validationResults = new List<object>();

            // Check for warnings/info
            if (deal.TotalVolume > 200000)
            {
                validationResults.Add(new
                {
                    Field = "TotalVolume",
                    Message = "Large volume deal - requires manager approval",
                    Severity = "Warning"
                });
            }

            if (deal.FromDateTime < DateTime.Now.AddDays(7))
            {
                validationResults.Add(new
                {
                    Field = "FromDateTime",
                    Message = "Deal starts within 7 days",
                    Severity = "Info"
                });
            }

            return Ok(new
            {
                TradeEntryId = tradeEntryId,
                IsValid = !validationResults.Any(v => ((dynamic)v).Severity == "Error"),
                ValidationResults = validationResults,
                CanActivate = deal.OrderStatus == "Draft" && !validationResults.Any(v => ((dynamic)v).Severity == "Error")
            });
        }

        // Helper method to generate mock deal details
        private object GenerateMockDetails(MockDeal deal)
        {
            var details = new List<object>();
            
            if (deal.Products.Contains("Propane"))
            {
                details.Add(new
                {
                    ProductId = 1,
                    ProductName = "Propane",
                    LocationId = 150,
                    LocationName = "Houston Terminal",
                    Quantities = new[]
                    {
                        new { DateTime = new DateTime(2024, 3, 1), Quantity = 50000, PeriodName = "March 2024" },
                        new { DateTime = new DateTime(2024, 4, 1), Quantity = 75000, PeriodName = "April 2024" },
                        new { DateTime = new DateTime(2024, 5, 1), Quantity = 60000, PeriodName = "May 2024" }
                    },
                    Prices = new[]
                    {
                        new { TradePriceTypeId = 1, PriceValue = 0.825, CurrencyId = 1 }
                    }
                });
            }

            if (deal.Products.Any(p => p.Contains("Butane")))
            {
                details.Add(new
                {
                    ProductId = 2,
                    ProductName = "Butane",
                    LocationId = 150,
                    LocationName = "Houston Terminal",
                    Quantities = new[]
                    {
                        new { DateTime = new DateTime(2024, 3, 1), Quantity = 25000, PeriodName = "March 2024" },
                        new { DateTime = new DateTime(2024, 4, 1), Quantity = 30000, PeriodName = "April 2024" },
                        new { DateTime = new DateTime(2024, 5, 1), Quantity = 35000, PeriodName = "May 2024" }
                    },
                    Prices = new[]
                    {
                        new { TradePriceTypeId = 2, IndexPricePublisherId = 1, CurrencyId = 1 }
                    }
                });
            }

            return details;
        }

        // Helper classes
        private class MockDeal
        {
            public int TradeEntryId { get; set; }
            public string? InternalContractNumber { get; set; }
            public string? ExternalContractNumber { get; set; }
            public string OrderStatus { get; set; } = "Draft";
            public int TradeInstrumentId { get; set; }
            public int InternalCounterPartyId { get; set; }
            public int ExternalCounterPartyId { get; set; }
            public DateTime FromDateTime { get; set; }
            public DateTime ToDateTime { get; set; }
            public decimal TotalVolume { get; set; }
            public string[] Products { get; set; } = Array.Empty<string>();
            public DateTime CreatedDate { get; set; }
            public DateTime ModifiedDate { get; set; }
        }

        public class UpdateStatusRequest
        {
            public string NewStatus { get; set; } = "";
            public string? Reason { get; set; }
        }

        public class CancelDealRequest
        {
            public string? Reason { get; set; }
            public string? CancelledBy { get; set; }
        }
    }
}