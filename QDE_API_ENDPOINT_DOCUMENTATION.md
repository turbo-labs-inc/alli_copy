# QDE (Quick Data Entry) API Endpoint Documentation

## Base URL
```
http://localhost:5000/api/custom/tradeentry
```

## Authentication
All endpoints require JWT Bearer token authentication.
```
Authorization: Bearer <JWT_TOKEN>
```

---

## GET Endpoints

### 1. Get External Companies
**Endpoint:** `GET /api/custom/tradeentry/externalcompanies`

**Purpose:** Retrieves a list of external companies/counterparties for trade entry selection.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| getByPrimaryMarketer | boolean | Yes | If true, returns companies filtered by user's colleague association. If false, returns all active carriers and customers. |

**Response Schema:**
```json
[
  {
    "Value": "string",  // Company ID
    "Text": "string"    // Company Display Name
  }
]
```

**Example Request:**
```bash
GET /api/custom/tradeentry/externalcompanies?getByPrimaryMarketer=true
```

**Example Response:**
```json
[
  {
    "Value": "1234",
    "Text": "ABC Trading Company"
  },
  {
    "Value": "5678",
    "Text": "XYZ Logistics Inc"
  }
]
```

---

### 2. Get Custom Frequency Values
**Endpoint:** `GET /api/custom/tradeentry/customfrequencyvalues`

**Purpose:** Returns configured trade quantity frequency types from application settings.

**Query Parameters:** None

**Response Schema:**
```json
[
  {
    "Value": "string",  // Frequency Type ID
    "Text": "string"    // Frequency Display Name
  }
]
```

**Example Request:**
```bash
GET /api/custom/tradeentry/customfrequencyvalues
```

**Example Response:**
```json
[
  {
    "Value": "1",
    "Text": "Daily"
  },
  {
    "Value": "2",
    "Text": "Weekly"
  },
  {
    "Value": "3",
    "Text": "Monthly"
  }
]
```

---

### 3. Get Custom Origin Locations
**Endpoint:** `GET /api/custom/tradeentry/customoriginlocations`

**Purpose:** Retrieves available origin/loading locations for trade entry.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| showFiltered | boolean | Yes | Whether to filter locations based on counterparty |
| externalCounterParty | integer | No | Counterparty ID to filter locations (required if showFiltered=true) |

**Response Schema:**
```json
[
  {
    "Value": "string",  // Location ID
    "Text": "string"    // Location Display Name
  }
]
```

**Example Request:**
```bash
GET /api/custom/tradeentry/customoriginlocations?showFiltered=true&externalCounterParty=1234
```

**Example Response:**
```json
[
  {
    "Value": "100",
    "Text": "Conway Terminal"
  },
  {
    "Value": "101",
    "Text": "Mont Belvieu Hub"
  }
]
```

---

### 4. Get Custom Destination Locations
**Endpoint:** `GET /api/custom/tradeentry/customdestinationlocations`

**Purpose:** Retrieves available destination/delivery locations.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| showFiltered | boolean | Yes | Whether to filter locations based on counterparty |
| externalCounterParty | integer | No | Counterparty ID to filter locations (required if showFiltered=true) |

**Response Schema:**
```json
[
  {
    "Value": "string",  // Location ID
    "Text": "string"    // Location Display Name
  }
]
```

**Example Request:**
```bash
GET /api/custom/tradeentry/customdestinationlocations?showFiltered=false
```

**Example Response:**
```json
[
  {
    "Value": "200",
    "Text": "Houston Terminal"
  },
  {
    "Value": "201",
    "Text": "Dallas Distribution Center"
  }
]
```

---

### 5. Get Price Components
**Endpoint:** `GET /api/custom/tradeentry/pricecomponents/{priceId}`

**Purpose:** Returns price breakdown components for a specific price ID.

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| priceId | integer | Yes | The price ID to get components for |

**Response Schema:**
```json
{
  "ComponentName1": "string",  // Price value as string
  "ComponentName2": "string"
}
```

**Example Request:**
```bash
GET /api/custom/tradeentry/pricecomponents/5000
```

**Example Response:**
```json
{
  "Base Price": "2.450",
  "Transport Fee": "0.125",
  "Fuel Surcharge": "0.055",
  "Terminal Fee": "0.030"
}
```

---

### 6. Get Price Publishers
**Endpoint:** `GET /api/custom/tradeentry/pricepublishers`

**Purpose:** Returns relevant price publishers filtered by price type.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| priceType | integer | Yes | Price type ID to filter publishers |

**Response Schema:**
```json
[
  {
    "Value": "string",  // Publisher ID
    "Text": "string"    // Publisher Abbreviation
  }
]
```

**Example Request:**
```bash
GET /api/custom/tradeentry/pricepublishers?priceType=10
```

**Example Response:**
```json
[
  {
    "Value": "1",
    "Text": "OPIS"
  },
  {
    "Value": "2",
    "Text": "PLATTS"
  }
]
```

---

### 7. Get Book From Location
**Endpoint:** `GET /api/custom/tradeentry/bookfromlocation/{locationId}`

**Purpose:** Returns the book ID associated with a specific location for accounting/settlement.

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| locationId | integer | Yes | The location ID to get book for |

**Response Schema:**
```json
{
  "bookId": "integer"
}
```

**Example Request:**
```bash
GET /api/custom/tradeentry/bookfromlocation/100
```

**Example Response:**
```json
{
  "bookId": 25
}
```

---

### 8. Get Previous Average OPIS Price
**Endpoint:** `GET /api/custom/tradeentry/previousaverageopisprice`

**Purpose:** Retrieves historical OPIS average price for a product at a location.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| locationId | integer | Yes | Location ID for price lookup |
| productId | integer | Yes | Product ID for price lookup |
| fromDateString | string | Yes | Date string (ISO format) for price date |

**Response Schema:**
```json
{
  "price": "number"
}
```

**Example Request:**
```bash
GET /api/custom/tradeentry/previousaverageopisprice?locationId=100&productId=5&fromDateString=2024-01-15
```

**Example Response:**
```json
{
  "price": 2.3456
}
```

---

### 9. Get Custom Index Price Types
**Endpoint:** `GET /api/custom/tradeentry/customindexpricetypes`

**Purpose:** Returns index price types for a specific price publisher with optional text filtering.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| pricePublisherId | integer | No | Publisher ID to get price types for |
| filterText | string | No | Text to filter price types by (partial match) |

**Response Schema:**
```json
[
  {
    "Value": "string",  // Price Type ID
    "Text": "string"    // Price Type Display Name
  }
]
```

**Example Request:**
```bash
GET /api/custom/tradeentry/customindexpricetypes?pricePublisherId=1&filterText=Average
```

**Example Response:**
```json
[
  {
    "Value": "101",
    "Text": "Average Prompt"
  },
  {
    "Value": "102",
    "Text": "Monthly Average"
  }
]
```

---

## POST Endpoints

### 10. Get Location Differential Price Default
**Endpoint:** `POST /api/custom/tradeentry/locationdiffpricedefault`

**Purpose:** Calculates weighted location differential price based on quantity schedule.

**Request Body Schema:**
```json
{
  "LocationId": "integer",
  "ProductId": "integer",
  "Quantities": [
    {
      "DateTime": "string (ISO 8601)",
      "Quantity": "number"
    }
  ]
}
```

**Response Schema:**
```json
{
  "price": "number"  // Rounded to 5 decimal places
}
```

**Example Request:**
```bash
POST /api/custom/tradeentry/locationdiffpricedefault
Content-Type: application/json

{
  "LocationId": 100,
  "ProductId": 5,
  "Quantities": [
    {
      "DateTime": "2024-02-01T00:00:00",
      "Quantity": 10000
    },
    {
      "DateTime": "2024-02-15T00:00:00",
      "Quantity": 15000
    },
    {
      "DateTime": "2024-03-01T00:00:00",
      "Quantity": 20000
    }
  ]
}
```

**Example Response:**
```json
{
  "price": 0.12575
}
```

**Business Logic:**
- Adds 14 days to each quantity date for price lookup
- Calculates weighted average based on quantity proportions
- Returns 0 if no matching prices found

---

### 11. Get Base Price Default
**Endpoint:** `POST /api/custom/tradeentry/basepricedefault`

**Purpose:** Calculates weighted or maximum base price based on frequency type and quantity schedule.

**Request Body Schema:**
```json
{
  "PriceDictionary": {
    "month0": "string",  // Current month price
    "month1": "string",  // Next month price
    "month2": "string"   // Two months out price
  },
  "FrequencyType": "integer",
  "Quantities": [
    {
      "DateTime": "string (ISO 8601)",
      "Quantity": "number"
    }
  ]
}
```

**Response Schema:**
```json
{
  "price": "number"  // Rounded to 5 decimal places for weighted, or max for per-trade
}
```

**Example Request:**
```bash
POST /api/custom/tradeentry/basepricedefault
Content-Type: application/json

{
  "PriceDictionary": {
    "month0": "2.450",
    "month1": "2.475",
    "month2": "2.500"
  },
  "FrequencyType": 1,
  "Quantities": [
    {
      "DateTime": "2024-02-01T00:00:00",
      "Quantity": 10000
    },
    {
      "DateTime": "2024-03-01T00:00:00",
      "Quantity": 15000
    },
    {
      "DateTime": "2024-04-01T00:00:00",
      "Quantity": 20000
    }
  ]
}
```

**Example Response:**
```json
{
  "price": 2.48333
}
```

**Business Logic:**
- Calculates month difference from current Central Time
- Maps quantities to price dictionary entries by month offset
- For "PerTrade" frequency type: returns maximum month price
- For other frequency types: returns weighted average
- Returns 0 if month key not found in dictionary

---

### 12. Create Deal (Save Trade Entry)
**Endpoint:** `POST /api/custom/tradeentry/createdeal`

**Purpose:** Creates a new trade entry deal or updates an existing one.

**Request Body Schema:**
```json
{
  "TradeEntryId": "integer (optional)",           // ID for existing deal update
  "InternalContractNumber": "string (optional)",   // Internal deal number
  "ExternalContractNumber": "string (optional)",   // External reference number
  "TradeInstrumentId": "integer",                  // Trade instrument type
  "InternalCounterPartyId": "integer",            // Internal company ID
  "InternalColleagueId": "integer",               // Internal user/colleague ID
  "ExternalCounterPartyId": "integer",            // External company ID
  "ExternalColleagueId": "integer (optional)",    // External contact ID
  "ExternalCounterPartyProfileId": "integer (optional)", // Profile ID
  "MovementTypeCvId": "integer",                  // Movement type code value
  "NetOrGrossCvId": "integer (optional)",         // Net or gross pricing
  "IsLegalContractOurs": "boolean",               // Legal contract ownership
  "TradeEntryDateTime": "string (ISO 8601)",      // Trade entry date
  "FromDateTime": "string (ISO 8601)",            // Deal start date
  "ToDateTime": "string (ISO 8601)",              // Deal end date
  "Comments": "string (optional)",                // Additional comments
  "Description": "string (optional)",             // Deal description
  "TradeQuantityLimitFrequencyCvId": "integer (optional)", // Quantity limit frequency
  "Activate": "boolean",                          // Whether to activate the deal immediately
  "SuppressEmail": "boolean",                     // Whether to suppress confirmation emails
  "TradeEntryDetails": [
    {
      "LocationId": "integer",                    // Delivery location
      "OriginLocationId": "integer (optional)",   // Origin location
      "ProductId": "integer",                     // Product ID
      "PayOrReceiveCvId": "integer",             // Pay or receive indicator
      "FrequencyCvId": "integer",                // Delivery frequency
      "NetOrGrossCvId": "integer (optional)",    // Net or gross for detail
      "UnitOfMeasureId": "integer",              // Unit of measure
      "FromDateTime": "string (ISO 8601)",       // Detail start date
      "ToDateTime": "string (ISO 8601)",         // Detail end date
      "Quantities": [
        {
          "DateTime": "string (ISO 8601)",
          "Quantity": "number"
        }
      ],
      "Prices": [
        {
          "TradePriceTypeId": "integer",         // Price type ID
          "CurrencyId": "integer",               // Currency ID
          "UnitOfMeasureId": "integer",          // Price UOM
          "PriceValue": "number (optional)",     // Fixed price value
          "IndexPricePublisherId": "integer (optional)", // Index publisher
          "IndexPriceInstrumentId": "integer (optional)", // Index instrument
          "IndexPriceTypeCvId": "integer (optional)",     // Index price type
          "PriceComponents": [
            {
              "ComponentTypeCvId": "integer",
              "Price": "number"
            }
          ]
        }
      ]
    }
  ]
}
```

**Response Schema:**
```json
{
  "success": "boolean",
  "tradeEntryId": "integer",                     // ID of created/updated deal
  "internalContractNumber": "string (optional)",  // RA deal number if activated
  "orderStatus": "string",                        // Current status of the deal
  "validationErrors": [                           // Array of validation errors if any
    {
      "field": "string",
      "message": "string",
      "severity": "string"  // "Error", "Warning", "Info"
    }
  ],
  "message": "string"                             // Success or error message
}
```

**Example Request (Multi-Product, Multi-Month Deal):**
```bash
POST /api/custom/tradeentry/createdeal
Content-Type: application/json

{
  "TradeInstrumentId": 1,
  "InternalCounterPartyId": 100,
  "InternalColleagueId": 50,
  "ExternalCounterPartyId": 200,
  "ExternalColleagueId": 75,
  "MovementTypeCvId": 1,
  "IsLegalContractOurs": true,
  "TradeEntryDateTime": "2024-02-01T09:00:00",
  "FromDateTime": "2024-03-01T00:00:00",
  "ToDateTime": "2024-05-31T23:59:59",
  "Comments": "Q2 2024 NGL supply deal",
  "Description": "Monthly propane and butane deliveries",
  "Activate": false,
  "SuppressEmail": false,
  "TradeEntryDetails": [
    {
      "LocationId": 150,
      "ProductId": 1,  // Propane
      "PayOrReceiveCvId": 1,
      "FrequencyCvId": 3,  // Monthly
      "UnitOfMeasureId": 10,  // Gallons
      "FromDateTime": "2024-03-01T00:00:00",
      "ToDateTime": "2024-05-31T23:59:59",
      "Quantities": [
        {
          "DateTime": "2024-03-01T00:00:00",
          "Quantity": 50000,
          "PeriodName": "March 2024"
        },
        {
          "DateTime": "2024-04-01T00:00:00",
          "Quantity": 75000,
          "PeriodName": "April 2024"
        },
        {
          "DateTime": "2024-05-01T00:00:00",
          "Quantity": 60000,
          "PeriodName": "May 2024"
        }
      ],
      "Prices": [
        {
          "TradePriceTypeId": 1,  // Fixed price
          "CurrencyId": 1,
          "UnitOfMeasureId": 10,
          "PriceValue": 0.825  // $/gallon
        }
      ]
    },
    {
      "LocationId": 150,
      "ProductId": 2,  // Butane
      "PayOrReceiveCvId": 1,
      "FrequencyCvId": 3,  // Monthly
      "UnitOfMeasureId": 10,  // Gallons
      "FromDateTime": "2024-03-01T00:00:00",
      "ToDateTime": "2024-05-31T23:59:59",
      "Quantities": [
        {
          "DateTime": "2024-03-01T00:00:00",
          "Quantity": 25000,
          "PeriodName": "March 2024"
        },
        {
          "DateTime": "2024-04-01T00:00:00",
          "Quantity": 30000,
          "PeriodName": "April 2024"
        },
        {
          "DateTime": "2024-05-01T00:00:00",
          "Quantity": 35000,
          "PeriodName": "May 2024"
        }
      ],
      "Prices": [
        {
          "TradePriceTypeId": 2,  // Index price
          "CurrencyId": 1,
          "UnitOfMeasureId": 10,
          "IndexPricePublisherId": 1,  // OPIS
          "IndexPriceInstrumentId": 100,
          "IndexPriceTypeCvId": 101  // Monthly Average
        }
      ]
    }
  ]
}
```

**Example Response:**
```json
{
  "success": true,
  "tradeEntryId": 12345,
  "internalContractNumber": null,
  "orderStatus": "Draft",
  "validationErrors": [],
  "message": "Deal created successfully with ID: 12345 | Products: Propane: 185,000 gals, Butane: 90,000 gals | Total Volume: 275,000 gallons"
}
```

**Business Logic:**
- Creates new trade entry if TradeEntryId is not provided
- Updates existing trade entry if TradeEntryId is provided
- Validates all required fields and business rules
- If Activate=true, attempts to activate the deal in RightAngle
- If SuppressEmail=true, prevents confirmation emails from being sent
- Returns validation errors if deal cannot be saved
- Transaction is rolled back if any errors occur during save

**Validation Rules:**
- All required fields must be provided
- Dates must be valid and in correct sequence
- Counterparties and colleagues must exist and be active
- Products and locations must be valid and active
- Quantities must be positive numbers
- Prices must follow configured business rules
- Each product detail must have at least one volume allocation
- Total volume per product must be greater than zero

**Volume Allocation Notes:**
- Each TradeEntryDetail represents a single product at a specific location
- Quantities array contains volume allocations by period (typically monthly)
- DateTime in quantities should be the first day of each allocation period
- PeriodName is optional but helpful for display purposes
- Multiple products can be included in a single deal
- Each product can have different pricing structures (fixed, index, formula)
- Volumes are specified in the UnitOfMeasure defined in the detail (gallons, barrels, etc.)

**Common Product IDs (example):**
- 1: Propane
- 2: Butane  
- 3: Isobutane
- 4: Natural Gasoline
- 5: Ethane

---

## Reference Data Endpoints

### 13. Get Products
**Endpoint:** `GET /api/fake/referencedata/products`

**Purpose:** Returns list of available products for trade entry.

**Response Example:**
```json
[
  { "Value": "1", "Text": "Propane", "Code": "C3" },
  { "Value": "2", "Text": "Butane", "Code": "NC4" },
  { "Value": "3", "Text": "Isobutane", "Code": "IC4" }
]
```

### 14. Get Trade Instruments
**Endpoint:** `GET /api/fake/referencedata/tradeinstruments`

**Purpose:** Returns list of available trade instrument types.

### 15. Get Units of Measure
**Endpoint:** `GET /api/fake/referencedata/unitsofmeasure`

**Purpose:** Returns list of available units of measure.

### 16. Get Currencies
**Endpoint:** `GET /api/fake/referencedata/currencies`

**Purpose:** Returns list of available currencies.

### 17. Get All Reference Data
**Endpoint:** `GET /api/fake/referencedata/allreferencedata`

**Purpose:** Returns all reference data in a single call for form initialization.

---

## Deal Management Endpoints

### 18. Get Deal by ID
**Endpoint:** `GET /api/fake/deals/{tradeEntryId}`

**Purpose:** Retrieves complete details of a specific deal.

**Response Example:**
```json
{
  "tradeEntryId": 12345,
  "internalContractNumber": "RA-12345",
  "orderStatus": "Active",
  "totalVolume": 275000,
  "products": ["Propane: 185,000 gals", "Butane: 90,000 gals"],
  "tradeEntryDetails": [...]
}
```

### 19. List Deals
**Endpoint:** `GET /api/fake/deals/list`

**Query Parameters:**
- `status` - Filter by deal status (Draft, Active, Cancelled, Completed)
- `fromDate` - Filter deals starting after this date
- `toDate` - Filter deals ending before this date
- `counterPartyId` - Filter by counter party
- `pageNumber` - Page number for pagination (default: 1)
- `pageSize` - Number of items per page (default: 10)

### 20. Update Deal Status
**Endpoint:** `PUT /api/fake/deals/{tradeEntryId}/status`

**Request Body:**
```json
{
  "newStatus": "Active",
  "reason": "Approved by manager"
}
```

### 21. Cancel Deal
**Endpoint:** `DELETE /api/fake/deals/{tradeEntryId}`

**Request Body (optional):**
```json
{
  "reason": "Customer request",
  "cancelledBy": "John Smith"
}
```

### 22. Get Deal History
**Endpoint:** `GET /api/fake/deals/{tradeEntryId}/history`

**Purpose:** Returns audit history of changes to the deal.

### 23. Validate Deal
**Endpoint:** `GET /api/fake/deals/{tradeEntryId}/validation`

**Purpose:** Validates deal and returns any warnings or errors.

---

## Common Response Codes

| Code | Description |
|------|-------------|
| 200 | Success - Request processed successfully |
| 400 | Bad Request - Invalid parameters or missing required data |
| 401 | Unauthorized - Missing or invalid JWT token |
| 403 | Forbidden - User lacks permission for this operation |
| 404 | Not Found - Requested resource doesn't exist |
| 500 | Internal Server Error - Server-side processing error |

---

## Data Models

### TradeEntryQuantityModel
Used in POST request bodies for quantity-based calculations:
```json
{
  "DateTime": "string (ISO 8601)",  // Date for the quantity
  "Quantity": "number"               // Quantity value (decimal)
}
```

### SelectListItem
Common response format for dropdown/selection lists:
```json
{
  "Value": "string",  // Item ID or value
  "Text": "string"    // Display text
}
```

---

## Notes

1. **Authentication**: All endpoints require a valid JWT token in the Authorization header
2. **CORS**: Configured for external access with "AllowExternal" policy
3. **Date Format**: All dates should be provided in ISO 8601 format
4. **Decimal Precision**: Prices are typically returned with 5 decimal places
5. **Caching**: Some reference data (locations, products) may be cached for performance
6. **Error Handling**: Failed requests return appropriate HTTP status codes with error details in response body

---

## Testing with cURL

### Get JWT Token (example - implement your auth endpoint)
```bash
curl -X POST http://localhost:5000/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpass"}'
```

### Call API with Token
```bash
curl http://localhost:5000/api/custom/tradeentry/externalcompanies?getByPrimaryMarketer=true \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### POST with JSON Body
```bash
curl -X POST http://localhost:5000/api/custom/tradeentry/basepricedefault \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"PriceDictionary":{"month0":"2.45"},"FrequencyType":1,"Quantities":[{"DateTime":"2024-02-01T00:00:00","Quantity":10000}]}'
```