using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class TradePriceValuationRuleIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TradePriceValuationRuleIntegration));
        private readonly ITradePriceValuationRuleIntegrationService _tradePriceValuationRuleIntegrationService;
        private readonly IQueryable<FormulaTabletRuleEntity> _formulaTabletRuleQueryable;


        public TradePriceValuationRuleIntegration(ITradePriceValuationRuleIntegrationService tradePriceValuationRuleIntegrationService, IQueryable<FormulaTabletRuleEntity> formulaTabletRuleQueryable)
        {
            _tradePriceValuationRuleIntegrationService = tradePriceValuationRuleIntegrationService;
            _formulaTabletRuleQueryable = formulaTabletRuleQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();

            var raEntities = _formulaTabletRuleQueryable.Where(ftr => ftr.ProvisionType == "Movement" || ftr.ProvisionType == "AllProvisionTypes").ToArray();

            Log.Info($"RA FormulaTabletRule query took {sw.Elapsed.ToString()} for {raEntities.Count()} provision");

            TradePriceValuationRuleIntegrationDTO[] dtos = raEntities.Select(rap => TranslateEntity(rap)).ToArray();

            Log.Info("Calling BulkSyncPriceTypeValuationRules");
            _tradePriceValuationRuleIntegrationService.BulkSyncPriceTypeValuationRules(sourceSystemId, dtos);
            Log.Info("Completed call to BulkSyncPriceTypeValuationRules");
        }

        public TradePriceValuationRuleIntegrationDTO TranslateEntity(FormulaTabletRuleEntity entity)
        {
            return new TradePriceValuationRuleIntegrationDTO
            {
                Name = entity.Name,
                SourceId = entity.FrmlaTbltRleId,
                IsActive = (entity.Status == "A")
            };
        }
    }
}
