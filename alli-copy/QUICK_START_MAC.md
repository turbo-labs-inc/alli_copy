# Quick Start Guide - QDE API on Mac

## What You're Getting
A working QDE Trade Entry API with 11 endpoints that return mock data. No database needed!

## 3-Minute Setup

### Step 1: Clone/Get the Code
Get the repository and navigate to the API folder:
```bash
cd Custom.Gravitate.WebAPI.Core
```

### Step 2: Install .NET 6 (if you don't have it)
```bash
# Check if you have .NET
dotnet --version

# If not installed, run this one-liner:
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 6.0 --install-dir ~/.dotnet

# Add to your current session
export PATH="$HOME/.dotnet:$PATH"

# Add to your shell profile (for permanent setup)
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.zshrc
```

### Step 3: Run the API
```bash
# From the Custom.Gravitate.WebAPI.Core folder
dotnet run
```

### Step 4: Access Swagger
Open your browser to: **http://localhost:5000/swagger**

## That's It! 🎉

You now have access to all 11 endpoints under the "FAKE Trade Entry" section in Swagger.

## What's Available

### Base URL for all endpoints:
`http://localhost:5000/api/fake/tradeentry`

### Endpoints You Can Test:
- **GET** `/externalcompanies?getByPrimaryMarketer=false` - Returns list of companies
- **GET** `/customfrequencyvalues` - Returns frequency options
- **GET** `/customoriginlocations?showFiltered=false` - Returns origin locations
- **GET** `/customdestinationlocations?showFiltered=false` - Returns destination locations
- **GET** `/pricecomponents/123` - Returns price breakdown
- **GET** `/pricepublishers?priceType=1` - Returns price publishers
- **GET** `/bookfromlocation/100` - Returns book ID
- **GET** `/previousaverageopisprice?locationId=100&productId=5&fromDateString=2024-01-15` - Returns OPIS price
- **GET** `/customindexpricetypes?pricePublisherId=1` - Returns price types
- **POST** `/locationdiffpricedefault` - Calculates location diff price
- **POST** `/basepricedefault` - Calculates base price

## Testing with cURL

Quick test to verify it's working:
```bash
curl "http://localhost:5000/api/fake/tradeentry/externalcompanies?getByPrimaryMarketer=false"
```

Should return:
```json
[
  {"value": "1001", "text": "ABC Trading Company"},
  {"value": "1002", "text": "XYZ Logistics Inc"},
  {"value": "1003", "text": "Global Petroleum Corp"}
]
```

## Troubleshooting

### Port 5000 already in use?
```bash
# Find what's using it
lsof -i :5000

# Kill it if needed
kill -9 <PID>
```

### .NET not found after installation?
```bash
# Make sure path is set
export PATH="$HOME/.dotnet:$PATH"
source ~/.zshrc
```

### Build errors?
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
dotnet run
```

## Important Files

- **Controllers/FakeCustomTradeEntryApiController.cs** - All the mock endpoints (easy to delete later)
- **Controllers/RealCustomTradeEntryApiController.cs** - Real DB endpoints (needs database connection)
- **appsettings.json** - Configuration (connection strings, etc.)

## Notes

- All FAKE endpoints return hardcoded mock data - perfect for frontend development
- REAL endpoints exist but need database connectivity to work
- When ready for production, just delete the FakeCustomTradeEntryApiController.cs file
- Swagger groups endpoints clearly: "FAKE Trade Entry" vs "REAL Trade Entry"

## Need More Info?

- Full API documentation: `QDE_API_ENDPOINT_DOCUMENTATION.md`
- Complete project docs: `Custom.Gravitate.WebAPI.Core/README.md`

---

**Contact**: If you have issues, the API is already tested and working - all endpoints return data as documented above.