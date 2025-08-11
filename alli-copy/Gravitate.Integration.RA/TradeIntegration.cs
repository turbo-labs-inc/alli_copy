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
using SD.LLBLGen.Pro.LinqSupportClasses;
using SD.LLBLGen.Pro.ORMSupportClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Gravitate.Integration.RA
{
    public class TradeIntegration : BaseIncrementalIntegration
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(TradeIntegration));
        private const int SourceSystemUomId = 3;

        private readonly IQueryable<DealHeaderEntity> _dhQueryable;
        private readonly IQueryable<VDateRuleNameEntity> _vDateRuleNameQueyable;
        private readonly ITradeIntegrationService _tradeIntegrationService;

        public TradeIntegration(IQueryable<DealHeaderEntity> dhQueryable, ITradeIntegrationService tradeIntegrationService, IQueryable<VDateRuleNameEntity> vDateRuleNameQueyable)
        {
            _dhQueryable = dhQueryable;
            _tradeIntegrationService = tradeIntegrationService;
            _vDateRuleNameQueyable = vDateRuleNameQueyable;
        }


        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            //int[] tradeTypes = new int[] { 2, 14, 1, 20, 100 };
            int batchSize = base.GetInt("BatchSize", true).Value;
            int resultCount = 0;

            Resync(sourceSystemId, batchSize);

            do
            {
                resultCount = ProcessBatch(sourceSystemId, status, batchSize, resultCount, false);

            } while (resultCount == batchSize);
        }

        private void Resync(int sourceSystemId, int batchSize)
        {
            int resultCount = 0;
            IntegrationStatus status = new IntegrationStatus();

            if (base.CounterPartySourceIdsToResync.Any(false))
            {
                _log.Info($"Resyncing Trades for BAIDs: {String.Join(", ", base.CounterPartySourceIdsToResync)}");

                do
                {
                    resultCount = ProcessBatch(sourceSystemId, status, batchSize, resultCount, true);

                } while (resultCount == batchSize);
            }
        }

        private int ProcessBatch(int sourceSystemId, IntegrationStatus status, int batchSize, int resultCount, bool isResync)
        {
            DealHeaderEntity[] raDeals = GetRaDeals(status, batchSize, isResync);

            resultCount = raDeals.Count();
            DateTime? maxSyncDateTime = raDeals.Any(d => d.DlHdrRvsnDte.HasValue) ? raDeals.Max(d => d.DlHdrRvsnDte).Value : status.MaxSyncDateTime;
            int? maxPkId = raDeals.Any() ? raDeals.Where(d => d.DlHdrRvsnDte == maxSyncDateTime).Max(d => d.DlHdrId) : status.MaxSyncDateMaxPKId;

            _log.Info($"Acquired batch of {resultCount} trades to process. Batch MaxPkId: {maxPkId}, MaxSyncDateTime: {maxSyncDateTime}, IsResync: {isResync}");

            var dlHdrIds = raDeals.Select(d => d.DlHdrId).ToArray();
            var dateRuleLookup = _vDateRuleNameQueyable.Where(dr => dlHdrIds.Contains(dr.DealHeaderId)).WithHint("FORCE ORDER", HintType.QueryHint).ToLookup(dr => Tuple.Create(dr.DealHeaderId, dr.DealDetail), dr => dr.DateRule);
            
            _log.Info("Loaded DateRule Lookup");

            TradeIntegrationDTO[] tradeDtOs = raDeals.Select(raDeal => TranslateEntity(raDeal, sourceSystemId, dateRuleLookup)).ToArray();
            _tradeIntegrationService.CreateOrUpdateTrades(sourceSystemId, tradeDtOs);

            status.MaxSyncDateTime = maxSyncDateTime;
            status.MaxSyncDateMaxPKId = maxPkId;
            return resultCount;
        }

        public Expression<Func<DealHeaderEntity, bool>> GetDealFilterExpression()
        {
            int[] tradeTypes = base.GetIntArray("TradeTypes", true);
            DateTime loadStartDate = base.GetDateTime("LoadStartDate", true).Value;
            int[] internalBaiDs = base.GetIntArray("InternalBAIDs", false);

            DateTime currentDateSmallDateCutoff = DateTime.Now.SetSecond(0).SetMillisecond(0); //Only get updates prior to the start of the current minute

            if (internalBaiDs == null)
            {
                return dh => dh.DlHdrRvsnDte < currentDateSmallDateCutoff && tradeTypes.Contains(dh.DlHdrTyp.Value) && dh.DlHdrToDte >= loadStartDate;
            }
            else
            {
                return dh => dh.DlHdrRvsnDte < currentDateSmallDateCutoff && tradeTypes.Contains(dh.DlHdrTyp.Value) && dh.DlHdrToDte >= loadStartDate && internalBaiDs.Contains(dh.DlHdrIntrnlBaid);
            }
        }

        private DealHeaderEntity[] GetRaDeals(IntegrationStatus status, int batchSize, bool isResync)
        {
            IQueryable<DealHeaderEntity> dealQuery = _dhQueryable
                                            .WithPath(
                                                PrefetchUser,
                                                PrefetchDealHeaderArchive,
                                                PrefetchDealDetails,
                                                PrefetchSourceSystem,
                                                PrefetchDealHeaderGeneralConfig,
                                                PrefetchExternalBusinessAssociate
                                                )
                                            .Where(GetDealFilterExpression());
            if (isResync)
            {
                dealQuery = dealQuery.Where(dh => CounterPartySourceIdsToResync.Contains(dh.DlHdrExtrnlBaid));
            }

            if (status.MaxSyncDateTime.HasValue && status.MaxSyncDateMaxPKId.HasValue)
            {
                dealQuery = dealQuery.Where(dh => dh.DlHdrRvsnDte > status.MaxSyncDateTime || (dh.DlHdrRvsnDte == status.MaxSyncDateTime && dh.DlHdrId > status.MaxSyncDateMaxPKId.Value));
            }

            DealHeaderEntity[] raDeals = dealQuery
                                            .OrderBy(dh => dh.DlHdrRvsnDte)
                                            .ThenBy(dh => dh.DlHdrId)
                                            .Take(batchSize)
                                            .WithHint("FORCE ORDER", HintType.QueryHint)
                                            .ToArray();

            return raDeals;
        }

        public TradeIntegrationDTO TranslateEntity(DealHeaderEntity raDeal, int sourceSystemId, ILookup<Tuple<int, short>, string> dateRuleLookup)
        {
            TradeIntegrationDTO tradeDto = new TradeIntegrationDTO
            {
                SourceId = raDeal.DlHdrId,
                InternalContractNumber = raDeal.DlHdrIntrnlNbr,
                ExternalContractNumber = raDeal.DlHdrExtrnlNbr,
                EffectiveFromDateTime = raDeal.DlHdrFrmDte,
                EffectiveToDateTime = raDeal.DlHdrToDte,
                InternalCounterPartyIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raDeal.DlHdrIntrnlBaid },
                ExternalCounterPartyIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raDeal.DlHdrExtrnlBaid },
                NegotiatedTradeDateTime = raDeal.DlHdrDsplyDte,
                InternalColleagueIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raDeal.DlHdrIntrnlUser.UserCntctId },
                ExternalColleagueIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raDeal.DlHdrExtrnlCntctId },
                TradeTypeMeaning = GetTradeTypeMeaning(raDeal),
                OriginSystemNumber = raDeal.SourceNumber,
                OriginSystemName = raDeal.SourceSystem?.Name,
                IsActive = (raDeal.DlHdrStat == RAStatus.Active)
            };


            tradeDto.CustomProperties = GetGeneralConfigAttributes(raDeal.GeneralConfigurations);

            tradeDto.TradeDetailDTOs = GetTradeDetailDtOs(sourceSystemId, tradeDto.TradeTypeMeaning, raDeal.DealDetails, dateRuleLookup);

            return tradeDto;
        }


        private Dictionary<string, string> GetGeneralConfigAttributes(EntityCollection<GeneralConfigurationEntity> entityCollection)
        {
            //Documentations says entityCollection should be empty not null
            return entityCollection.ToDictionary(key => key.GnrlCnfgQlfr.ToUpper(), value => value.GnrlCnfgMulti);
        }

        private string GetTradeTypeMeaning(DealHeaderEntity raDeal)
        {
            switch (raDeal.DlHdrTyp)
            {
                case 2: return TradeType.SaleMeaning;
                case 14: return TradeType.RackSaleMeaning;
                case 1: return TradeType.PurchaseMeaning;
                case 20: return TradeType.ExchangeMeaning;
                case 100: return TradeType.BuySellMeaning;
                default: return null;
            }
        }

        private List<TradeDetailIntegrationDTO> GetTradeDetailDtOs(int sourceSystemId, string tradeTypeMeaning, EntityCollection<DealDetailEntity> entityCollection, ILookup<Tuple<int, short>, string> dateRuleLookup)
        {
            //Documentations says entityCollection should be empty not null
            return entityCollection.Select(dd => TranslateDealDetailEntity(dd, tradeTypeMeaning, sourceSystemId, dateRuleLookup)).ToList();
        }

        private TradeDetailIntegrationDTO TranslateDealDetailEntity(DealDetailEntity dd, string tradeTypeMeaning, int sourceSystemId, ILookup<Tuple<int, short>, string> dateRuleLookup)
        {
            TradeDetailIntegrationDTO tradeDetail = new TradeDetailIntegrationDTO();

            tradeDetail.DetailQuantity = Convert.ToDecimal(dd.DlDtlQntty);
            tradeDetail.DeliveryFromDateTime = dd.DlDtlFrmDte;
            tradeDetail.DeliveryToDateTime = dd.DlDtlToDte;
            tradeDetail.IncoTermIdLookup = null; //todo:figure out what to put here
            tradeDetail.ProductIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = dd.DlDtlPrdctId, SourceId2 = dd.AlternateProductNameId };
            tradeDetail.LocationIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = dd.DlDtlLcleId };
            tradeDetail.UnitOfMeasureIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = dd.DlDtlDsplyUom };
            tradeDetail.DestinationIdLookup = dd.DestinationLcleId.HasValue ? new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = dd.DestinationLcleId } : null;
            tradeDetail.SourceId = dd.DlDtlId;
            tradeDetail.OriginLocationIdLookup = dd.OriginLcleId.HasValue ? new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = dd.OriginLcleId } : null;
            tradeDetail.TradeQuantityFrequencyMeaning = GetTradeFrequencyTypeCodeValue(dd.DlDtlVlmeTrmTpe);
            tradeDetail.DeliverOrReceiveMeaning = GetDeliverOrReceiveCodeValue(dd.DlDtlSpplyDmnd);

            tradeDetail.BookIdLookup = new RelatedEntityLookup
            {
                SourceSystemId = sourceSystemId,
                SourceId = dd.DealDetailStrategies.MaxByFirst(x => x.FromDate).StrtgyId
            };

            tradeDetail.IsActive = dd.DlDtlStat == RAStatus.Active;

            tradeDetail.Terms = dd.Term?.TrmAbbrvtn ?? "";

            tradeDetail.CustomProperties = new Dictionary<string, string> { { "DateRule", dateRuleLookup[Tuple.Create(dd.DealHeader.DlHdrId, dd.DlDtlId)]?.FirstOrDefault() } };

            tradeDetail.TradeQuantityDTOs = GetTradeQuantityDtOs(sourceSystemId, dd.Obligations);

            tradeDetail.TradePriceDTOs = GetTradePriceDtOs(sourceSystemId, tradeTypeMeaning, dd.DealDetailProvisions);

            tradeDetail.CustomDatabaseValues.Add("TransportMethod", dd.DlDtlMthdTrnsprttn);
            tradeDetail.CustomDatabaseValues.Add("LocationType", dd.Locale?.LocaleType?.LcleTpeId.ToString());
            tradeDetail.CustomDatabaseValues.Add("HasPrimaryBAOwner", dd.DealHeader.ExternalBusinessAssociate.GeneralConfigurations.Any(gc => gc.GnrlCnfgTblNme == "BusinessAssociate" && gc.GnrlCnfgQlfr == "PrimaryBusinessOwner").ToString());
            tradeDetail.CustomDatabaseValues.Add("HasCorrectParentLocaleNameEnding", dd.Locale?.VCustomPCRHByChildLocale.Any(x => !x.ParentLocale.LcleNme.EndsWith("Direct Ship")).ToString());
            return tradeDetail;
        }

        private string GetDeliverOrReceiveCodeValue(string dlDtlSpplyDmnd)
        {
            switch (dlDtlSpplyDmnd)
            {
                case "R": return DeliverOrReceive.ReceiveMeaning;
                default: return DeliverOrReceive.DeliverMeaning;
            }
        }

        private List<TradeQuantityIntegrationDTO> GetTradeQuantityDtOs(int sourceSystemId, EntityCollection<ObligationEntity> entityCollection)
        {
            //Documentations says entityCollection should be empty not null
            return entityCollection.Select(ob => TranslateObligationEntity(ob, sourceSystemId)).ToList();
        }

        private TradeQuantityIntegrationDTO TranslateObligationEntity(ObligationEntity ob, int sourceSystemId)
        {
            TradeQuantityIntegrationDTO tradeQuantity = new TradeQuantityIntegrationDTO();

            var targetUomId = base.GetInt("TradeQuantityUnitOfMeasureIdOverride", false) ?? SourceSystemUomId;
            var convertedQuantity = UomConversionCache.Instance.ConvertValue(Convert.ToDecimal(ob.ObQty), SourceSystemUomId, targetUomId, null);
            //log.Info("ConvertedQuantity: " + convertedQuantity);

            tradeQuantity.SourceId = ob.ObId;
            tradeQuantity.ContractQuantity = convertedQuantity;
            tradeQuantity.ScheduledQuantity = convertedQuantity;
            tradeQuantity.FromDateTime = ob.ObFrmDte;
            tradeQuantity.ToDateTime = ob.ObToDte;
            tradeQuantity.UnitOfMeasureIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = targetUomId };

            tradeQuantity.IsActive = (ob.ObStts == RAStatus.Active);
            tradeQuantity.CustomDatabaseValues = new Dictionary<string, string>
            {
                    {
                            "PlannedTransferTotalQuantity", ((decimal)ob.PlannedTransfers
                                                                .Where(pt => new[] {"A", "C"}.Contains(pt.Status))
                                                                .Sum(pt => pt.PlnndTrnsfrTtlQty)).ToString()
                    }
            };


            return tradeQuantity;
        }

        private List<TradePriceIntegrationDTO> GetTradePriceDtOs(int sourceSystemId, string tradeTypeMeaning, EntityCollection<DealDetailProvisionEntity> entityCollection)
        {
            //Documentations says entityCollection should be empty not null
            var entityCollectionList = entityCollection.Where(ddp => ddp.Actual != "I" && ddp.Status == "A").Select(ddp=> ddp.Prvsn.PrvsnNme).ToList();

            return entityCollection.Where(ddp => ddp.Actual != "I" && ddp.Status == "A").Select(ddp => TranslateDealDetailProvisionEntity(ddp, tradeTypeMeaning, sourceSystemId)).ToList();
        }

        private TradePriceIntegrationDTO TranslateDealDetailProvisionEntity(DealDetailProvisionEntity ddp, string tradeType, int sourceSystemId)
        {
           
                TradePriceIntegrationDTO tradePrice = new TradePriceIntegrationDTO();

                tradePrice.SourceId = ddp.DlDtlPrvsnId;
                if(ddp.Prvsn.PrvsnNme.Contains("FixedPrice"))
                {
                    try
                    {
                        tradePrice.FixedPrice = Convert.ToDecimal(ddp.DealDetailProvisionRows.FirstOrDefault().PriceAttribute1);
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Error translating DealDetailProvisionEntity", ex);
                        tradePrice.FixedPrice = null;
                    }
                       
                    
                }
                else
                {
                    tradePrice.FixedPrice = null;
                }
                //tradePrice.FixedPrice = ddp.Prvsn.PrvsnNme.Contains("FixedPrice") ? (decimal?)Convert.ToDecimal(ddp.DealDetailProvisionRows.FirstOrDefault().PriceAttribute1) : null;
                tradePrice.PriceDescription = GetPriceDescription(ddp, tradeType);
                tradePrice.PriceCategory = ddp.Prvsn.PrvsnNme;
                tradePrice.FromDateTime = ddp.DlDtlPrvsnFrmDte;
                tradePrice.ToDateTime = ddp.DlDtlPrvsnToDte;
                tradePrice.IsProductPrice = ddp.CostType == "P" ? true : false;
                tradePrice.UnitOfMeasureIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = ddp.Uomid };
                tradePrice.CurrencyIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = ddp.CrrncyId };

                tradePrice.IsActive = (ddp.Status == RAStatus.Active);

                return tradePrice;
            
            
           
        }

        private string GetPriceDescription(DealDetailProvisionEntity ddp, string tradeTypeMeaning)
        {
            var description = ddp.DealDetailProvisionRows.FirstOrDefault(ddpr => ddpr.DlDtlPrvsnRwTpe == "A" && !String.IsNullOrWhiteSpace(ddpr.RowText))?.RowText ?? "";

            return description;
        }

        private string GetTradeFrequencyTypeCodeValue(string dlDtlVlmeTrmTpe)
        {
            switch (dlDtlVlmeTrmTpe)
            {
                case "M": return TradeQuantityFrequencyType.PerMonthMeaning;
                case "C": return TradeQuantityFrequencyType.PerTradeMeaning;
                case "D": return TradeQuantityFrequencyType.PerDayMeaning;
                case "P": return TradeQuantityFrequencyType.PerCycleMeaning;
                default: return null;
            }
        }

        public IPathEdge PrefetchDealHeaderGeneralConfig => GetGeneralConfigPrefetches(DealHeaderEntity.PrefetchPathGeneralConfigurations, "DealHeader");

        public IPathEdge PrefetchDealHeaderArchive => new PathEdge<DealHeaderArchiveEntity>(DealHeaderEntity.PrefetchPathDealHeaderArchives, DealHeaderArchiveFilter);

        /// <summary>
        /// Prefetch the Deal Header Archive with the most recent print date where the Deal Header Archive: 
        ///     Has a print date 
        ///     Has an Archived Document
        /// </summary>
        private Expression<Func<DealHeaderArchiveEntity, bool>> DealHeaderArchiveFilter => dha => dha.PrintDate.HasValue && dha.DmtDocument != null;



        public IPathEdge PrefetchUser => new PathEdge<UserEntity>(DealHeaderEntity.PrefetchPathDlHdrIntrnlUser);
        public IPathEdge PrefetchExternalBusinessAssociate => new PathEdge<BusinessAssociateEntity>(DealHeaderEntity.PrefetchPathExternalBusinessAssociate, PrefetchDealHeaderExternalBAGeneralConfig);
        public IPathEdge PrefetchDealDetails => new PathEdge<DealDetailEntity>(DealHeaderEntity.PrefetchPathDealDetails, PrefetchObligations, PrefetchDealDetailProvisions, PrefetchDealDetailGeneralConfig, PrefetchTerm, PrefetchStrategyHeader, PrefetchLocations);
        public IPathEdge PrefetchObligations => new PathEdge<ObligationEntity>(DealDetailEntity.PrefetchPathObligations, PrefetchPlannedTransfer);
        public IPathEdge PrefetchPcrh => new PathEdge<VCustomProfitCenterRollupHierarchyEntity>(LocaleEntity.PrefetchPathVCustomPCRHByChildLocale, PrefetchPcrhLocale);
        public IPathEdge PrefetchPcrhLocale => new PathEdge<LocaleEntity>(VCustomProfitCenterRollupHierarchyEntity.PrefetchPathParentLocale);
        public IPathEdge PrefetchLocations => new PathEdge<LocaleEntity>(DealDetailEntity.PrefetchPathLocale, PrefetchLocaleType, PrefetchPcrh);
        public IPathEdge PrefetchLocaleType => new PathEdge<LocaleTypeEntity>(LocaleEntity.PrefetchPathLocaleType);
        public IPathEdge PrefetchPlannedTransfer => new PathEdge<PlannedTransferEntity>(ObligationEntity.PrefetchPathPlannedTransfers);
        public IPathEdge PrefetchTerm => new PathEdge<TermEntity>(DealDetailEntity.PrefetchPathTerm);
        public IPathEdge PrefetchStrategyHeader => new PathEdge<DealDetailStrategyEntity>(DealDetailEntity.PrefetchPathDealDetailStrategies,
            new PathEdge<StrategyHeaderEntity>(DealDetailStrategyEntity.PrefetchPathStrategyHeader));
        public IPathEdge PrefetchDealDetailGeneralConfig => GetGeneralConfigPrefetches(DealDetailEntity.PrefetchPathGeneralConfigurations, "DealDetail");
        public IPathEdge PrefetchDealHeaderExternalBAGeneralConfig => GetGeneralConfigPrefetches(BusinessAssociateEntity.PrefetchPathGeneralConfigurations, "BusinessAssociate");
        public IPathEdge PrefetchDealDetailProvisions
        {
            get
            {
                return new PathEdge<DealDetailProvisionEntity>(
                                DealDetailEntity.PrefetchPathDealDetailProvisions,
                                dd => dd.Actual != "I" && dd.Status == "A",
                                PrefetchPrvsn,
                                PrefetchDealDetailProvisionRow
                                );
            }
        }

        public IPathEdge PrefetchPrvsn => new PathEdge<PrvsnEntity>(DealDetailProvisionEntity.PrefetchPathPrvsn);

        public IPathEdge PrefetchSourceSystem => new PathEdge<SourceSystemEntity>(DealHeaderEntity.PrefetchPathSourceSystem);

        //can't seem to limit this to just the Global ones of the configured provisions.(doesn't want to convert the .Contains)
        public IPathEdge PrefetchDealDetailProvisionRow => new PathEdge<DealDetailProvisionRowEntity>(DealDetailProvisionEntity.PrefetchPathDealDetailProvisionRows);
    }
}
