using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class ProductIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ProductIntegration));

        private readonly IQueryable<ProductEntity> _productQueryable;
        private readonly IQueryable<AlternateProductNameEntity> _alternateProductQueryable;
        private readonly IProductIntegrationService _productIntegrationService;
        private IDictionary<string, string> _pricingAndTradingLocaleDictionary;


        public ProductIntegration(IQueryable<ProductEntity> productQueryable, IProductIntegrationService productIntegrationService, IQueryable<AlternateProductNameEntity> alternateProductQueryable)
        {
            _productQueryable = productQueryable;
            _productIntegrationService = productIntegrationService;
            _alternateProductQueryable = alternateProductQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();

            _pricingAndTradingLocaleDictionary = base.GetStringByTypeArray("Gravitate.Integration.RA.LocationIntegration", "PricingAndTradingLocaleTypes", true).Select(x => x.Trim().ToLower()).ToDictionary(key => key);
            var alternateLookup = _alternateProductQueryable.ToLookup(a => a.PrdctId);

            ProductEntity[] raProducts = _productQueryable.WithPath(PrefetchProductLocale).ToArray();

            Log.Info($"RA product query took {sw.Elapsed.ToString()} for {raProducts.Length} products");
            List<ProductIntegrationDTO> productDtOs = new List<ProductIntegrationDTO>();
            foreach (var product in raProducts)
            {
                if (alternateLookup[product.PrdctId].Any())
                {

                    foreach (var alternate in alternateLookup[product.PrdctId])
                    {
                        productDtOs.Add(TranslateEntity(product, alternate));
                    }
                }
                else
                {
                    productDtOs.Add(TranslateEntity(product, null));
                }
            }


            Log.Info($"Calling {nameof(IProductIntegrationService.BulkSyncProducts)}");
            _productIntegrationService.BulkSyncProducts(sourceSystemId, productDtOs.ToArray());
            Log.Info($"Completed call to {nameof(IProductIntegrationService.BulkSyncProducts)}");
        }

        public ProductIntegrationDTO TranslateEntity(ProductEntity raProduct, AlternateProductNameEntity alternateProductName)
        {
            ProductIntegrationDTO piDto = new ProductIntegrationDTO();
            piDto.Name = (alternateProductName != null) ? alternateProductName.AlternateProductName : raProduct.PrdctNme;
            piDto.Abbreviation = (alternateProductName != null) ? alternateProductName.AlternateProductAbbreviation : raProduct.PrdctAbbv;
            piDto.Symbol = (alternateProductName != null) ? alternateProductName.AlternateProductAbbreviation : raProduct.PrdctAbbv;
            piDto.IsPricingProduct = false;
            piDto.IsActive = (alternateProductName != null) ? alternateProductName.Status != RAStatus.Inactive : raProduct.PrdctStat != RAStatus.Inactive;
            piDto.SortOrder = (alternateProductName != null) ? ((alternateProductName.AlternateProductName == raProduct.PrdctNme) ? 10 : 0) : 0;
            piDto.SourceId = raProduct.PrdctId;
            piDto.SourceId2 = alternateProductName?.AlternateProductNameId;
            piDto.ProductTypeMeaning = raProduct.ProductLocales.Any(pl => _pricingAndTradingLocaleDictionary.ContainsKey(pl.Locale.LocaleType.LcleTpeDscrptn.Trim().ToLower())) ? ProductType.TradingProductMeaning : null;

            // Gravitate.WCF.Integration.ProductIntegration.ExtractProductType was false previously, so this is set to never (even though it looks like we derive a product type above)
            //piDto.ProductTypeExtractMode = ExtractMode.Never;

            piDto.ProductTypeExtractMode = ExtractMode.Always;
            return piDto;
        }


        public IPathEdge PrefetchProductLocale =>
            new PathEdge<ProductLocaleEntity>(ProductEntity.PrefetchPathProductLocales,
                new PathEdge<LocaleEntity>(ProductLocaleEntity.PrefetchPathLocale,
                    new PathEdge<LocaleTypeEntity>(LocaleEntity.PrefetchPathLocaleType)));

    }

}
