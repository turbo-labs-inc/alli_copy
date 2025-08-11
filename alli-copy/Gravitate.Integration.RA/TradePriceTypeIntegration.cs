using Castle.Components.DictionaryAdapter.Xml;
using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using MoreLinq;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class TradePriceTypeIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TradePriceTypeIntegration));

        private readonly IQueryable<PrvsnEntity> _tradePriceTypeQueryable;
        private readonly IQueryable<VrblePrvsnEntity> _vrblePrvsnQueryable;
        private readonly ITradePriceTypeIntegrationService _tradePriceTypeIntegrationService;
        private Dictionary<int, VrblePrvsnEntity> _vrblePrvsnDictionary;


        public TradePriceTypeIntegration(IQueryable<PrvsnEntity> tradePriceTypeQueryable, IQueryable<VrblePrvsnEntity> vrblePrvsnQueryable, ITradePriceTypeIntegrationService tradePriceTypeIntegrationService)
        {
            _tradePriceTypeQueryable = tradePriceTypeQueryable;
            _vrblePrvsnQueryable = vrblePrvsnQueryable;
            _tradePriceTypeIntegrationService = tradePriceTypeIntegrationService;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            _vrblePrvsnDictionary = _vrblePrvsnQueryable.Where(p => p.ColumnName != null).DistinctBy(p => p.VrblePrvsnPrvsnId).ToDictionary(key => key.VrblePrvsnPrvsnId);
            Stopwatch sw = Stopwatch.StartNew();

            var one = new PathEdge<PrvsnValidTemplateEntity>(PrvsnEntity.PrefetchPathPrvsnValidTemplates);
            var two = new PathEdge<PrvsnValidEntityTemplateEntity>(PrvsnEntity.PrefetchPathPrvsnValidEntityTemplates);

            PrvsnEntity[] raTradePriceTypes =
                _tradePriceTypeQueryable
                    .WithPath(
                        new PathEdge<PrvsnValidTemplateEntity>(
                            PrvsnEntity.PrefetchPathPrvsnValidTemplates),
                        new PathEdge<PrvsnValidEntityTemplateEntity>(
                            PrvsnEntity.PrefetchPathPrvsnValidEntityTemplates))
                    .ToArray();

            Log.Info($"RA provision query took {sw.Elapsed.ToString()} for {raTradePriceTypes.Count()} provision");

            TradePriceTypeIntegrationDTO[] tradePriceTypeDtOs = raTradePriceTypes.Select(rap => TranslateEntity(rap)).ToArray();

            Log.Info("Calling BulkSyncTradePriceTypes");
            _tradePriceTypeIntegrationService.BulkSyncTradePriceTypes(sourceSystemId, tradePriceTypeDtOs);
            Log.Info("Completed call to BulkSyncTradePriceTypes");
        }

        public TradePriceTypeIntegrationDTO TranslateEntity(PrvsnEntity entity)
        {
            TradePriceTypeIntegrationDTO dto = new TradePriceTypeIntegrationDTO
            {
                Name = entity.PrvsnNme,
                DisplayName = entity.PrvsnDscrptn,
                IsPrimary = entity.PrvsnValidTemplates.Any(pvt => pvt.DefaultCostType.Trim() == "P") || entity.PrvsnValidEntityTemplates.Any(pvt => pvt.DefaultCostType.Trim() == "P"),
                IsIndexBased = entity.PrvsnExprssn.Contains("FormulaTablet") || entity.Comments.Contains("FormulaTablet"),
                PriceSettlementFrequencyTypeMeaning = GetPriceSettlementFrequencyTypeMeaning(entity),
                IsFlat = entity.IsFlatFee.Trim() == "Y",
                HasPrice = _vrblePrvsnDictionary.ContainsKey(entity.PrvsnId),
                IsActive = entity.PrvsnStat.Trim() != "I",
                SourceId = entity.PrvsnId


            };

            return dto;
        }

        //Custom Alliance Wholesale Function to apply FrequencyTypeMeaning
        private string GetPriceSettlementFrequencyTypeMeaning(PrvsnEntity entity)
        {
            var prvName = entity.PrvsnNme;
            if (entity.PrvsnEvlteDteTpe != "T")
            {
                return null;
            }

            var settings = ApplicationSettingsManager.GetAppSettingJSON<Dictionary<string, string>>(
                "Gravitate.Integration.RA.TradePriceTypeIntegration.PriceSettlementFrequencyTypes");

            if (settings.ContainsKey(prvName))
            {
                return CodeSetManager.GetCodeValue("PriceSettlementFrequencyType", settings[prvName]).Meaning;
            }

            return CodeSetFacade.Instance.PriceSettlementFrequencyType.PerMonth.Meaning;
        }
    }
}
