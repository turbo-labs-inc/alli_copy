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