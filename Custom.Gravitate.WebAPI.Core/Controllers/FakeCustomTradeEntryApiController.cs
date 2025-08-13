using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Custom.Gravitate.WebAPI.Core.Models;

namespace Custom.Gravitate.WebAPI.Core.Controllers
{
    /// <summary>
    /// FAKE/MOCK CONTROLLER - DELETE THIS FILE WHEN REAL ENDPOINTS ARE WORKING
    /// This controller provides mock data for testing without database dependencies
    /// </summary>
    [ApiController]
    [Route("api/fake/tradeentry")]
    [Tags("FAKE Trade Entry - Mock Data")]
    public class FakeCustomTradeEntryApiController : ControllerBase
    {
        [HttpGet("externalcompanies")]
        public IActionResult GetExternalCompanies(bool getByPrimaryMarketer)
        {
            var mockData = new[]
            {
                new { Value = "1001", Text = "ABC Trading Company" },
                new { Value = "1002", Text = "XYZ Logistics Inc" },
                new { Value = "1003", Text = "Global Petroleum Corp" }
            };
            return Ok(mockData);
        }

        [HttpGet("customfrequencyvalues")]
        public IActionResult GetCustomFrequencyCodeValueList()
        {
            var mockData = new[]
            {
                new { Value = "1", Text = "Daily" },
                new { Value = "2", Text = "Weekly" },
                new { Value = "3", Text = "Monthly" }
            };
            return Ok(mockData);
        }

        [HttpGet("customoriginlocations")]
        public IActionResult GetCustomOriginLocationList(bool showFiltered, int? externalCounterParty)
        {
            var mockData = new[]
            {
                new { Value = "100", Text = "Conway Terminal" },
                new { Value = "101", Text = "Mont Belvieu Hub" },
                new { Value = "102", Text = "Houston Storage" }
            };
            return Ok(mockData);
        }

        [HttpGet("customdestinationlocations")]
        public IActionResult GetCustomDestinationLocationList(bool showFiltered, int? externalCounterParty)
        {
            var mockData = new[]
            {
                new { Value = "200", Text = "Houston Terminal" },
                new { Value = "201", Text = "Dallas Distribution Center" },
                new { Value = "202", Text = "San Antonio Hub" }
            };
            return Ok(mockData);
        }

        [HttpGet("pricecomponents/{priceId}")]
        public IActionResult GetComponentsFromPriceId(int priceId)
        {
            var mockData = new Dictionary<string, string>
            {
                { "Base Price", "2.450" },
                { "Transport Fee", "0.125" },
                { "Fuel Surcharge", "0.055" },
                { "Terminal Fee", "0.030" }
            };
            return Ok(mockData);
        }

        [HttpGet("pricepublishers")]
        public IActionResult GetPricePublisherList(int priceType)
        {
            var mockData = new[]
            {
                new { Value = "1", Text = "OPIS" },
                new { Value = "2", Text = "PLATTS" },
                new { Value = "3", Text = "ARGUS" }
            };
            return Ok(mockData);
        }

        [HttpGet("bookfromlocation/{locationId}")]
        public IActionResult GetBookFromLocationId(int locationId)
        {
            return Ok(new { bookId = 25 });
        }

        [HttpGet("previousaverageopisprice")]
        public IActionResult GetPreviousAverageOpisPrice(int locationId, int productId, string fromDateString)
        {
            return Ok(new { price = 2.3456 });
        }

        [HttpGet("customindexpricetypes")]
        public IActionResult GetCustomIndexPriceTypeList(int? pricePublisherId, string? filterText = null)
        {
            var mockData = new[]
            {
                new { Value = "101", Text = "Average Prompt" },
                new { Value = "102", Text = "Monthly Average" },
                new { Value = "103", Text = "Daily Index" }
            };
            return Ok(mockData);
        }

        [HttpPost("locationdiffpricedefault")]
        public IActionResult GetLocationDiffPriceDefault([FromBody] LocationDiffPriceRequest request)
        {
            return Ok(new { price = 0.12575 });
        }

        [HttpPost("basepricedefault")]
        public IActionResult GetBasePriceDefault([FromBody] BasePriceDefaultRequest request)
        {
            return Ok(new { price = 2.48333 });
        }

        [HttpPost("createdeal")]
        public IActionResult CreateDeal([FromBody] CreateDealRequest request)
        {
            // Mock validation
            var validationErrors = new List<ValidationError>();
            
            if (request.TradeInstrumentId <= 0)
            {
                validationErrors.Add(new ValidationError 
                { 
                    Field = "TradeInstrumentId", 
                    Message = "Trade Instrument is required", 
                    Severity = "Error" 
                });
            }

            if (request.TradeEntryDetails == null || !request.TradeEntryDetails.Any())
            {
                validationErrors.Add(new ValidationError 
                { 
                    Field = "TradeEntryDetails", 
                    Message = "At least one product detail is required", 
                    Severity = "Error" 
                });
            }
            else
            {
                // Validate each detail has product and volumes
                for (int i = 0; i < request.TradeEntryDetails.Count; i++)
                {
                    var detail = request.TradeEntryDetails[i];
                    
                    if (detail.ProductId <= 0)
                    {
                        validationErrors.Add(new ValidationError 
                        { 
                            Field = $"TradeEntryDetails[{i}].ProductId", 
                            Message = "Product is required for each detail line", 
                            Severity = "Error" 
                        });
                    }
                    
                    if (detail.Quantities == null || !detail.Quantities.Any())
                    {
                        validationErrors.Add(new ValidationError 
                        { 
                            Field = $"TradeEntryDetails[{i}].Quantities", 
                            Message = "Volume allocations are required for each product", 
                            Severity = "Error" 
                        });
                    }
                    else
                    {
                        // Check that total volume is reasonable
                        var totalVolume = detail.Quantities.Sum(q => q.Quantity);
                        if (totalVolume <= 0)
                        {
                            validationErrors.Add(new ValidationError 
                            { 
                                Field = $"TradeEntryDetails[{i}].Quantities", 
                                Message = "Total volume must be greater than zero", 
                                Severity = "Error" 
                            });
                        }
                    }
                }
            }

            if (validationErrors.Any())
            {
                return Ok(new CreateDealResponse
                {
                    Success = false,
                    ValidationErrors = validationErrors,
                    Message = "Validation failed"
                });
            }

            // Mock successful creation with more realistic response
            var mockTradeEntryId = request.TradeEntryId ?? new Random().Next(10000, 99999);
            var isUpdate = request.TradeEntryId.HasValue;
            
            // Calculate total volumes for summary
            decimal totalVolume = 0;
            var productSummary = new List<string>();
            
            if (request.TradeEntryDetails != null)
            {
                foreach (var detail in request.TradeEntryDetails)
                {
                    if (detail.Quantities != null)
                    {
                        var detailVolume = detail.Quantities.Sum(q => q.Quantity);
                        totalVolume += detailVolume;
                        
                        // Mock product names
                        var productName = detail.ProductId switch
                        {
                            1 => "Propane",
                            2 => "Butane",
                            3 => "Isobutane",
                            4 => "Natural Gasoline",
                            5 => "Ethane",
                            _ => $"Product {detail.ProductId}"
                        };
                        
                        productSummary.Add($"{productName}: {detailVolume:N0} gals");
                    }
                }
            }
            
            var summaryMessage = isUpdate 
                ? $"Deal updated successfully with ID: {mockTradeEntryId}" 
                : $"Deal created successfully with ID: {mockTradeEntryId}";
                
            if (productSummary.Any())
            {
                summaryMessage += $" | Products: {string.Join(", ", productSummary)} | Total Volume: {totalVolume:N0} gallons";
            }
            
            return Ok(new CreateDealResponse
            {
                Success = true,
                TradeEntryId = mockTradeEntryId,
                InternalContractNumber = request.Activate ? $"RA-{mockTradeEntryId}" : null,
                OrderStatus = request.Activate ? "Active" : "Draft",
                ValidationErrors = new List<ValidationError>(),
                Message = summaryMessage
            });
        }
        
        [HttpGet("sampledealrequest")]
        public IActionResult GetSampleDealRequest()
        {
            // Provide a sample request for testing
            var startDate = new DateTime(2024, 3, 1);
            var endDate = new DateTime(2024, 5, 31);
            
            var sample = new CreateDealRequest
            {
                TradeInstrumentId = 1,
                InternalCounterPartyId = 100,
                InternalColleagueId = 50,
                ExternalCounterPartyId = 200,
                ExternalColleagueId = 75,
                MovementTypeCvId = 1,
                IsLegalContractOurs = true,
                TradeEntryDateTime = DateTime.Now,
                FromDateTime = startDate,
                ToDateTime = endDate,
                Comments = "Q2 2024 NGL supply deal",
                Description = "Monthly propane and butane deliveries",
                Activate = false,
                SuppressEmail = false,
                TradeEntryDetails = new List<TradeEntryDetailRequest>
                {
                    new TradeEntryDetailRequest
                    {
                        LocationId = 150,  // Houston Terminal
                        ProductId = 1,      // Propane
                        PayOrReceiveCvId = 1,  // Pay
                        FrequencyCvId = 3,     // Monthly
                        UnitOfMeasureId = 10,  // Gallons
                        FromDateTime = startDate,
                        ToDateTime = endDate,
                        Quantities = new List<TradeEntryQuantityModel>
                        {
                            new TradeEntryQuantityModel { DateTime = new DateTime(2024, 3, 1), Quantity = 50000, PeriodName = "March 2024" },
                            new TradeEntryQuantityModel { DateTime = new DateTime(2024, 4, 1), Quantity = 75000, PeriodName = "April 2024" },
                            new TradeEntryQuantityModel { DateTime = new DateTime(2024, 5, 1), Quantity = 60000, PeriodName = "May 2024" }
                        },
                        Prices = new List<TradeEntryPriceRequest>
                        {
                            new TradeEntryPriceRequest
                            {
                                TradePriceTypeId = 1,
                                CurrencyId = 1,  // USD
                                UnitOfMeasureId = 10,  // $/Gallon
                                PriceValue = 0.825m
                            }
                        }
                    },
                    new TradeEntryDetailRequest
                    {
                        LocationId = 150,  // Houston Terminal
                        ProductId = 2,      // Butane
                        PayOrReceiveCvId = 1,  // Pay
                        FrequencyCvId = 3,     // Monthly
                        UnitOfMeasureId = 10,  // Gallons
                        FromDateTime = startDate,
                        ToDateTime = endDate,
                        Quantities = new List<TradeEntryQuantityModel>
                        {
                            new TradeEntryQuantityModel { DateTime = new DateTime(2024, 3, 1), Quantity = 25000, PeriodName = "March 2024" },
                            new TradeEntryQuantityModel { DateTime = new DateTime(2024, 4, 1), Quantity = 30000, PeriodName = "April 2024" },
                            new TradeEntryQuantityModel { DateTime = new DateTime(2024, 5, 1), Quantity = 35000, PeriodName = "May 2024" }
                        },
                        Prices = new List<TradeEntryPriceRequest>
                        {
                            new TradeEntryPriceRequest
                            {
                                TradePriceTypeId = 2,  // Index price
                                CurrencyId = 1,  // USD
                                UnitOfMeasureId = 10,  // $/Gallon
                                IndexPricePublisherId = 1,  // OPIS
                                IndexPriceInstrumentId = 100,
                                IndexPriceTypeCvId = 101  // Monthly Average
                            }
                        }
                    }
                }
            };
            
            return Ok(sample);
        }
    }
}