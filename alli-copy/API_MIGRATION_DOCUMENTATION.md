# Custom Trade Entry API Migration Documentation

## Overview
Migration of `CustomTradeEntryController` from ASP.NET MVC to Web API for external access support.

---

## 1. Accessory Development for Controller Porting

### Extension Methods Required
- **`ToSelectList()` Extensions** 
  - Located in: `/Gravitate.Web/Extensions/KendoExtensions.cs`
  - Purpose: Converts entity collections to SelectListItem for dropdown data
  - Used for: CounterParty, Location, Product entities

- **`MonthDiff()` Extension**
  - Located in: `/Gravitate.Library/DateTimeExtensions.cs`
  - Purpose: Calculates month difference between dates for price calculations
  - Used in: Base price default calculations

- **`EndOfDay()` Extension**
  - Located in: `/Gravitate.Library/DateTimeExtensions.cs`
  - Purpose: Gets end of day timestamp (23:59:59)
  - Used in: OPIS price date range queries

### Request/Response Models Created
- **`LocationDiffPriceRequest`**
  - Properties: LocationId, ProductId, Quantities
  - Purpose: POST body for location differential price calculations

- **`BasePriceDefaultRequest`**
  - Properties: PriceDictionary, FrequencyType, Quantities
  - Purpose: POST body for weighted base price calculations

- **`DefaultFuelSurchargeModel`**
  - Properties: Rail/Truck surcharge IDs and publisher IDs
  - Purpose: Deserialize fuel surcharge configuration from app settings

---

## 2. Authentication Configuration

### Session to JWT Migration
- **Original**: Used `SessionCache.Instance.Colleague` for user context
- **Migrated to**: `ApiCache.Instance.Colleague` with JWT token claims
- **Base Class**: Inherits from `GravitateSecureApiController` for automatic JWT validation

### Security Setup
- **JWT Bearer Authentication**: Configured in Program.cs for .NET 6 version
- **CORS Policy**: "AllowExternal" policy enables cross-origin requests
- **Token Validation**: Symmetric key validation with configurable secret

### Authentication Flow
```
External Client → JWT Token → API validates → Access granted to endpoints
```

---

## 3. API Endpoints Documentation

### GET Endpoints

#### `/api/custom/tradeentry/externalcompanies`
- **Parameters**: `getByPrimaryMarketer` (bool)
- **Purpose**: Retrieves list of external companies/counterparties
- **Logic**: 
  - If `getByPrimaryMarketer=true`: Returns companies associated with current user's colleague
  - If `false`: Returns all active carriers and customers
- **Returns**: List of SelectListItem (id/text pairs)

#### `/api/custom/tradeentry/customfrequencyvalues`
- **Parameters**: None
- **Purpose**: Gets configured trade quantity frequency types
- **Logic**: Reads allowed frequency types from app settings
- **Returns**: Frequency code values for trade entry forms

#### `/api/custom/tradeentry/customoriginlocations`
- **Parameters**: `showFiltered` (bool), `externalCounterParty` (int?)
- **Purpose**: Retrieves origin/loading locations for trades
- **Logic**:
  - If filtered: Returns only locations with existing trade details for the counterparty
  - Otherwise: Returns all active trading locations
- **Returns**: Location list for origin selection

#### `/api/custom/tradeentry/customdestinationlocations`
- **Parameters**: `showFiltered` (bool), `externalCounterParty` (int?)
- **Purpose**: Retrieves destination/delivery locations
- **Logic**:
  - If filtered: Returns locations with trade history for counterparty
  - Otherwise: Returns locations based on product location mapping types
- **Returns**: Location list for destination selection

#### `/api/custom/tradeentry/pricecomponents/{priceId}`
- **Parameters**: `priceId` (int) in route
- **Purpose**: Gets price breakdown components for a specific price
- **Logic**: Queries price component table and groups by component type
- **Returns**: Dictionary of component names to price values

#### `/api/custom/tradeentry/pricepublishers`
- **Parameters**: `priceType` (int)
- **Purpose**: Returns relevant price publishers for a price type
- **Logic**: Filters publishers based on fuel surcharge configuration
- **Returns**: List of price publisher options

#### `/api/custom/tradeentry/bookfromlocation/{locationId}`
- **Parameters**: `locationId` (int) in route
- **Purpose**: Gets the book ID associated with a location
- **Returns**: Book ID for accounting/settlement purposes

#### `/api/custom/tradeentry/previousaverageopisprice`
- **Parameters**: `locationId`, `productId`, `fromDateString`
- **Purpose**: Retrieves historical OPIS average price
- **Logic**:
  - Maps location to price curve (Conway, Mont Belvieu TET/NTET)
  - Searches back up to 100 days for valid price
  - Calculates average of configured price types
- **Returns**: Average OPIS price for the date

#### `/api/custom/tradeentry/customindexpricetypes`
- **Parameters**: `pricePublisherId` (int?), `filterText` (string)
- **Purpose**: Gets index price types for a publisher
- **Logic**: Returns price types with optional text filtering
- **Returns**: Filtered list of price type options

### POST Endpoints

#### `/api/custom/tradeentry/locationdiffpricedefault`
- **Body**: `LocationDiffPriceRequest`
- **Purpose**: Calculates weighted location differential price
- **Logic**:
  - Takes quantity schedule
  - Adds 14 days to each date
  - Calculates weighted average based on quantities
- **Returns**: Rounded weighted price

#### `/api/custom/tradeentry/basepricedefault`
- **Body**: `BasePriceDefaultRequest`
- **Purpose**: Calculates weighted or max base price
- **Logic**:
  - Calculates month difference from current date
  - Uses price dictionary lookup by month offset
  - Returns either weighted average or max based on frequency type
- **Returns**: Calculated base price

---

## 4. Local Development Setup

### Files Created for Mac Development

#### `run-webapi-standalone.sh`
- **Purpose**: Quick start script for .NET 6 version
- **Features**:
  - Auto-installs .NET SDK if missing
  - Runs with hot reload
  - Provides Swagger UI
- **Usage**: `./run-webapi-standalone.sh`

#### `run-local-mac.sh`
- **Purpose**: Run original .NET Framework code with Mono
- **Features**:
  - Installs Mono and NuGet via Homebrew
  - Builds with msbuild
  - Runs on XSP4 web server
- **Usage**: `./run-local-mac.sh`

#### `docker-compose.yml` & `Dockerfile.webapi`
- **Purpose**: Containerized deployment option
- **Features**:
  - Windows container with IIS
  - Volume mounts for config files
  - Port 8080 exposure
- **Usage**: `docker-compose up`

#### `Custom.Gravitate.WebAPI.Core/` Project
- **Purpose**: .NET 6 cross-platform version
- **Features**:
  - Native Mac/Linux support
  - Modern minimal API setup
  - Swagger documentation
  - JWT authentication pre-configured

### Configuration Files

#### `appsettings.json`
- Connection strings
- JWT secret configuration
- Trade entry settings
- Price publisher mappings

### Development URLs
- **API Base**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger
- **HTTPS**: https://localhost:5001

---

## 5. Key Architectural Changes

### From MVC to Web API
- Changed return types from `ActionResult` to `IHttpActionResult`
- Removed `JsonRequestBehavior.AllowGet` (not needed in Web API)
- Simplified JSON returns (auto-serialization)

### Session State Removal
- Replaced session-based user context with JWT claims
- Removed `SessionModel` dependencies
- Used stateless authentication pattern

### External Access Enablement
- Added CORS configuration
- Implemented JWT bearer authentication
- Created RESTful route structure
- Removed view-specific logic

---

## 6. Testing Recommendations

### Authentication Testing
```bash
# Get JWT token (implement token endpoint)
curl -X POST http://localhost:5000/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"user","password":"pass"}'

# Use token in requests
curl http://localhost:5000/api/custom/tradeentry/externalcompanies?getByPrimaryMarketer=true \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### Endpoint Testing
- Use Swagger UI for interactive testing
- Postman collection can be generated from Swagger
- Integration tests should mock service dependencies

---

## 7. Deployment Considerations

### Production Requirements
- Update JWT secret in configuration
- Configure CORS for specific domains
- Set up SSL certificates
- Configure connection strings
- Implement rate limiting
- Add logging and monitoring

### Security Checklist
- [ ] Replace default JWT secret
- [ ] Configure CORS allowlist
- [ ] Enable HTTPS only
- [ ] Implement request validation
- [ ] Add API versioning
- [ ] Set up API gateway if needed

---

## 8. Next Steps

1. **Implement JWT Token Generation Endpoint**
   - Create `/api/auth/token` endpoint
   - Validate credentials against database
   - Generate and return JWT token

2. **Service Dependency Injection**
   - Wire up Castle Windsor or use built-in DI
   - Register all service interfaces
   - Configure database contexts

3. **Error Handling**
   - Add global exception handler
   - Implement consistent error response format
   - Add logging middleware

4. **API Documentation**
   - Add XML comments to controllers
   - Configure Swagger to use XML docs
   - Add request/response examples

5. **Testing**
   - Create unit tests for controllers
   - Add integration tests
   - Set up CI/CD pipeline