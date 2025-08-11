using Castle.Core.Internal;
using Gravitate.Domain.Adapter.DAL;
using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System;
using System.Data.SqlClient;
using System.Linq;



namespace Gravitate.Integration.RA
{
    public class EftIntegration : BaseIncrementalIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EftIntegration));

        private readonly IQueryable<VAesGravitateBankDepositHeaderEntity> _AllianceBankDepositQueryable;
        //private readonly IQueryable<CustomEftHeaderEntity> _customEftHeaderQueryable;
        private readonly IPaymentIntegrationService _paymentIntegrationService;

        /*public EftIntegration(IQueryable<CustomEftHeaderEntity> customEftHeaderQueryable, IPaymentIntegrationService paymentIntegrationService)
        {
            _customEftHeaderQueryable = customEftHeaderQueryable;
            _paymentIntegrationService = paymentIntegrationService;
        }*/

        public EftIntegration(IQueryable<VAesGravitateBankDepositHeaderEntity> aesBankDepositQueryable, IPaymentIntegrationService paymentIntegrationService)
        {
            _AllianceBankDepositQueryable = aesBankDepositQueryable;
            _paymentIntegrationService = paymentIntegrationService;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            int batchSize = base.GetInt("BatchSize", true) ?? 500;
            int resultCount;

            Resync(sourceSystemId, batchSize);
            do
            {
                resultCount = ProcessBatch(sourceSystemId, status, batchSize, false);

            } while (resultCount == batchSize);


            //Cleanup any duplicates now...
            string sql = $@"Delete from PaymentApplied
                            where PaymentID not in (select max(PaymentID) as PaymentID
						                            from Payment
						                            group by ReferenceNumber, ExternalCounterPartyID)";
            ExecuteQuery(sql);

            sql = $@"Delete from Payment
                            where PaymentID not in (select max(PaymentID) as PaymentID
						                            from Payment
						                            group by ReferenceNumber, ExternalCounterPartyID)";
            ExecuteQuery(sql);


        }

        private void Resync(int sourceSystemId, int batchSize)
        {
            int resultCount = 0;
            IntegrationStatus status = new IntegrationStatus();

            if (CounterPartySourceIdsToResync.Any(false))
            {
                Log.Info($"Resyncing EFTs for BAIDs: {String.Join(", ", CounterPartySourceIdsToResync)}");

                do
                {
                    resultCount = ProcessBatch(sourceSystemId, status, batchSize, true);

                } while (resultCount == batchSize);
            }
        }
       

        private int ProcessBatch(int sourceSystemId, IntegrationStatus status, int batchSize, bool isResync)
        {
            var efts = GetEfts(status, batchSize, isResync);

            var resultCount = efts.Count();
            DateTime? maxSyncDateTime = efts.Any(eft => eft.LastUpdateTimestamp.HasValue) ? efts.Max(eft => eft.LastUpdateTimestamp).Value : status.MaxSyncDateTime;
            int? maxPkId = efts.Any() ? efts.Where(eft => eft.LastUpdateTimestamp == maxSyncDateTime).Max(d => d.BankDepositHdrId) : status.MaxSyncDateMaxPKId;

            Log.Info($"Acquired batch of {resultCount} EFTs to process. Batch MaxPkId: {maxPkId}, MaxSyncDateTime: {maxSyncDateTime}, IsResync: {isResync}");

            var dtos = efts.Select(GetHeaderDto).ToArray();
            _paymentIntegrationService.CreateOrUpdatePayments(sourceSystemId, dtos);

            status.MaxSyncDateTime = maxSyncDateTime;
            status.MaxSyncDateMaxPKId = maxPkId;
            return resultCount;
        }

        private VAesGravitateBankDepositHeaderEntity[] GetEfts(IntegrationStatus status, int batchSize, bool isResync)
        {
            int[] invoiceTypes = base.GetIntArray("InvoiceTypes", true);
            DateTime loadStartDate = base.GetDateTime("LoadStartDate", true).Value;
            int[] internalBaiDs = base.GetIntArray("InternalBAIDs", false);

            Log.Info($"EFT Filter Expression: " +
                     $"Invoice Types: {String.Join(",", invoiceTypes)}, " +
                     $"Load Start: {loadStartDate}, " +
                     $"Internal BAID: {(internalBaiDs.IsNullOrEmpty() ? "" : String.Join(",", internalBaiDs))}, ");

            /*    var eftQueryable =
                    _customEftHeaderQueryable
                        .WithPath(PrefetchDetails)
                        .Where(eft => eft.PlannedEftDate > loadStartDate || eft.CustomEftDetails.Any(eftDetail => eftDetail.SalesInvoiceHeader.SlsInvceHdrPstdDte > loadStartDate));
            */
            var eftQueryable =
                 _AllianceBankDepositQueryable
                     .WithPath(PrefetchDetails)
                     .Where(eft => eft.PlannedDepositDate > loadStartDate || eft.VAesGravitateBankDepositDetails.Any(detail => detail.SalesInvoiceHeader.SlsInvceHdrPstdDte > loadStartDate)); 


            if (internalBaiDs != null)
            {
                eftQueryable = eftQueryable.Where(eft => internalBaiDs.Contains(eft.InternalBaid));
            }

           /* if (invoiceTypes != null)
            {
           //     eftQueryable = eftQueryable.Where(eft => eft.CustomEftDetails.Any(eftDetail => !eftDetail.RaInvoiceId.HasValue || invoiceTypes.Contains(eftDetail.SalesInvoiceHeader.SlsInvceHdrSlsInvceTpeId)));
            }*/

            if (invoiceTypes != null)
            {
                eftQueryable = eftQueryable.Where(eft => eft.VAesGravitateBankDepositDetails.Any(detail => detail.RainvoiceId == 0 || !detail.RainvoiceId.HasValue || invoiceTypes.Contains(detail.SalesInvoiceHeader.SlsInvceHdrSlsInvceTpeId)));
            }

            if (isResync)
            {
                eftQueryable = eftQueryable.Where(eft => CounterPartySourceIdsToResync.Contains(eft.ExternalBaid));
            }

            if (status.MaxSyncDateTime.HasValue && status.MaxSyncDateMaxPKId.HasValue)
            {
                eftQueryable = eftQueryable.Where(eft => eft.LastUpdateTimestamp > status.MaxSyncDateTime || (eft.LastUpdateTimestamp == status.MaxSyncDateTime && eft.BankDepositHdrId > status.MaxSyncDateMaxPKId));
            }

            return eftQueryable
                        .OrderBy(eft => eft.LastUpdateTimestamp)
                        .ThenBy(eft => eft.DepositNumber)
                        .ThenBy(eft => eft.DepositDocument)
                        .ThenBy(eft => eft.ExternalBaid)
                        .Take(batchSize)
                        .ToArray();            
        }

        public PaymentIntegrationDTO GetHeaderDto(VAesGravitateBankDepositHeaderEntity eft)
        {
            var eftDto = new PaymentIntegrationDTO();

            eftDto.PayOrReceiveMeaning = PayOrReceive.ReceiveMeaning;
            eftDto.StatusMeaning = PaymentStatus.CompletedMeaning;
            eftDto.PaymentMethodMeaning = PaymentMethod.EFTMeaning;
            eftDto.PaymentDate = eft.PlannedDepositDate ?? DateTime.Now;
            eftDto.ReferenceNumber = eft.DepositNumber;
            eftDto.Amount = eft.TotalDepositAmount ?? 0;
            eftDto.AmountCurrencyIdLookup = new RelatedEntityLookup() { SourceSystemId = PrimarySourceSystemId, SourceId = eft.CurrencyId };
            eftDto.InternalCounterPartyIdLookup = new RelatedEntityLookup() { SourceSystemId = PrimarySourceSystemId, SourceId = eft.InternalBaid };
            eftDto.ExternalCounterPartyIdLookup = new RelatedEntityLookup() { SourceSystemId = PrimarySourceSystemId, SourceId = eft.ExternalBaid };
           
            eftDto.EncryptedBankAccount = eft.DepositDocument;
            eftDto.IsActive = true;
            eftDto.SourceId = eft.BankDepositHdrId;


            eftDto.PaymentAppliedDtos = eft.VAesGravitateBankDepositDetails.Select((eftDetail, i) => GetDetailDto(eftDetail, i));


            return eftDto;
        }

     /*   public PaymentAppliedIntegrationDTO GetDetailDto(CustomEftDetailEntity eftDetail, int index)
        {
            var detailDto = new PaymentAppliedIntegrationDTO();
            detailDto.InvoiceIdLookup = eftDetail.RaInvoiceId.HasValue ? new RelatedEntityLookup() { SourceSystemId = PrimarySourceSystemId, SourceId = eftDetail.RaInvoiceId } : null;
            detailDto.InvoiceNumber = eftDetail.InvoiceNumber;
            detailDto.Amount = eftDetail.InvoiceEftAmount;
            detailDto.SourceId = index + 1;
            return detailDto;
        }
     */
        public PaymentAppliedIntegrationDTO GetDetailDto(VAesGravitateBankDepositDetailEntity eftDetail, int index)
        {
            var detailDto = new PaymentAppliedIntegrationDTO();
            detailDto.InvoiceIdLookup = eftDetail.RainvoiceId.HasValue ? new RelatedEntityLookup() { SourceSystemId = PrimarySourceSystemId, SourceId = eftDetail.RainvoiceId } : null;
            detailDto.InvoiceNumber = eftDetail.InvoiceNumber;
            detailDto.Amount = eftDetail.InvoiceAmount;
            detailDto.SourceId = index + 1;
            return detailDto;
        }

        public IPathEdge PrefetchDetails => new PathEdge<VAesGravitateBankDepositHeaderEntity>(VAesGravitateBankDepositHeaderEntity.PrefetchPathVAesGravitateBankDepositDetails);

        public string ExecuteQuery(string sql)
        {
            string returnMessage = null;
            Log.Info($"Using Connect string:  {ApplicationSettingsManager.ApplicationConnectionString}");
            Log.Info($"Executing SQL :  {sql}");
            try
            {

                using (SqlConnection connection = new SqlConnection(ApplicationSettingsManager.ApplicationConnectionString))
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        connection.Open();
                        // command.CommandTimeout = GetTimeoutSetting();
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                returnMessage = reader[0].ToString();
                            }
                        }
                    }
                }
                return returnMessage;
            }
            catch (Exception ex)
            {
                Log.Error($"An exception occurred executing query:  {sql} -  {ex}");
                //throw new Exception($"An exception occurred executing query: {sql}", ex);
                return ex.Message;
            }
        }

    }
}
