using Custom.Gravitate.WCF.Integration.Custom;
using Custom.Gravitate.WCF.Integration.Custom.DTO;
using Custom.Gravitate.WCF.Integration.Custom.Interfaces;
using Gravitate.Domain.Adapter.EntityClasses;
using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using log4net;
using SD.LLBLGen.Pro.LinqSupportClasses;
using SD.LLBLGen.Pro.ORMSupportClasses;
using System;
using System.Linq;
using System.Linq.Dynamic;


namespace Gravitate.Integration.RA
{
    public class CustomInvoiceIntegration : BaseIncrementalIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CustomInvoiceIntegration));

        //private readonly IQueryable<CustomInvoiceOraclePaymentEntity> _customQueryable;
        private readonly IQueryable<AllianceSalesInvoiceRemainingBalanceEntity> _salesInvoiceRemainingBalanceQueryable;
        private readonly IQueryable<SalesInvoiceHeaderEntity> _salesInvoiceHeaderQueryable;

        private readonly ICustomInvoiceIntegrationService _customInvoiceIntegrationService;


        /* public CustomInvoiceIntegration(IQueryable<CustomInvoiceOraclePaymentEntity> customQueryable, ICustomInvoiceIntegrationService customInvoiceIntegrationService)
         {
             _customQueryable = customQueryable;
             _customInvoiceIntegrationService = customInvoiceIntegrationService;
         }*/
        public CustomInvoiceIntegration(IQueryable<AllianceSalesInvoiceRemainingBalanceEntity> customQueryable, IQueryable<SalesInvoiceHeaderEntity> salesInvoiceHeaderQueryable,ICustomInvoiceIntegrationService customInvoiceIntegrationService)
        {
            _salesInvoiceRemainingBalanceQueryable = customQueryable;
            _salesInvoiceHeaderQueryable = salesInvoiceHeaderQueryable;
            _customInvoiceIntegrationService = customInvoiceIntegrationService;

        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            //int[] invoiceTypes = new int[] { 2, 14, 1, 20, 100 };
            int batchSize = base.GetInt("BatchSize", true).Value;
            int resultCount = 0;

            Resync(sourceSystemId, batchSize);

            do
            {
                resultCount = ProcessBatch(sourceSystemId, status, batchSize, resultCount, false);

            } while (resultCount == batchSize);
            _customInvoiceIntegrationService.UpdateUnpaidInvoices(status.MaxSyncDateTime);


        }

        private void Resync(int sourceSystemId, int batchSize)
        {
            int resultCount = 0;
            IntegrationStatus status = new IntegrationStatus();

            if (base.CounterPartySourceIdsToResync.Any(false))
            {
                Log.Info($"Resyncing AllianceSalesInvoiceRemainingBalance records for BAIDs: {String.Join(", ", base.CounterPartySourceIdsToResync)}");

                do
                {
                    resultCount = ProcessBatch(sourceSystemId, status, batchSize, resultCount, true);

                } while (resultCount == batchSize);
                _customInvoiceIntegrationService.UpdateUnpaidInvoices(status.MaxSyncDateTime);
            }
        }

        private int ProcessBatch(int sourceSystemId, IntegrationStatus status, int batchSize, int resultCount, bool isResync)
        {            
            int[] invoiceTypes = base.GetIntArray("InvoiceTypes", true);
            DateTime loadStartDate = base.GetDateTime("LoadStartDate", true).Value;
            int[] internalBaiDs = base.GetIntArray("InternalBAIDs", false);

            var cutoff = base.IncrementalIntegrationsStartDateTime;

            /*var tempQueryable =
            _customQueryable.Where(
                    ci => ci.InvoiceId != null
                    && invoiceTypes.Contains(ci.SalesInvoiceHeader.SlsInvceHdrSlsInvceTpeId)
                    && ci.SalesInvoiceHeader.SlsInvceHdrPstdDte >= loadStartDate
                    && ci.InvoiceType == "SH"
                    && ci.SalesInvoiceHeader.SlsInvceHdrStts == "S" && ci.LastUpdateTimestamp < cutoff);
            */
            var tempQueryable = _salesInvoiceRemainingBalanceQueryable;


            
            if (isResync)
            {
                tempQueryable = tempQueryable.Where(ci => CounterPartySourceIdsToResync.Contains(ci.ExternalBaid));
            }

            if (status.MaxSyncDateTime.HasValue && status.MaxSyncDateMaxPKId.HasValue)
            {
                tempQueryable = tempQueryable.Where(ci => ci.LastUpdateTimestamp > status.MaxSyncDateTime || (ci.LastUpdateTimestamp == status.MaxSyncDateTime && ci.InvoiceId > status.MaxSyncDateMaxPKId.Value));

            }

            var customInvoices =
                tempQueryable.OrderBy(ci => ci.LastUpdateTimestamp)
                .ThenBy(ci => ci.InvoiceId)
                .Take(batchSize)
                .ToArray();

            resultCount = customInvoices.Count();
            DateTime? maxSyncDateTime = customInvoices.Any(d => d.LastUpdateTimestamp.HasValue) ? customInvoices.Max(d => d.LastUpdateTimestamp).Value : status.MaxSyncDateTime;
            int? maxPkId = customInvoices.Any() ? customInvoices.Where(d => d.LastUpdateTimestamp == maxSyncDateTime).Max(d => d.InvoiceId) : status.MaxSyncDateMaxPKId;
            Log.Info($"Acquired batch of {resultCount} AllianceSalesInvoiceRemainingBalance records to process. Batch MaxPkId: {maxPkId}, MaxSyncDateTime: {maxSyncDateTime}, IsResync: {isResync}");

            var dtos =
                customInvoices.Select(
                    ci =>
                        new CustomInvoiceIntegrationDTO()
                        {
                            InvoiceSourceId = ci.InvoiceId,
                            AmountDue = ci.AmountDue,
                            AmountPaid = ci.AmountPaid,
                            AmountRemaining = ci.AmountRemaining,
                            RALastUpdateTime = ci.LastUpdateTimestamp
                        }).ToArray();



            _customInvoiceIntegrationService.CreateOrUpdateCustomInvoices(sourceSystemId, dtos);

            status.MaxSyncDateTime = maxSyncDateTime;
            status.MaxSyncDateMaxPKId = maxPkId;

            


            return resultCount;
         
        }
    }
}
