using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using MoreLinq;
using SD.LLBLGen.Pro.LinqSupportClasses;
using SD.LLBLGen.Pro.ORMSupportClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class PriceServiceSetupIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PriceServiceSetupIntegration));

        private readonly IQueryable<RawPriceHeaderEntity> _rawPriceHeaderQueryable;
        private readonly IQueryable<VCustomGravitateLogisticsPriceEntity> _raLogisticsPriceQueryable;
        private readonly IPriceIntegrationService _pricePublisherIntegrationService;
        private HashSet<int> _validInstrumentHashSet;


        public PriceServiceSetupIntegration(
            IQueryable<RawPriceHeaderEntity> rawPriceHeaderQueryable,
            IPriceIntegrationService pricePublisherIntegrationService, IQueryable<VCustomGravitateLogisticsPriceEntity> raLogisticsPriceQueryable)
        {
            _rawPriceHeaderQueryable = rawPriceHeaderQueryable;
            _pricePublisherIntegrationService = pricePublisherIntegrationService;
            _raLogisticsPriceQueryable = raLogisticsPriceQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
            RawPriceHeaderEntity[] raPricePublishers = _rawPriceHeaderQueryable.WithPath(PrefetchPaths).ToArray();
            Log.Info($"RA RawPriceHeader query took {sw.Elapsed.ToString()} for {raPricePublishers.Count()} RawPriceHeaders");

            _validInstrumentHashSet = _raLogisticsPriceQueryable.WithHint("FORCE ORDER", HintType.QueryHint).Select(x => x.PriceInstrumentSourceId).Distinct().ToHashSet();
            PricePublisherIntegrationDTO[] pricePublisherDtOs = raPricePublishers.Select(rac => TranslateEntity(rac, sourceSystemId)).ToArray();

            Log.Info("Calling BulkSyncPriceServiceSetup");
            _pricePublisherIntegrationService.BulkSyncPriceStructure(sourceSystemId, pricePublisherDtOs);
            Log.Info("Completed call to BulkSyncPriceServiceSetup");
        }

        public PricePublisherIntegrationDTO TranslateEntity(RawPriceHeaderEntity raPricePublisher, int sourceSystemId)
        {
            PricePublisherIntegrationDTO ppDto = new PricePublisherIntegrationDTO();
            ppDto.Name = raPricePublisher.RphdrNme;
            ppDto.Abbreviation = raPricePublisher.RphdrAbbv;
            ppDto.PricePublisherTypeMeaning = SetPricePublisherTypeMeaning(raPricePublisher); //PricingNotification is using PricePublisherType of Rack, so we want to make sure we don't have collisions here.
            ppDto.IsActive = raPricePublisher.RphdrStts == RAStatus.Active ? true : false;
            ppDto.SourceId = raPricePublisher.RphdrId;
            ppDto.PriceInstrumentDTOs = GetPriceInstrumentDtOs(sourceSystemId, raPricePublisher, ppDto.PricePublisherTypeMeaning);
            ppDto.PriceTypeDTOs = GetPriceTypeIntegrationDtos(raPricePublisher).DistinctBy(x => x.PriceTypeMeaning).ToList();
            return ppDto;
        }

        private string SetPricePublisherTypeMeaning(RawPriceHeaderEntity raPricePublisher)
        {
            string typeMeaning = "";

            if (raPricePublisher.RphdrDesc == "Rack Posting")
            {
                return CodeSetFacade.Instance.PricePublisherType.RackPosting.Meaning;
            }

            if (raPricePublisher.GeneralConfigurations.Any(gc => gc.GnrlCnfgQlfr == "AESPriceServiceType"))
            {
                typeMeaning = raPricePublisher.GeneralConfigurations
                    .Single(gc => gc.GnrlCnfgQlfr == "AESPriceServiceType").GnrlCnfgMulti.Replace(" ", "").Replace("TR-Surcharge", "TruckSurcharge").Replace("TR-Tariff", "TruckTariff"); ;
            }

            return typeMeaning;
        }

        private List<PriceInstrumentIntegrationDTO> GetPriceInstrumentDtOs(int sourceSystemId, RawPriceHeaderEntity rph, string pricePublisherTypeMeaning)
        {
            //Documentations says entityCollection should be empty not null
            var collection = rph.RawPriceLocales.Select(rpl => TranslateRawPriceLocaleEntity(rpl, sourceSystemId)).ToList();
            // Add counter party to price instruments

            //Custom to NGL: We don't want to set a CounterParty on Rack prices so that everyone can see them
            if (pricePublisherTypeMeaning != CodeSetFacade.Instance.PricePublisherType.RackPosting.Meaning)
            {
                var counterPartyIdLookup = rph.RphdrBaid.HasValue ? new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = rph.RphdrBaid } : null;
                if (counterPartyIdLookup != null)
                {
                    collection.ForEach(x => x.CounterPartyIdLookup = counterPartyIdLookup);
                }
            }

            return collection;
        }

        private PriceInstrumentIntegrationDTO TranslateRawPriceLocaleEntity(RawPriceLocaleEntity rpl, int sourceSystemId)
        {
            PriceInstrumentIntegrationDTO priceInstrument = new PriceInstrumentIntegrationDTO();




            priceInstrument.Name = rpl.CurveName;
            priceInstrument.Abbreviation = rpl.CurveName;
            priceInstrument.IsActive = rpl.Status != RAStatus.Inactive; //I think this should be switched but copied extract
            priceInstrument.UnitOfMeasureIdLookup = null;
            priceInstrument.CurrencyIdLookup = null;
            priceInstrument.ProductIdLookup = new RelatedEntityLookup() { SourceId = rpl.RplcleChmclChdPrdctId, SourceId2 = GetAlternateProductSourceId(rpl.RplcleChmclChdPrdctId), SourceSystemId = sourceSystemId };
            priceInstrument.LocationIdLookup = new RelatedEntityLookup() { SourceId = rpl.RplcleLcleId, SourceSystemId = sourceSystemId };
            priceInstrument.ToCurrencyIdLookup = null; //ToCrrncyID (don't think exists on S9)
            priceInstrument.SourceId = rpl.RwPrceLcleId;

            return priceInstrument;
        }

        #region PriceTypeIntegration
        private readonly List<String> _missingPriceTypes = new List<String>();

        private IEnumerable<PriceTypeIntegrationDTO> GetPriceTypeIntegrationDtos(RawPriceHeaderEntity x)
        {
            return x.RawPriceHeaderPriceTypes.Select(TranslatePriceTypeIntegrationDto).Where(y => y.PriceTypeMeaning != null);
        }

        private PriceTypeIntegrationDTO TranslatePriceTypeIntegrationDto(RawPriceHeaderPriceTypeEntity rphPriceType)
        {
            if (_validInstrumentHashSet == null)
            {
                return new PriceTypeIntegrationDTO()
                {
                    PriceTypeMeaning = GetPriceTypeCodeValue(rphPriceType.Pricetype.PrceTpeNme),
                    ExtractPrices = false
                };
            }
            else
            {
                return new PriceTypeIntegrationDTO()
                {
                    PriceTypeMeaning = GetPriceTypeCodeValue(rphPriceType.Pricetype.PrceTpeNme),
                    ExtractPrices = rphPriceType.RawPriceHeader.RawPriceLocales.Any(x => _validInstrumentHashSet.Contains(x.RwPrceLcleId)) ? (bool?)true : null
                };
            }
        }

        private string GetPriceTypeCodeValue(string prceTpeNme)
        {
            prceTpeNme = prceTpeNme.Replace(" ", String.Empty);
            prceTpeNme = CodeSetIntegration.GetPriceTypeCodeValueMeaning(prceTpeNme);
            var ptc = (PriceTypeCodeValue)CodeSetFacade.Instance.PriceType.GetCodeValue(prceTpeNme);
            if (ptc == null)
            {
                _missingPriceTypes.Add($"Unable to find Price Type: {prceTpeNme.Trim()}");
            }
            return ptc.Meaning;
        }
        #endregion


        public IPathEdge[] PrefetchPaths => new[]
        {
            PrefetchRawPriceLocale,
            PrefetchRawPriceHeaderGeneralConfig,
            PrefetchPathRawPriceHeaderPriceType
        };

        public IPathEdge PrefetchRawPriceLocale => new PathEdge<RawPriceLocaleEntity>(RawPriceHeaderEntity.PrefetchPathRawPriceLocales);

        public IPathEdge PrefetchRawPriceHeaderGeneralConfig => GetGeneralConfigPrefetches(RawPriceHeaderEntity.PrefetchPathGeneralConfigurations, "RawPriceHeader");

        public IPathEdge PrefetchPathRawPriceHeaderPriceType => new PathEdge<RawPriceHeaderPriceTypeEntity>(RawPriceHeaderEntity.PrefetchPathRawPriceHeaderPriceTypes,
                                                                    new PathEdge<PricetypeEntity>(RawPriceHeaderPriceTypeEntity.PrefetchPathPricetype));


    }
}
