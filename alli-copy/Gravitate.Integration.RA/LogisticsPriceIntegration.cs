using Custom.Gravitate.WCF.DataServices.Interfaces;
using Custom.Gravitate.WCF.DTO;
using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using log4net;
using SD.LLBLGen.Pro.LinqSupportClasses;
using SD.LLBLGen.Pro.ORMSupportClasses;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class LogisticsPriceIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LogisticsPriceIntegration));

        private readonly IQueryable<VCustomGravitateLogisticsPriceEntity> _raLogisticsPriceQueryable;
        private readonly ILogisticsPriceService _logisticsPriceService;


        public LogisticsPriceIntegration(IQueryable<VCustomGravitateLogisticsPriceEntity> raLogisticsPriceQueryable, ILogisticsPriceService logisticsPriceService)
        {
            _raLogisticsPriceQueryable = raLogisticsPriceQueryable;
            _logisticsPriceService = logisticsPriceService;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
                       
            
            VCustomGravitateLogisticsPriceEntity[] raLogisticsPrices = null;
            raLogisticsPrices = _raLogisticsPriceQueryable.WithHint("FORCE ORDER", HintType.QueryHint).ToArray(); 

            Log.Info($"RA VCustomGravitateLogisticsPriceEntity query took {sw.Elapsed.ToString()} for {raLogisticsPrices.Count()} records");
            LogisticsPriceDTO[] logisticsPriceDtos = raLogisticsPrices.Select(x => TranslateEntity(x, sourceSystemId)).ToArray();

            Log.Info($"Calling {nameof(ILogisticsPriceService.CreateOrDeleteLogisticsPrices)}");
            _logisticsPriceService.CreateOrDeleteLogisticsPrices(sourceSystemId, logisticsPriceDtos);
            Log.Info($"Completed call to {nameof(ILogisticsPriceService.CreateOrDeleteLogisticsPrices)}");
            
        }

        public LogisticsPriceDTO TranslateEntity(VCustomGravitateLogisticsPriceEntity raLogisticsPrice, int sourceSystemId)
        {
            LogisticsPriceDTO lpDto = new LogisticsPriceDTO
            {
                FromLocationLookup = raLogisticsPrice.FromLocationSourceId.HasValue ? new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raLogisticsPrice.FromLocationSourceId } : null,
                ToLocationLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raLogisticsPrice.ToLocationSourceId },
                PriceInstrumentLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raLogisticsPrice.PriceInstrumentSourceId },
                CounterPartyLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raLogisticsPrice.DlHdrExtrnlBaid },
                ProductLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raLogisticsPrice.DlDtlPrdctId, SourceId2 = GetAlternateProductSourceId(raLogisticsPrice.DlDtlPrdctId) },
                MethodOfTransportationMeaning = CodeSetFacade.Instance.MethodOfTransportation.GetByMeaning(raLogisticsPrice.MethodOfTransportationMeaning)?.Meaning,
                InternalContractNumber = raLogisticsPrice.InternalContractNumber,
                EffectiveFromDateTime = raLogisticsPrice.FromDate,
                EffectiveToDateTime = raLogisticsPrice.ToDate,
                IsAutoRenewEnabled = raLogisticsPrice.IsAutoRenewEnabled.HasValue ? raLogisticsPrice.IsAutoRenewEnabled.Value : false,
                IsFlatPrice = raLogisticsPrice.IsFlatPrice.HasValue ? raLogisticsPrice.IsFlatPrice.Value : false,
            };
            return lpDto;
        }

    }

}
