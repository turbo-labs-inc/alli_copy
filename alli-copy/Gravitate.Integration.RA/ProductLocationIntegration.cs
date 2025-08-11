using Castle.Core;
using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using MoreLinq;
using MoreLinq.Extensions;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class ProductLocationMappingIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ProductLocationMappingIntegration));

        //private readonly IQueryable<PricingNotificationDetailEntity> _pricingNotificationDetailQueryable;
        private IQueryable<ProductLocaleEntity> _productLocationMappingQueryable = null;
        private readonly IQueryable<DealDetailProvisionRowEntity> _dealDetailProvisionRowQueryable;
        private readonly IQueryable<RawPriceLocaleEntity> _rawPriceLocaleQueryable;
        private readonly IQueryable<AlternateProductNameEntity> _alternateProductQueryable;
        private readonly IProductLocationMappingIntegrationService _productLocationMappingIntegrationService;
        private readonly IQueryable<DealHeaderEntity> _dealHeaderQueryable;
        private readonly IQueryable<DealDetailEntity> _dealDetailQueryable;

        public ProductLocationMappingIntegration(IQueryable<DealHeaderEntity> dealHeaderEntityQueryable, IQueryable<DealDetailEntity> dealDetailEntityQueryable, IQueryable<RawPriceLocaleEntity> rawPriceLocaleQueryable, IQueryable<AlternateProductNameEntity> alternateProductQueryable, IProductLocationMappingIntegrationService productLocationMappingIntegrationService, IQueryable<DealDetailProvisionRowEntity> dealDetailProvisionRowQueryable, IQueryable<ProductLocaleEntity> productLocationMappingQueryable)
        {
            //_pricingNotificationDetailQueryable = pricingNotificationDetailQueryable;
            _rawPriceLocaleQueryable = rawPriceLocaleQueryable;
            _productLocationMappingIntegrationService = productLocationMappingIntegrationService;
            _dealDetailProvisionRowQueryable = dealDetailProvisionRowQueryable;
            _alternateProductQueryable = alternateProductQueryable;
            _productLocationMappingQueryable = productLocationMappingQueryable;
            _dealHeaderQueryable = dealHeaderEntityQueryable;
            _dealDetailQueryable = dealDetailEntityQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var localeTypeIdsToExclude = base.GetIntArray("LocaleTypeIdsToExclude");

            //var rawPrices = _rawPriceLocaleQueryable.Where(rpl => rpl.RawPriceHeader.RphdrDesc == "Rack Posting").Select(rpl => new { rpl.RwPrceLcleId, LcleId = rpl.RplcleLcleId, PrdctId = rpl.RplcleChmclParPrdctId, rpl.RawPriceHeader.RphdrId });
            //var rplDictionary = rawPrices.ToDictionary(rpl => rpl.RwPrceLcleId);
            //var notificationDictionary = _pricingNotificationDetailQueryable.Select(pnd => new { pnd.DlDtlPrvsnId, pnd.PricingNotificationHeader.ExtrnlBaid, pnd.PrdctId, pnd.LcleId }).ToDictionary(pnd => pnd.DlDtlPrvsnId);
            //var dealDetailProvisionIds = notificationDictionary.Keys.ToArray();

            // look up all Active Rack Contracts - Rack DlHdrType = 14
            var DealHeaderIDs = _dealHeaderQueryable.Where(dh => dh.DlHdrTyp == 14 && dh.DlHdrStat == "A").Select(dh => new { dh.DlHdrId, dh.DlHdrExtrnlBaid });
            //now join and find all deal details for these rack contracts
            var dealDetails = _dealDetailQueryable.Join(DealHeaderIDs, dd => dd.DlDtlDlHdrId, dh => dh.DlHdrId, (dd, dh) => new { dh.DlHdrId, dd.DlDtlId, dh.DlHdrExtrnlBaid, dd.DlDtlPrdctId, dd.DlDtlLcleId });

            // join to RawPrices
            /*     var fullcombination = _rawPriceLocaleQueryable.Where(rpl => rpl.RawPriceHeader.RphdrDesc == "Rack Posting").Select(rpl => new { rpl.RwPrceLcleId, LcleId = rpl.RplcleLcleId, PrdctId = rpl.RplcleChmclParPrdctId, rpl.RawPriceHeader.RphdrId }).Join(dealDetails, rp => new { rp.LcleId, rp.PrdctId }
                                                                 , dd => new { dd.DlDtlLcleId, dd.DlDtlPrdctId }
                                                                 , (rp, dd) => new { rp.PrdctId, rp.LcleId, dd.DlHdrExtrnlBaid, rp.RwPrceLcleId, sourceSystemId });

                 */
            // join to RawPrices
            /*   var fullcombination = _rawPriceLocaleQueryable.Join(dealDetails, rpl => new { rpl.RplcleLcleId, rpl.RplcleChmclParPrdctId }
                                                               , dd => new { dd.DlDtlLcleId, dd.DlDtlPrdctId }
                                                               , (rpl, dd) => new { rpl.RplcleChmclParPrdctId, rpl.RplcleLcleId, dd.DlHdrExtrnlBaid, rpl.RwPrceLcleId, sourceSystemId });
                                                               .Where(rpl => rpl.RawPriceHeader.RphdrDesc == "Rack Posting")
                                                               .Select(rpl => new { PrdctId = rpl.RplcleChmclParPrdctId, LcleId = rpl.RplcleLcleId, ExtrnlBaid = rpl.DlHdrExtrnlBaid, rpl.RwPrceLcleId, sourceSystemId }
                                                               );
            */

            var fullcombination = _rawPriceLocaleQueryable.Join(dealDetails, rpl => new { lcleid = rpl.RplcleLcleId, prdctid = rpl.RplcleChmclParPrdctId }
                                                                , dd => new { lcleid = dd.DlDtlLcleId, prdctid = dd.DlDtlPrdctId }
                                                                , (rpl, dd) => new { rpl.RplcleChmclParPrdctId, rpl.RplcleLcleId, dd.DlHdrExtrnlBaid, rpl.RwPrceLcleId, RphdrDesc = rpl.RawPriceHeader.RphdrDesc, sourceSystemId })         
                                                            .Where(rpl => rpl.RphdrDesc == "Rack Posting")
                                                            .Select(rpl => new { PrdctId = rpl.RplcleChmclParPrdctId, LcleId = rpl.RplcleLcleId, ExtrnlBaid = rpl.DlHdrExtrnlBaid, rpl.RwPrceLcleId, sourceSystemId }
                                                            );

            // var dealDetailsDictionary = _dealDetailEntityQueryable.Where(dd => ).Select(dd => new { dd.DlDtlDlHdrId, dd.DlDtlId, dd.ExternalBaid, dd.DlDtlPrdctId, dd.DlDtlLcleId }).ToDictionary(dd => (dd.DlDtlDlHdrId * 100000) + dd.DlDtlId);

            var globalProductId = GetInt("GlobalProductId", true);
            var pricingDtoList = new List<ProductLocationMappingIntegrationDTO.MappingDTO>();
            var tradeEntryDtoList = new List<ProductLocationMappingIntegrationDTO.MappingDTO>();
            
            Log.Info($"Starting tradeEntryDtoList GlobalProductId {globalProductId}, SourceSystemId:{sourceSystemId}");
            tradeEntryDtoList = _productLocationMappingQueryable.Where(x => !localeTypeIdsToExclude.Contains(x.Locale.LcleTpeId)).Distinct()
                .Select(x => TranslateEntity(globalProductId, x.LcleId, null, null, sourceSystemId)).ToList();

            /*   foreach (var dealDetailBatch in dealDetailProvisionIds.Batch(1000))
               {
                   var rphStrings = rawPrices.Select(x => x.RphdrId.ToString()).Distinct().ToArray();

                   var objects = _dealDetailProvisionRowQueryable
                       .Where(ddpr => rphStrings.Contains(ddpr.PriceAttribute6) && ddpr.DlDtlPrvsnRwTpe == "F" && dealDetailBatch.Contains(ddpr.DlDtlPrvsnId)).Select(ddpr => new { ddpr.DlDtlPrvsnId, RwPrceLcleIdString = ddpr.PriceAttribute3 }).Distinct();


                   var dtos = objects.Select(
                       ddpr => TranslateEntity(
                           notificationDictionary[ddpr.DlDtlPrvsnId].PrdctId,
                           notificationDictionary[ddpr.DlDtlPrvsnId].LcleId,
                           notificationDictionary[ddpr.DlDtlPrvsnId].ExtrnlBaid,
                           rplDictionary[Convert.ToInt32(ddpr.RwPrceLcleIdString)].RwPrceLcleId,
                           sourceSystemId));
                   pricingDtoList = pricingDtoList.Concat(dtos.ToList()).ToList();
               }
            */

          foreach ( var row in fullcombination)
            {
                ProductLocationMappingIntegrationDTO.MappingDTO mapitem = new ProductLocationMappingIntegrationDTO.MappingDTO();

                mapitem = TranslateEntity(row.PrdctId, row.LcleId, row.ExtrnlBaid, row.RwPrceLcleId, row.sourceSystemId);
                pricingDtoList.Add( mapitem );
            }



            Log.Info($"RA Rack Pricing DetailEntity query took {sw.Elapsed.ToString()} for {pricingDtoList.Count()} PricingNotificationDetailEntities");

            ProductLocationMappingIntegrationDTO[] productLocales = new ProductLocationMappingIntegrationDTO[2];

            productLocales[0] = new ProductLocationMappingIntegrationDTO()
            {
                MappingTypeMeaning = ProductLocationMappingType.PriceNotificationMeaning
            };
            productLocales[1] = new ProductLocationMappingIntegrationDTO()
            {
                MappingTypeMeaning = ProductLocationMappingType.TradeEntryMeaning
            };

            productLocales[0].MappingsDto = pricingDtoList.ToArray();  
            productLocales[1].MappingsDto = tradeEntryDtoList.ToArray();

            Log.Info("Calling BulkSyncProducts");
            _productLocationMappingIntegrationService.BulkSyncProductLocationMappings(sourceSystemId, productLocales);
            Log.Info("Completed call to BulkSyncProducts");
        }

        public ProductLocationMappingIntegrationDTO.MappingDTO TranslateEntity(int? prdctId, int lcleId, int? externalBAID, int? RwPrceLcleId, int sourceSystemId)
        {
            ProductLocationMappingIntegrationDTO.MappingDTO mappingDto = new ProductLocationMappingIntegrationDTO.MappingDTO()
            {
                ProductLookup = new RelatedEntityLookup() { SourceId = prdctId, SourceId2 = GetAlternateProductSourceId(prdctId), SourceSystemId = sourceSystemId },
                LocationLookup = new RelatedEntityLookup() { SourceId = lcleId, SourceSystemId = sourceSystemId },
            };
            if (RwPrceLcleId != null)
            {
                mappingDto.PriceInstrumentLookup = new RelatedEntityLookup()
                { SourceId = RwPrceLcleId, SourceSystemId = sourceSystemId };
            }
            if (externalBAID != null)
            {
                mappingDto.CounterPartyLookup = new RelatedEntityLookup()
                { SourceId = externalBAID, SourceSystemId = sourceSystemId };
            }

            return mappingDto;
        }



        public IPathEdge PrefetchPricingNotification => new PathEdge<PricingNotificationDetailEntity>(PricingNotificationHeaderEntity.PrefetchPathPricingNotificationDetails,
            new PathEdge<DealDetailProvisionEntity>(PricingNotificationDetailEntity.PrefetchPathDealDetailProvision,
                new PathEdge<DealDetailProvisionRowEntity>(DealDetailProvisionEntity.PrefetchPathDealDetailProvisionRows)));

    }

}
