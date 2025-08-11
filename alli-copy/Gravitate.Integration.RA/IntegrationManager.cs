using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.Windsor;
using Gravitate.Library;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class IntegrationManager
    {
        private static ILog log = LogManager.GetLogger(typeof(IntegrationManager));
        private List<string> executionSummary;

        private IIntegrationStatusService integrationStatusService = null;

        private int primarySourceSystemId;

        public IDictionary<string, string> SettingOverrides { get; set; } = new Dictionary<string, string>();
        public DateTime StartDateTime { get; set; }

        public IntegrationManager(IIntegrationStatusService integrationStatusService)
        {
            this.integrationStatusService = integrationStatusService;

            this.primarySourceSystemId = GetSourceSystemId("RightAngle");
            this.executionSummary = new List<string>();
        }

        public static Type[] BulkIntegrations => new Type[]
        {
                typeof(CodeSetIntegration),
                typeof(CurrencyIntegration),
                typeof(UnitOfMeasureIntegration),
                typeof(ProductIntegration),
                typeof(CounterPartyIntegration),
                typeof(ColleagueIntegration),
                typeof(BookIntegration),
                typeof(LocationIntegration),
                typeof(CounterPartyLocationIntegration),
                typeof(PriceServiceSetupIntegration),
                typeof(TradePriceTypeIntegration),
                typeof(TradePriceValuationRuleIntegration),
                typeof(PaymentTermIntegration),
                typeof(CalendarIntegration),
                typeof(CodeSetIntegration),
                typeof(LogisticsPriceIntegration),
                typeof(ProductLocationMappingIntegration)
                
        };

        public static Type[] IncrementalIntegrations => new Type[]
        {
                typeof(TradeIntegration),
                typeof(BillOfLadingIntegration),
                typeof(InvoiceIntegration),
                typeof(CustomInvoiceIntegration),
                typeof(EftIntegration),
                typeof(PriceIntegration),
                typeof(InvoiceDocumentIntegration),
                typeof(TradeDocumentIntegration)
                

        };

        public static Type[] AllIntegrations => BulkIntegrations.Concat(IncrementalIntegrations).Distinct().ToArray();

        public void Execute()
        {
            StartDateTime = DateTime.Now;
            Stopwatch sw = Stopwatch.StartNew();

            ExecuteBulkIntegrations();

            integrationStatusService.ExpireAllMemoryCaches();

            ExecuteIncrementalIntegrations();

            integrationStatusService.TrackIntegrationsCompleted(primarySourceSystemId, StartDateTime, DateTime.Now);

            log.Info($"Extract completed successfully in {sw.Elapsed.ToString()}ms. Summary:\n{String.Join("\n", executionSummary)}");
        }

        private int GetSourceSystemId(string sourceSystemName)
        {
            return integrationStatusService.GetSourceSystemIdByName(sourceSystemName);
        }

        private void ExecuteBulkIntegrations()
        {

            BulkIntegrations.Select(x => new Tuple<Type, int>(x, primarySourceSystemId)).ForEach(t => ExecuteIntegration(t.Item1, t.Item2));
        }



        public BaseIntegration ResolveIntegration(Type integrationType)
        {
            var integration = (BaseIntegration)IntegrationContainer.Instance.Resolve(integrationType);

            integration.PrimarySourceSystemId = this.primarySourceSystemId;
            integration.SettingOverrides = this.SettingOverrides;

            return integration;
        }

        private void ExecuteIncrementalIntegrations()
        {
            var incrementalIntegrations = IncrementalIntegrations;

            var counterPartySourceIdsToResync = integrationStatusService.GetCounterPartySourceIdsToResync(primarySourceSystemId);

            var now = DateTime.Now;

            foreach (var integrationType in incrementalIntegrations)
            {
                var integration = (BaseIncrementalIntegration)ResolveIntegration(integrationType);

                integration.IncrementalIntegrationsStartDateTime = now;
                integration.IntegrationStartDateTime = StartDateTime;

                try
                {
                    integration.CounterPartySourceIdsToResync = counterPartySourceIdsToResync;
                    ExecuteIncrementalIntegration(integration, primarySourceSystemId);
                }
                finally
                {
                    IntegrationContainer.Instance.Release(integration);
                }

            }

            integrationStatusService.ResetResyncFlag(primarySourceSystemId, counterPartySourceIdsToResync);
        }


        private void ExecuteIncrementalIntegration(BaseIncrementalIntegration incrementalIntegrationComponent, int sourceSystemId)
        {
            ExecuteIntegration(incrementalIntegrationComponent, sourceSystemId);
        }

        private void ExecuteIntegration(Type integrationType, int sourceSystemId)
        {
            var integration = ResolveIntegration(integrationType);
            integration.IntegrationStartDateTime = StartDateTime;

            try
            {
                ExecuteIntegration(integration, sourceSystemId);
            }
            finally
            {
                IntegrationContainer.Instance.Release(integration);
            }
        }

        private void ExecuteIntegration(BaseIntegration integration, int sourceSystemId)
        {
            string integrationName = GetIntegrationName(integration);
            if (integration.IsEnabled())
            {
                try
                {
                    integration.SetCommandTimeouts();
                    Stopwatch sw = new Stopwatch();
                    LogIntegrationStart(integrationName, sw);
                    IntegrationStatus status = IntegrationStatusStart(sourceSystemId, integrationName);
                    (integration as BaseIncrementalIntegration)?.ApplySyncOverlap(status, log); // apply sync overlap for incremental integrations only
                    integration.Execute(sourceSystemId, status);
                    IntegrationStatusStop(sourceSystemId, integrationName, status);
                    LogIntegrationStop(integrationName, sw);
                }
                catch (Exception e)
                {
                    IntegrationStatusError(sourceSystemId, integrationName);
                    throw new Exception($"Error executing integration {integration.GetType().FullName}", e);
                }
            }
            else
            {
                var skipLogging = Boolean.TrueString.Equals(SettingOverrides.GetValueOrDefault("DisableSkippedIntegrationLogging"), StringComparison.OrdinalIgnoreCase);
                if (!skipLogging)
                {
                    string message = $"Skipped integration {integration.GetType().FullName}, because it is disabled";
                    executionSummary.Add(message);
                    log.Info(message);
                }

            }
        }

        private static string GetIntegrationName(BaseIntegration integration)
        {
            return integration.GetType().Name;
        }

        private IntegrationStatus IntegrationStatusStart(int sourceSystemId, string integrationName) => new IntegrationStatus(integrationStatusService.IntegrationStart(sourceSystemId, integrationName));

        private void IntegrationStatusStop(int sourceSystemId, string integrationName, IntegrationStatus status) => integrationStatusService.IntegrationStop(sourceSystemId, integrationName, status.MaxSyncDateTime, status.MaxSyncDateMaxPKId);

        private void IntegrationStatusError(int sourceSystemId, string integrationName) => integrationStatusService.IntegrationError(sourceSystemId, integrationName);

        private void LogIntegrationStart(string integrationName, Stopwatch stopWatch)
        {
            ValidateStopwatch(stopWatch);
            log.Info($"Starting integration: {integrationName} at {DateTime.Now}");
            stopWatch?.Restart();
        }

        private void LogIntegrationStop(string integrationName, Stopwatch stopWatch)
        {
            ValidateStopwatch(stopWatch);
            string message = $"Finished integration: {integrationName} at {DateTime.Now} with total run time of {stopWatch.ElapsedMilliseconds}ms";
            log.Info(message);
            executionSummary.Add(message);
        }

        private static void ValidateStopwatch(Stopwatch stopWatch)
        {
            if (stopWatch == null)
            {
                throw new ArgumentNullException(nameof(stopWatch), "You must provide a stopwatch in order to use this function");
            }
        }
    }
}
