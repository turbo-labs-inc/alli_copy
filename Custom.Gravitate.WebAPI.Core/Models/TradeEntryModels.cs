namespace Custom.Gravitate.WebAPI.Core.Models
{
    // Shared request/response models for Trade Entry API
    public class LocationDiffPriceRequest
    {
        public int LocationId { get; set; }
        public int ProductId { get; set; }
        public List<TradeEntryQuantityModel>? Quantities { get; set; }
    }

    public class BasePriceDefaultRequest
    {
        public Dictionary<string, string>? PriceDictionary { get; set; }
        public int FrequencyType { get; set; }
        public List<TradeEntryQuantityModel>? Quantities { get; set; }
    }

    public class TradeEntryQuantityModel
    {
        public int? PeriodId { get; set; }  // Optional period ID for monthly allocations
        public DateTime DateTime { get; set; }  // Date for the quantity (typically 1st of month)
        public decimal Quantity { get; set; }  // Volume in specified UoM (gallons, barrels, etc.)
        public string? PeriodName { get; set; }  // Optional: "March 2024", "April 2024", etc.
    }

    // Create Deal Request/Response Models
    public class CreateDealRequest
    {
        public int? TradeEntryId { get; set; }
        public string? InternalContractNumber { get; set; }
        public string? ExternalContractNumber { get; set; }
        public int TradeInstrumentId { get; set; }
        public int InternalCounterPartyId { get; set; }
        public int InternalColleagueId { get; set; }
        public int ExternalCounterPartyId { get; set; }
        public int? ExternalColleagueId { get; set; }
        public int? ExternalCounterPartyProfileId { get; set; }
        public int MovementTypeCvId { get; set; }
        public int? NetOrGrossCvId { get; set; }
        public bool IsLegalContractOurs { get; set; } = true;
        public DateTime TradeEntryDateTime { get; set; } = DateTime.Now;
        public DateTime FromDateTime { get; set; }
        public DateTime ToDateTime { get; set; }
        public string? Comments { get; set; }
        public string? Description { get; set; }
        public int? TradeQuantityLimitFrequencyCvId { get; set; }
        public bool Activate { get; set; } = false;
        public bool SuppressEmail { get; set; } = false;
        public List<TradeEntryDetailRequest>? TradeEntryDetails { get; set; }
    }

    public class TradeEntryDetailRequest
    {
        public int LocationId { get; set; }
        public int? OriginLocationId { get; set; }
        public int ProductId { get; set; }
        public int PayOrReceiveCvId { get; set; }
        public int FrequencyCvId { get; set; }
        public int? NetOrGrossCvId { get; set; }
        public int UnitOfMeasureId { get; set; }
        public DateTime FromDateTime { get; set; }
        public DateTime ToDateTime { get; set; }
        public List<TradeEntryQuantityModel>? Quantities { get; set; }
        public List<TradeEntryPriceRequest>? Prices { get; set; }
    }

    public class TradeEntryPriceRequest
    {
        public int TradePriceTypeId { get; set; }
        public int CurrencyId { get; set; }
        public int UnitOfMeasureId { get; set; }
        public decimal? PriceValue { get; set; }
        public int? IndexPricePublisherId { get; set; }
        public int? IndexPriceInstrumentId { get; set; }
        public int? IndexPriceTypeCvId { get; set; }
        public List<PriceComponentRequest>? PriceComponents { get; set; }
    }

    public class PriceComponentRequest
    {
        public int ComponentTypeCvId { get; set; }
        public decimal Price { get; set; }
    }

    public class CreateDealResponse
    {
        public bool Success { get; set; }
        public int? TradeEntryId { get; set; }
        public string? InternalContractNumber { get; set; }
        public string? OrderStatus { get; set; }
        public List<ValidationError>? ValidationErrors { get; set; }
        public string? Message { get; set; }
    }

    public class ValidationError
    {
        public string? Field { get; set; }
        public string? Message { get; set; }
        public string? Severity { get; set; }
    }
}