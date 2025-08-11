using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Integration.BOLDetailMatchingRules.Abstract;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.ServiceModel.Channels;

namespace Gravitate.Integration.RA
{
    public class BillOfLadingIntegration : BaseIncrementalIntegration
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(BillOfLadingIntegration));
        private const int SourceSystemUomId = 3;

        private readonly IQueryable<TransactionHeaderEntity> _thQueryable;
        private IQueryable<vUOMConversionEntity> _vUomConversionQueryable;
        private readonly IBillOfLadingIntegrationService _bolIntegrationService;
        private IQueryable<LocaleEntity> _localeQueryable;
        private readonly Dictionary<int, string> _terminalIDictionary = new Dictionary<int, string>();

        private int[] BoxPdfInternalBAs => GetIntArray("BoxPdfInternalBAs", false);
        private int[] BoxPdfSourceSystems => GetIntArray("BoxPdfSourceSystems", false);
        private String[] BoxPdfMovementTypes => GetStringArray("BoxPdfMovementTypes", false);
        private String[] AllowedMovementTypes => GetStringArray("MovementTypes", false);



        public BillOfLadingIntegration(
            IQueryable<TransactionHeaderEntity> thQueryable,
            IQueryable<vUOMConversionEntity> vUomConversionQueryable,
            IBillOfLadingIntegrationService bolIntegrationService, IQueryable<LocaleEntity> localeQueryable)
        {
            _thQueryable = thQueryable;
            _vUomConversionQueryable = vUomConversionQueryable;
            _bolIntegrationService = bolIntegrationService;
            _localeQueryable = localeQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            int batchSize = base.GetInt("BatchSize", true).Value;
            int resultCount = 0;

            Resync(sourceSystemId, batchSize);

            do
            {
                resultCount = ProcessBatch(sourceSystemId, status, batchSize, resultCount, false);

            } while (resultCount == batchSize);
        }

        private int ProcessBatch(int sourceSystemId, IntegrationStatus status, int batchSize, int resultCount, bool isResync)
        {
            BillOfLadingIntegrationDTO[] raMovements = GetRaMovements(sourceSystemId, status, batchSize, out resultCount, isResync);
            _bolIntegrationService.CreateOrUpdateBOLs(sourceSystemId, raMovements);
            return resultCount;
        }


        private void Resync(int sourceSystemId, int batchSize)
        {
            int resultCount = 0;
            IntegrationStatus status = new IntegrationStatus();

            if (base.CounterPartySourceIdsToResync.Any(false))
            {
                _log.Info($"Resyncing BOLs for BAIDs: {String.Join(", ", base.CounterPartySourceIdsToResync)}");
                do
                {
                    resultCount = ProcessBatch(sourceSystemId, status, batchSize, resultCount, true);

                } while (resultCount == batchSize);
            }
        }

        public Expression<Func<TransactionHeaderEntity, bool>> GetTransHeaderFilterExpression()
        {
            int[] tradeTypes = base.GetIntArray("TradeTypes", true);
            DateTime loadStartDate = base.GetDateTime("LoadStartDate", true).Value;
            int[] internalBaiDs = base.GetIntArray("InternalBAIDs", false);

            DateTime currentDateSmallDateCutoff = DateTime.Now.SetSecond(0).SetMillisecond(0); //Only get updates prior to the start of the current minute

            if (internalBaiDs == null)
            {
                return th =>
                    (th.XhdrDte >= th.MovementHeader.ChangeDate ? th.XhdrDte : th.MovementHeader.ChangeDate) < currentDateSmallDateCutoff
                    && tradeTypes.Contains(th.PlannedTransfer.DealHeader.DlHdrTyp.Value)
                    && AllowedMovementTypes.Contains(th.MovementHeader.MvtHdrTyp)
                    && th.PlannedTransfer.DealHeader.DlHdrToDte >= loadStartDate;
            }
            else
            {
                return th =>
                    (th.XhdrDte >= th.MovementHeader.ChangeDate ? th.XhdrDte : th.MovementHeader.ChangeDate) < currentDateSmallDateCutoff
                    && tradeTypes.Contains(th.PlannedTransfer.DealHeader.DlHdrTyp.Value)
                    && th.PlannedTransfer.DealHeader.DlHdrToDte >= loadStartDate
                    && AllowedMovementTypes.Contains(th.MovementHeader.MvtHdrTyp)
                    && internalBaiDs.Contains(th.PlannedTransfer.DealHeader.DlHdrIntrnlBaid);
            }
        }

        public BillOfLadingIntegrationDTO[] GetRaMovements(int sourceSystemId, IntegrationStatus status, int batchSize, out int resultCount, bool isResync)
        {
            IQueryable<TransactionHeaderEntity> thQuery = _thQueryable
                                                                .Where(GetTransHeaderFilterExpression());
            if (isResync)
            {
                thQuery = thQuery.Where(th => CounterPartySourceIdsToResync.Contains(th.PlannedTransfer.DealHeader.DlHdrExtrnlBaid));
            }

            if (status.MaxSyncDateTime.HasValue && status.MaxSyncDateMaxPKId.HasValue)
            {
                thQuery = thQuery.Where(th => (th.XhdrDte >= th.MovementHeader.ChangeDate ? th.XhdrDte : th.MovementHeader.ChangeDate) > status.MaxSyncDateTime || ((th.XhdrDte >= th.MovementHeader.ChangeDate ? th.XhdrDte : th.MovementHeader.ChangeDate) == status.MaxSyncDateTime && th.XhdrId > status.MaxSyncDateMaxPKId.Value));
            }

            //Ignore movements prior to 10-1-23
            thQuery = thQuery.Where(th => th.MovementDate >= new DateTime(2023, 10, 1));            

            int ? uomOverride = base.GetInt("BillOfLadingDetailUnitOfMeasureIdOverride", false);

            Stopwatch sw = Stopwatch.StartNew();
            var tempResults = thQuery.Select(th => new
            {
                MvtDcmntDte = th.MovementDocument.MvtDcmntDte,
                MvtHdrDte = th.MovementHeader.MvtHdrDte,
                DestinationLocationSourceId = th.MovementHeader.MvtHdrDstntnLcleId,
                TradeSourceId = th.PlannedTransfer.PlnndTrnsfrObDlDtlDlHdrId,
                TradeDetailSourceId = th.PlannedTransfer.PlnndTrnsfrObDlDtlId,
                TradeQuantitySourceId = th.PlannedTransfer.PlnndTrnsfrObId,
                LocationSourceId = th.MovementHeader.MvtHdrLcleId,
                OriginLocationSourceId = th.MovementHeader.MvtHdrOrgnLcleId,
                ProductSourceId = th.MovementHeader.MvtHdrPrdctId,
                UnitOfMeasureSourceId = uomOverride ?? th.MovementHeader.MvtHdrDsplyUom,
                XHdrId = th.XhdrId,
                // if any primary pricing provisions have type N, then use net, otherwise use gross
                Quantity = th.XhdrQty,
                QuantityAmbientTemp = th.XhdrGrssQty,
                QuantityStandardTemp = th.XhdrQty,
                Density = th.MovementHeader.Density,
                SpecificGravity = th.MovementHeader.MvtHdrGrvty,
                CarrierCounterPartySourceId = th.MovementHeader.MvtHdrCrrrBaid,
                LiftingNumber = th.MovementHeader.MvtHdrLftngNmbr,
                IsActive = th.XhdrStat == "C" ? true : false,
                MvtDcmntID = th.MovementHeader.MvtHdrMvtDcmntId,
                MvtHdrID = th.MovementHeader.MvtHdrId,
                BOLNumber = th.MovementHeader.MovementDocument.MvtDcmntExtrnlDcmntNbr,
                CounterPartySourceId = th.PlannedTransfer.DealHeader.DlHdrExtrnlBaid,
                MethodOfTransportation = th.MovementHeader.MvtHdrTyp,
                MaxChangeDate = th.XhdrDte >= th.MovementHeader.ChangeDate ? th.XhdrDte : th.MovementHeader.ChangeDate,
                VehicleInitial = string.Empty,
                VehicleNumber = th.MovementHeader.Text3,
                // global specific
                RASourceSystemId = th.MovementDocument.SrceSystmId,
                InternalBA = th.PlannedTransfer.DealHeader.DlHdrIntrnlBaid
            })
                .OrderBy(th => th.MaxChangeDate)
            .ThenBy(th => th.XHdrId)
            .Take(batchSize)
            .ToList();
            _log.Info($"RA BOL query took {sw.Elapsed} for {tempResults.Count} records");

            resultCount = tempResults.Count();
            DateTime? maxSyncDateTime = tempResults.Any() ? tempResults.Max(d => d.MaxChangeDate) : status.MaxSyncDateTime;
            int? maxPkId = tempResults.Any() ? tempResults.Where(d => d.MaxChangeDate == maxSyncDateTime).Max(d => d.XHdrId) : status.MaxSyncDateMaxPKId;
            _log.Info($"Acquired batch of {resultCount} bols to process. Batch MaxPkId: {maxPkId}, MaxSyncDateTime: {maxSyncDateTime}");

            // This Where clause is specific to NGL, because they removed some Locales. Gotta make sure the Locale exists!
            var localeDictionary = _localeQueryable.ToDictionary(key => key.LcleId);
            tempResults = tempResults.Where(x => localeDictionary.ContainsKey(x.DestinationLocationSourceId ?? -1)).ToList();

            status.MaxSyncDateTime = maxSyncDateTime;
            status.MaxSyncDateMaxPKId = maxPkId;

            var results = new List<BillOfLadingIntegrationDTO>();
            var groups = tempResults.GroupBy(row => new
            {
                row.BOLNumber,
                row.CounterPartySourceId,
                row.UnitOfMeasureSourceId,
                row.MvtHdrDte,
                row.MvtDcmntID,
                row.MethodOfTransportation
            });

            foreach (var bol in groups)
            {
                var dto = new BillOfLadingIntegrationDTO
                {
                    BOLNumber = bol.Key.BOLNumber,
                    CounterPartyIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = bol.Key.CounterPartySourceId },
                    UnitOfMeasureIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = bol.Key.UnitOfMeasureSourceId },
                    CreatedDateTime = bol.Key.MvtHdrDte,
                    IsActive = true,
                    SourceId = bol.Key.MvtDcmntID,
                    MethodOfTransportationMeaning = GetMethodOfTransportationCodeValue(bol.Key.MethodOfTransportation),
                    CustomDatabaseValues = new Dictionary<string, string>(),
                    BillOfLadingDetailDTOs = bol.Select(bold => new BillOfLadingDetailIntegrationDTO()
                    {
                        TradeQuantityMatchRule = BOLDetailMatchRuleType.ByQuantityDetailAndTradeSourceIds,
                        BOLDateTime = bold.MvtHdrDte,
                        DestinationLocationIdLookup = bold.DestinationLocationSourceId.HasValue ? new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = bold.DestinationLocationSourceId } : null,
                        TradeQuantityIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = bold.TradeQuantitySourceId, SourceId2 = bold.TradeDetailSourceId, SourceId3 = bold.TradeSourceId },
                        LocationIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = bold.LocationSourceId },
                        OriginLocationIdLookup = bold.OriginLocationSourceId.HasValue ? new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = bold.OriginLocationSourceId } : null,
                        ProductIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = bold.ProductSourceId, SourceId2 = GetAlternateProductSourceId(bold.ProductSourceId) },
                        UnitOfMeasureIdLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = bold.UnitOfMeasureSourceId },
                        Quantity = ConvertQuantityToDisplayUom(bold.Quantity, bold.UnitOfMeasureSourceId, bold.SpecificGravity).Value,
                        QuantityAmbientTemp = ConvertQuantityToDisplayUom(bold.QuantityAmbientTemp, bold.UnitOfMeasureSourceId, bold.SpecificGravity),
                        QuantityStandardTemp = ConvertQuantityToDisplayUom(bold.QuantityStandardTemp, bold.UnitOfMeasureSourceId, bold.SpecificGravity),
                        Density = bold.Density.HasValue ? Convert.ToDecimal(bold.Density) : (decimal?)null,
                        SpecificGravity = Convert.ToDecimal(bold.SpecificGravity),
                        CarrierCounterPartyIdLookup = bold.CarrierCounterPartySourceId.HasValue ? new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = bold.CarrierCounterPartySourceId } : null,
                        LiftingNumber = bold.LiftingNumber,
                        VehicleInitial = bold.VehicleInitial,
                        VehicleNumber = bold.VehicleNumber,
                        IsActive = bold.IsActive,
                        SourceId = bold.XHdrId
                    }).ToList(),
                };


                results.Add(dto);


            }


            return results.ToArray();
        }

        private decimal? ConvertQuantityToDisplayUom(double? quantity, int unitOfMeasureSourceId, double specificGravity)
        {
            if (!quantity.HasValue)
            {
                return null;
            }

            decimal sg = Convert.ToDecimal(specificGravity);
            decimal q = Convert.ToDecimal(quantity.Value);

            return UomConversionCache.Instance.ConvertValue(q, SourceSystemUomId, unitOfMeasureSourceId, sg);
        }

        private string GetMethodOfTransportationCodeValue(string methodOfTransportation)
        {
            var mot = CodeSetFacade.Instance.MethodOfTransportation.GetByMeaning(methodOfTransportation.Trim());

            if (mot == null)
            {
                _log.Warn("Unable to find MethodOfTransportation: " + methodOfTransportation.Trim());
            }

            return mot?.Meaning;
        }
    }
}
