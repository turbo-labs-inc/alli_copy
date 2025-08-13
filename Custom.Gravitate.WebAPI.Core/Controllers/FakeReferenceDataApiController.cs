using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Custom.Gravitate.WebAPI.Core.Models;

namespace Custom.Gravitate.WebAPI.Core.Controllers
{
    /// <summary>
    /// FAKE/MOCK CONTROLLER - Reference data endpoints for deal creation
    /// </summary>
    [ApiController]
    [Route("api/fake/referencedata")]
    [Tags("FAKE Reference Data - Mock Data")]
    public class FakeReferenceDataApiController : ControllerBase
    {
        [HttpGet("products")]
        public IActionResult GetProducts()
        {
            var products = new[]
            {
                new { Value = "1", Text = "Propane", Code = "C3" },
                new { Value = "2", Text = "Butane", Code = "NC4" },
                new { Value = "3", Text = "Isobutane", Code = "IC4" },
                new { Value = "4", Text = "Natural Gasoline", Code = "C5+" },
                new { Value = "5", Text = "Ethane", Code = "C2" },
                new { Value = "6", Text = "Crude Oil", Code = "CRD" },
                new { Value = "7", Text = "Diesel", Code = "DSL" },
                new { Value = "8", Text = "Gasoline", Code = "GAS" }
            };
            return Ok(products);
        }

        [HttpGet("tradeinstruments")]
        public IActionResult GetTradeInstruments()
        {
            var instruments = new[]
            {
                new { Value = "1", Text = "Physical Fixed Price", Category = "Physical" },
                new { Value = "2", Text = "Physical Index Price", Category = "Physical" },
                new { Value = "3", Text = "Physical Formula Price", Category = "Physical" },
                new { Value = "4", Text = "Financial Swap", Category = "Financial" },
                new { Value = "5", Text = "Financial Option", Category = "Financial" }
            };
            return Ok(instruments);
        }

        [HttpGet("unitsofmeasure")]
        public IActionResult GetUnitsOfMeasure()
        {
            var uoms = new[]
            {
                new { Value = "10", Text = "Gallons", Abbreviation = "GAL" },
                new { Value = "11", Text = "Barrels", Abbreviation = "BBL" },
                new { Value = "12", Text = "Metric Tons", Abbreviation = "MT" },
                new { Value = "13", Text = "Thousand Gallons", Abbreviation = "KGAL" },
                new { Value = "14", Text = "Million BTU", Abbreviation = "MMBTU" }
            };
            return Ok(uoms);
        }

        [HttpGet("currencies")]
        public IActionResult GetCurrencies()
        {
            var currencies = new[]
            {
                new { Value = "1", Text = "US Dollar", Code = "USD", Symbol = "$" },
                new { Value = "2", Text = "Canadian Dollar", Code = "CAD", Symbol = "C$" },
                new { Value = "3", Text = "Euro", Code = "EUR", Symbol = "€" },
                new { Value = "4", Text = "British Pound", Code = "GBP", Symbol = "£" }
            };
            return Ok(currencies);
        }

        [HttpGet("frequencytypes")]
        public IActionResult GetFrequencyTypes()
        {
            var frequencies = new[]
            {
                new { Value = "1", Text = "Daily" },
                new { Value = "2", Text = "Weekly" },
                new { Value = "3", Text = "Monthly" },
                new { Value = "4", Text = "Quarterly" },
                new { Value = "5", Text = "Per Trade" }
            };
            return Ok(frequencies);
        }

        [HttpGet("movementtypes")]
        public IActionResult GetMovementTypes()
        {
            var movementTypes = new[]
            {
                new { Value = "1", Text = "Truck" },
                new { Value = "2", Text = "Rail" },
                new { Value = "3", Text = "Pipeline" },
                new { Value = "4", Text = "Marine" },
                new { Value = "5", Text = "In-Tank Transfer" }
            };
            return Ok(movementTypes);
        }

        [HttpGet("payorreceive")]
        public IActionResult GetPayOrReceiveTypes()
        {
            var types = new[]
            {
                new { Value = "1", Text = "Pay", Code = "P" },
                new { Value = "2", Text = "Receive", Code = "R" }
            };
            return Ok(types);
        }

        [HttpGet("tradepricetypes")]
        public IActionResult GetTradePriceTypes()
        {
            var priceTypes = new[]
            {
                new { Value = "1", Text = "Fixed Price", Category = "Fixed" },
                new { Value = "2", Text = "Index Price", Category = "Index" },
                new { Value = "3", Text = "Formula Price", Category = "Formula" },
                new { Value = "4", Text = "Differential Price", Category = "Differential" }
            };
            return Ok(priceTypes);
        }

        [HttpGet("paymentterms")]
        public IActionResult GetPaymentTerms()
        {
            var terms = new[]
            {
                new { Value = "1", Text = "Net 10", Days = 10 },
                new { Value = "2", Text = "Net 15", Days = 15 },
                new { Value = "3", Text = "Net 30", Days = 30 },
                new { Value = "4", Text = "Prepay", Days = 0 },
                new { Value = "5", Text = "Due on Receipt", Days = 0 }
            };
            return Ok(terms);
        }

        [HttpGet("incoterms")]
        public IActionResult GetIncoTerms()
        {
            var incoTerms = new[]
            {
                new { Value = "1", Text = "FOB - Free On Board" },
                new { Value = "2", Text = "CIF - Cost Insurance Freight" },
                new { Value = "3", Text = "DDP - Delivered Duty Paid" },
                new { Value = "4", Text = "EXW - Ex Works" },
                new { Value = "5", Text = "FCA - Free Carrier" }
            };
            return Ok(incoTerms);
        }

        [HttpGet("books")]
        public IActionResult GetBooks()
        {
            var books = new[]
            {
                new { Value = "1", Text = "Conway", Code = "CNW" },
                new { Value = "2", Text = "Mont Belvieu TET", Code = "MB-TET" },
                new { Value = "3", Text = "Mont Belvieu NTET", Code = "MB-NTET" },
                new { Value = "4", Text = "Houston Ship Channel", Code = "HSC" }
            };
            return Ok(books);
        }

        [HttpGet("colleagues")]
        public IActionResult GetColleagues(int? counterPartyId = null)
        {
            var colleagues = new[]
            {
                new { Value = "50", Text = "John Smith", Email = "john.smith@company.com" },
                new { Value = "51", Text = "Jane Doe", Email = "jane.doe@company.com" },
                new { Value = "75", Text = "Bob Johnson", Email = "bob.johnson@external.com" },
                new { Value = "76", Text = "Alice Williams", Email = "alice.williams@external.com" }
            };
            return Ok(colleagues);
        }

        [HttpGet("counterpartyprofiles/{counterPartyId}")]
        public IActionResult GetCounterPartyProfiles(int counterPartyId)
        {
            var profiles = new[]
            {
                new { Value = "1", Text = "Standard Terms", IsDefault = true },
                new { Value = "2", Text = "Preferred Customer", IsDefault = false },
                new { Value = "3", Text = "Spot Trading", IsDefault = false }
            };
            return Ok(profiles);
        }

        [HttpGet("allreferencedata")]
        public IActionResult GetAllReferenceData()
        {
            var referenceData = new
            {
                Products = GetProducts(),
                TradeInstruments = GetTradeInstruments(),
                UnitsOfMeasure = GetUnitsOfMeasure(),
                Currencies = GetCurrencies(),
                FrequencyTypes = GetFrequencyTypes(),
                MovementTypes = GetMovementTypes(),
                PayOrReceiveTypes = GetPayOrReceiveTypes(),
                TradePriceTypes = GetTradePriceTypes(),
                PaymentTerms = GetPaymentTerms(),
                IncoTerms = GetIncoTerms(),
                Books = GetBooks()
            };
            
            return Ok(referenceData);
        }
    }
}