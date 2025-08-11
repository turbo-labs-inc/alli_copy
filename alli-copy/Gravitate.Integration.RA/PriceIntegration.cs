using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Integration.RA.DAL.HelperClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using MoreLinq.Extensions;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

namespace Gravitate.Integration.RA
{
    public class PriceIntegration : BaseIncrementalIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PriceIntegration));

        private readonly IQueryable<RawPriceDetailEntity> _rpdQueryable;
        private readonly IQueryable<RawPriceEntity> _rawPriceQueryable;
        private readonly IQueryable<PricetypeEntity> _raPriceType;

        private readonly IPriceIntegrationService _priceIntegrationService;

        private int[] EstimateAsActualPriceHeaders => GetIntArray("Global.PriceHeader.ConvertEstimateToActual") ?? new int[0];

        private PriceIntegrationQueryFilterDTO[] PriceQueryFilters;
        private IDictionary<short, PricetypeEntity> PriceTypeDictionary;



        public PriceIntegration(IQueryable<RawPriceDetailEntity> rpdQueryable, IPriceIntegrationService priceIntegrationService, IQueryable<RawPriceEntity> rawPriceQueryable, IQueryable<PricetypeEntity> raPriceType)
        {
            _rpdQueryable = rpdQueryable;
            _priceIntegrationService = priceIntegrationService;
            _rawPriceQueryable = rawPriceQueryable;
            _raPriceType = raPriceType;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            PriceQueryFilters = _priceIntegrationService.GetExtractedPriceTypes(sourceSystemId);
            PriceTypeDictionary = Enumerable.ToDictionary(_raPriceType.ToArray(), key => key.Idnty);

            int batchSize = base.GetInt("BatchSize", true).Value;
            int sourceBatchSize = base.GetInt("BatchSize.SourceSystem", true).Value;
            int gravitateBatchSize = base.GetInt("BatchSize.Gravitate", true).Value;
            int resultCount = 0;

            do
            {
                resultCount = ProcessBatchMultiQuery(sourceSystemId, status, sourceBatchSize, gravitateBatchSize, resultCount, false);
                //resultCount = ProcessBatch(sourceSystemId, status, batchSize, resultCount, false);

            } while (resultCount == sourceBatchSize);
        }

        private int ProcessBatchMultiQuery(int sourceSystemId, IntegrationStatus status, int sourceBatchSize, int gravitateBatchSize, int resultCount, bool isResync)
        {
            var result = QueryAndConvertPrices(status, sourceSystemId, sourceBatchSize);
            resultCount = result.Item1;
            int? maxPkId = result.Item2;
            DateTime? maxSyncDateTime = result.Item3;
            Log.Info($"Acquired batch of {result.Item1} prices to process from source system. Batch MaxPkId: {maxPkId}, MaxSyncDateTime: {maxSyncDateTime}");
            Log.Info($"Generating batches of size {gravitateBatchSize}");
            var batches = result.Item4.Batch(gravitateBatchSize).Select(x => x.ToArray()).ToList();
            var totalBatches = batches.Count;
            var currentBatch = 1;
            foreach (var dtoBatch in batches)
            {
                Log.Info($"Processing batch {currentBatch} / {totalBatches}");
                _priceIntegrationService.CreateOrUpdatePrices(sourceSystemId, dtoBatch);
                currentBatch++;
            }


            status.MaxSyncDateTime = maxSyncDateTime;
            status.MaxSyncDateMaxPKId = maxPkId;
            return resultCount;

        }


        public Expression<Func<RawPriceDetailEntity, bool>> GetPriceFilterExpression()
        {
            DateTime loadStartDate = base.GetDateTime("LoadStartDate", true).Value;

            DateTime currentDateSmallDateCutoff = DateTime.Now.SetSecond(0).SetMillisecond(0); //Only get updates prior to the start of the current minute

            return rpd => rpd.RpdtlEntryDte < currentDateSmallDateCutoff && rpd.RpdtlEntryDte >= loadStartDate; // we used to use RpdtlQteToDte but RpdtlEntryDte performs much better
        }



        private Tuple<int, int?, DateTime?, CurvePointIntegrationDTO[]> QueryAndConvertPrices(IntegrationStatus status, int sourceSystemId, int batchSize)
        {
            var query = _rpdQueryable.Where(GetPriceFilterExpression());

            if (status.MaxSyncDateTime.HasValue && status.MaxSyncDateMaxPKId.HasValue)
            {
                query = query.Where(dh => dh.RpdtlEntryDte > status.MaxSyncDateTime || (dh.RpdtlEntryDte == status.MaxSyncDateTime && dh.Idnty > status.MaxSyncDateMaxPKId.Value));
            }

            if (PriceQueryFilters.Length <= 2000)
            {
                var rawPriceLocals = PriceQueryFilters.Select(x => x.PriceInstrumentLookup?.SourceId).Where(x => x.HasValue).Select(x => x.Value).Distinct().ToArray();
                var rawPriceHeaders = PriceQueryFilters.Where(x => x.PriceInstrumentLookup == null)
                        .Select(x => x.PricePublisherLookup?.SourceId).Where(x => x.HasValue).Select(x => x.Value)
                        .Distinct().ToArray();

                query = query.Where(x => rawPriceHeaders.Contains(x.RawPriceLocale.RplcleRphdrId));
            }



            var rawPriceDetails = query.Select(raRpd => new
            {
                raRpd.RwPrceLcleId,
                RphdrId = raRpd.RawPriceLocale.RplcleRphdrId,
                raRpd.RpdtlQteFrmDte,
                raRpd.RpdtlQteToDte,
                raRpd.RpdtlTrdeFrmDte,
                raRpd.RpdtlTrdeToDte,
                raRpd.RpdtlUom,
                raRpd.RpdtlStts,
                raRpd.Idnty,
                raRpd.RpdtlTpe,
                raRpd.RpdtlEntryDte
            }).WithHint("INDEX(IE_RPDtlEntryDte)");

            var sw = Stopwatch.StartNew();
            var raPrices = rawPriceDetails.OrderBy(dh => dh.RpdtlEntryDte)
                .ThenBy(dh => dh.Idnty)
                .Take(batchSize)
                .ToArray();

            Log.Info($"Raw Price Detail Query Took: {sw.Elapsed}");

            DateTime? maxSyncDateTime = raPrices.Any() ? raPrices.Max(d => d.RpdtlEntryDte) : status.MaxSyncDateTime;
            int? maxPkId = raPrices.Any() ? raPrices.Where(d => d.RpdtlEntryDte == maxSyncDateTime).Max(d => d.Idnty) : status.MaxSyncDateMaxPKId;


            var dtos = raPrices.Select(raRpd =>
            {
                CurvePointIntegrationDTO cpDto = new CurvePointIntegrationDTO();
                cpDto.PriceInstrumentIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raRpd.RwPrceLcleId };
                cpDto.EstimateActual = EstimateAsActualPriceHeaders.Contains(raRpd.RphdrId) ? "A" : raRpd.RpdtlTpe;
                cpDto.EffectiveFromDateTime = raRpd.RpdtlQteFrmDte;
                cpDto.EffectiveToDateTime = raRpd.RpdtlQteToDte;
                cpDto.TradePeriodFromDateTime = raRpd.RpdtlTrdeFrmDte;
                cpDto.TradePeriodToDateTime = raRpd.RpdtlTrdeToDte;
                cpDto.UnitOfMeasureIdLookup = raRpd.RpdtlUom.HasValue ? new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raRpd.RpdtlUom } : null;
                cpDto.SourceId = raRpd.Idnty;
                cpDto.IsActive = (raRpd.RpdtlStts == RAStatus.Active);
                cpDto.CurvePointPriceDTOs = new List<CurvePointPriceIntegrationDTO>();
                return cpDto;
            }).ToArray();


            if (raPrices.Any())
            {
                var rawPriceIdentities = dtos.Select(x => x.SourceId).Distinct().ToArray();

                sw.Restart();
                var rawPriceRecords = rawPriceIdentities.Batch(2000).SelectMany(x =>
                {
                    return _rawPriceQueryable.Where(rp => x.Contains(rp.RprpdtlIdnty))
                            .Select(rp => new
                            {
                                rp.Rpvle,
                                rp.RpprceTpeIdnty,
                                rp.Idnty,
                                DetailId = rp.RprpdtlIdnty
                            }).ToArray();
                }).ToArray();

                Log.Info($"Raw Price Query Took: {sw.Elapsed}");


                var groups = Enumerable.ToLookup(rawPriceRecords, rp => rp.DetailId);

                foreach (var curvePointDto in dtos)
                {
                    var curvePointPriceDtos = groups[curvePointDto.SourceId.Value]
                            .Select(rp =>
                            {
                                CurvePointPriceIntegrationDTO cpp = new CurvePointPriceIntegrationDTO();
                                cpp.Value = Convert.ToDecimal(rp.Rpvle);
                                cpp.PriceTypeMeaning = GetPriceTypeCodeValue(PriceTypeDictionary[rp.RpprceTpeIdnty]);
                                cpp.SourceId = rp.Idnty;
                                return cpp;
                            }).ToArray();

                    curvePointDto.CurvePointPriceDTOs.AddRange(curvePointPriceDtos.Where(dd => dd.PriceTypeMeaning != null));
                }
            }


            return Tuple.Create(raPrices.Length, maxPkId, maxSyncDateTime, dtos);

        }

        public CurvePointIntegrationDTO TranslateEntity(RawPriceDetailEntity raRpd, int sourceSystemId)
        {
            CurvePointIntegrationDTO cpDto = new CurvePointIntegrationDTO();

            cpDto.PriceInstrumentIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raRpd.RwPrceLcleId };
            // treat all prices as actuals for EstimateAsActualPriceHeaders
            cpDto.EstimateActual = EstimateAsActualPriceHeaders.Contains(raRpd.RawPriceLocale.RawPriceHeader.RphdrId) ? "A" : raRpd.RpdtlTpe;
            cpDto.EffectiveFromDateTime = raRpd.RpdtlQteFrmDte;
            cpDto.EffectiveToDateTime = raRpd.RpdtlQteToDte;
            cpDto.TradePeriodFromDateTime = raRpd.RpdtlTrdeFrmDte;
            cpDto.TradePeriodToDateTime = raRpd.RpdtlTrdeToDte;
            cpDto.UnitOfMeasureIdLookup = raRpd.RpdtlUom.HasValue ? new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raRpd.RpdtlUom } : null;
            cpDto.SourceId = raRpd.Idnty;
            cpDto.IsActive = (raRpd.RpdtlStts == RAStatus.Active);

            cpDto.CurvePointPriceDTOs = GetCurvePointPriceDtOs(sourceSystemId, raRpd.RawPrices);

            return cpDto;
        }

        private List<CurvePointPriceIntegrationDTO> GetCurvePointPriceDtOs(int sourceSystemId, EntityCollection<RawPriceEntity> entityCollection)
        {
            //Documentations says entityCollection should be empty not null
            return entityCollection.Select(dd => TranslateRawPriceEntity(dd, sourceSystemId)).Where(dd => dd.PriceTypeMeaning != null).ToList();
        }

        private CurvePointPriceIntegrationDTO TranslateRawPriceEntity(RawPriceEntity rp, int sourceSystemId)
        {
            CurvePointPriceIntegrationDTO cpp = new CurvePointPriceIntegrationDTO();

            cpp.Value = Convert.ToDecimal(rp.Rpvle);
            cpp.PriceTypeMeaning = GetPriceTypeCodeValue(rp.Pricetype);
            cpp.SourceId = rp.Idnty;

            return cpp;
        }

        private string GetPriceTypeCodeValue(PricetypeEntity typeEntity)
        {
            var prceTpeNme = CodeSetIntegration.GetPriceTypeCodeValueMeaning(typeEntity);
            var ptc = (PriceTypeCodeValue)CodeSetFacade.Instance.PriceType.GetCodeValue(prceTpeNme);
            if (ptc == null)
            {
                Log.Warn("Unable to find Price Type: " + prceTpeNme.Trim());
            }
            return ptc.Meaning;
        }


        private static IPathEdge PrefetchRawPrice => new PathEdge<RawPriceEntity>(RawPriceDetailEntity.PrefetchPathRawPrices, PrefetchPriceType);

        private static IPathEdge PrefetchPriceType => new PathEdge<PricetypeEntity>(RawPriceEntity.PrefetchPathPricetype);

        private static IPathEdge PrefetchRawPriceLocaleAndHeader => new PathEdge<RawPriceLocaleEntity>(RawPriceDetailEntity.PrefetchPathRawPriceLocale, PrefetchRawPriceHeader);
        private static IPathEdge PrefetchRawPriceHeader => new PathEdge<RawPriceHeaderEntity>(RawPriceLocaleEntity.PrefetchPathRawPriceHeader);


    }
}
