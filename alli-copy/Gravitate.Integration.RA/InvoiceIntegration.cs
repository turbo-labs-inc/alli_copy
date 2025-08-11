using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Integration.RA.DAL.HelperClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Gravitate.Integration.RA
{
    public class InvoiceIntegration : BaseIncrementalIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(InvoiceIntegration));
        private static readonly ILog FileCopyLog = LogManager.GetLogger("FileCopyLog");

        private readonly IQueryable<SalesInvoiceHeaderEntity> _sihQueryable;
        private readonly IQueryable<SalesInvoiceLogEntity> _silQueryable;

        private readonly IInvoiceIntegrationService _invoiceIntegrationService;

        private readonly int _sourceSystemUomId = 3;

        private int DetailPriceDescriptionDecimalPrecision { get; set; } = 2;
        private int DetailPriceDescriptionDecimalsToDisplay { get; set; } = 2;

        /// <summary>
        /// Global Specific, Used to store what sales invoice details are embedded
        /// </summary>
        private Dictionary<int, int> ParentChildSalesInvoiceHeader { get; set; } = new Dictionary<int, int>();


        public InvoiceIntegration(IQueryable<SalesInvoiceHeaderEntity> sihQueryable, IQueryable<SalesInvoiceLogEntity> silQueryable, IInvoiceIntegrationService invoiceIntegrationService)
        {
            _sihQueryable = sihQueryable;
            _silQueryable = silQueryable;
            _invoiceIntegrationService = invoiceIntegrationService;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            //int[] invoiceTypes = new int[] { 2, 14, 1, 20, 100 };
            int batchSize = base.GetInt("BatchSize", true).Value;
            int resultCount = 0;

            DetailPriceDescriptionDecimalPrecision = base.GetInt("DetailPriceDescriptionDecimalPrecision", true).Value;
            DetailPriceDescriptionDecimalsToDisplay = base.GetInt("DetailPriceDescriptionDecimalsToDisplay", true).Value;

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
                Log.Info($"Resyncing Invoices for BAIDs: {String.Join(", ", base.CounterPartySourceIdsToResync)}");

                do
                {
                    resultCount = ProcessBatch(sourceSystemId, status, batchSize, resultCount, true);

                } while (resultCount == batchSize);
            }
        }

        private int ProcessBatch(int sourceSystemId, IntegrationStatus status, int batchSize, int resultCount, bool isResync)
        {
            SalesInvoiceHeaderEntity[] raInvoices = GetRaInvoices(status, batchSize, isResync);

            resultCount = raInvoices.Count();
            DateTime? maxSyncDateTime = raInvoices.Any(d => d.SlsInvceHdrPstdDte.HasValue) ? raInvoices.Max(d => d.SlsInvceHdrPstdDte).Value : status.MaxSyncDateTime;
            int? maxPkId = raInvoices.Any() ? raInvoices.Where(d => d.SlsInvceHdrPstdDte == maxSyncDateTime).Max(d => d.SlsInvceHdrId) : status.MaxSyncDateMaxPKId;
            Log.Info($"Acquired batch of {resultCount} invoices to process. Batch MaxPkId: {maxPkId}, MaxSyncDateTime: {maxSyncDateTime}, IsResync: {isResync}");

            InvoiceIntegrationDTO[] invoiceDtOs = raInvoices.Select(raInvoice => TranslateEntity(raInvoice, sourceSystemId)).ToArray();

            _invoiceIntegrationService.CreateOrUpdateInvoices(sourceSystemId, invoiceDtOs);

            status.MaxSyncDateTime = maxSyncDateTime;
            status.MaxSyncDateMaxPKId = maxPkId;
            return resultCount;
        }

        public Expression<Func<SalesInvoiceHeaderEntity, bool>> GetInvoiceFilterExpression()
        {
            int[] invoiceTypes = base.GetIntArray("InvoiceTypes", true);
            DateTime loadStartDate = base.GetDateTime("LoadStartDate", true).Value;
            int[] internalBaiDs = base.GetIntArray("InternalBAIDs", false);
            DateTime currentDateSmallDateCutoff = DateTime.Now.SetSecond(0).SetMillisecond(0); //Only get updates prior to the start of the current minute

            if (internalBaiDs == null)
            {
                return sih => sih.SlsInvceHdrPstdDte < currentDateSmallDateCutoff && invoiceTypes.Contains(sih.SlsInvceHdrSlsInvceTpeId) && sih.SlsInvceHdrPstdDte >= loadStartDate && sih.SlsInvceHdrStts == "S";
            }
            else
            {
                return sih => sih.SlsInvceHdrPstdDte < currentDateSmallDateCutoff && invoiceTypes.Contains(sih.SlsInvceHdrSlsInvceTpeId) && sih.SlsInvceHdrPstdDte >= loadStartDate && internalBaiDs.Contains(sih.SlsInvceHdrIntrnlBaid) && sih.SlsInvceHdrStts == "S";
            }
        }

        private SalesInvoiceHeaderEntity[] GetRaInvoices(IntegrationStatus status, int batchSize, bool isResync)
        {
            IQueryable<SalesInvoiceHeaderEntity> invoiceQuery = _sihQueryable
                                                                        .WithPath(
                                                                            PrefetchSalesInvoiceType,
                                                                            PrefetchTerm,
                                                                            PrefetchSalesInvoiceDetails
                                                                            )
                                                                        .Where(GetInvoiceFilterExpression());

            if (isResync)
            {
                invoiceQuery = invoiceQuery.Where(dh => CounterPartySourceIdsToResync.Contains(dh.SlsInvceHdrBarltnBaid));
            }

            if (status.MaxSyncDateTime.HasValue && status.MaxSyncDateMaxPKId.HasValue)
            {
                invoiceQuery = invoiceQuery.Where(dh => dh.SlsInvceHdrPstdDte > status.MaxSyncDateTime || (dh.SlsInvceHdrPstdDte == status.MaxSyncDateTime && dh.SlsInvceHdrId > status.MaxSyncDateMaxPKId.Value));
            }

            SalesInvoiceHeaderEntity[] raInvoices = invoiceQuery
                                            .OrderBy(dh => dh.SlsInvceHdrPstdDte)
                                            .ThenBy(dh => dh.SlsInvceHdrId)
                                            .Take(batchSize)
                                            .ToArray();
            return raInvoices;
        }

        public InvoiceIntegrationDTO TranslateEntity(SalesInvoiceHeaderEntity raInvoice, int sourceSystemId)
        {
            InvoiceIntegrationDTO invoiceDto = new InvoiceIntegrationDTO();

            invoiceDto.InvoiceNumber = raInvoice.SlsInvceHdrNmbr;
            invoiceDto.InvoiceAmount = raInvoice.SlsInvceHdrTtlVle;//.SalesInvoiceDetails.Sum(sid => sid.SlsInvceDtlTrnsctnVle);
            invoiceDto.InvoiceDateTime = raInvoice.SlsInvceHdrPstdDte ?? raInvoice.SlsInvceHdrCrtnDte;
            invoiceDto.Terms = raInvoice.Term.TrmAbbrvtn;
            invoiceDto.DueDate = raInvoice.SlsInvceHdrDueDte;
            invoiceDto.InternalCounterPartyIdLookup = new RelatedEntityLookup() { SourceId = raInvoice.SlsInvceHdrIntrnlBaid, SourceSystemId = sourceSystemId };
            invoiceDto.ExternalCounterPartyIdLookup = new RelatedEntityLookup() { SourceId = raInvoice.SlsInvceHdrBarltnBaid, SourceSystemId = sourceSystemId };
            invoiceDto.CurrencyIdLookup = new RelatedEntityLookup() { SourceId = raInvoice.SlsInvceHdrCrrncyId, SourceSystemId = sourceSystemId };
            invoiceDto.InvoiceStatusMeaning = InvoiceStatus.UnpaidMeaning;
            invoiceDto.IsActive = true;
            invoiceDto.SourceId = raInvoice.SlsInvceHdrId;
            invoiceDto.InternalColleagueIdLookup = raInvoice.InternalCntctId.HasValue ? new RelatedEntityLookup() { SourceId = raInvoice.InternalCntctId.Value, SourceSystemId = sourceSystemId } : null;
            invoiceDto.PayOrReceiveMeaning = PayOrReceive.ReceiveMeaning;
            invoiceDto.InvoiceTypeMeaning = GetInvoiceTypeCodeValue(raInvoice.SalesInvoiceType);

            invoiceDto.InvoiceDetailDTOs = GetInvoiceDetailDtOs(sourceSystemId, raInvoice.SalesInvoiceDetails);

            return invoiceDto;
        }

        private string GetInvoiceTypeCodeValue(SalesInvoiceTypeEntity salesInvoiceTypeEntity)
        {
            if (salesInvoiceTypeEntity != null && !String.IsNullOrWhiteSpace(salesInvoiceTypeEntity.SlsInvceTpeAbbrvtn))
            {
                string meaning = salesInvoiceTypeEntity.SlsInvceTpeAbbrvtn.Trim().Replace(" ", String.Empty);

                return CodeSetFacade.Instance.InvoiceType.GetByMeaning(meaning)?.Meaning;
            }
            else
            {
                return null;
            }
        }

        private Dictionary<string, string> GetGeneralConfigAttributes(EntityCollection<GeneralConfigurationEntity> entityCollection)
        {
            //Documentations says entityCollection should be empty not null
            return entityCollection.ToDictionary(key => key.GnrlCnfgQlfr, value => value.GnrlCnfgMulti);
        }

        private List<InvoiceDetailIntegrationDTO> GetInvoiceDetailDtOs(int sourceSystemId, EntityCollection<SalesInvoiceDetailEntity> entityCollection)
        {
            //Documentations says entityCollection should be empty not null
            return entityCollection.Select(dd => TranslateInvoiceDetailEntity(dd, sourceSystemId)).ToList();
        }

        private InvoiceDetailIntegrationDTO TranslateInvoiceDetailEntity(SalesInvoiceDetailEntity sid, int sourceSystemId)
        {
            InvoiceDetailIntegrationDTO invoiceDetail = new InvoiceDetailIntegrationDTO();

            invoiceDetail.SourceId = sid.AccountDetail.AcctDtlId;

            invoiceDetail.BillOfLadingDetailIdLookup = GetBillOfLadingDetailIdLookup(sid, sourceSystemId);
            invoiceDetail.TradeDetailIdLookup = GetTradeDetailIdLookup(sid, sourceSystemId);
            invoiceDetail.Quantity = GetInvoiceDetailQuantity(sid);
            if (sid.SlsInvceDtlOrgnLcleId.HasValue)
            {
                if (sid.SlsInvceDtlOrgnLcleId == 14)
                {
                    //var i = 0;
                }
            }
            invoiceDetail.OriginLocationIdLookup = sid.SlsInvceDtlOrgnLcleId.HasValue ? new RelatedEntityLookup() { SourceId = sid.SlsInvceDtlOrgnLcleId.Value, SourceSystemId = sourceSystemId } : null;
            invoiceDetail.DestinationLocationIdLookup = GetDestinationLocationIdLookup(sid, sourceSystemId);
            invoiceDetail.BOLDateTime = sid.AccountDetail.AcctDtlTrnsctnDte;
            invoiceDetail.PriceDescription = GetPriceDescription(sid);
            invoiceDetail.TotalAmount = sid.SlsInvceDtlTrnsctnVle;
            invoiceDetail.ProductIdLookup = sid.AccountDetail.ChildPrdctId.HasValue ? new RelatedEntityLookup() { SourceId = sid.AccountDetail.ChildPrdctId, SourceId2 = GetAlternateProductSourceId(sid.AccountDetail.ChildPrdctId), SourceSystemId = sourceSystemId } : null;
            invoiceDetail.CurrencyIdLookup = sid.CrrncyId.HasValue ? new RelatedEntityLookup() { SourceId = sid.CrrncyId, SourceSystemId = sourceSystemId } : null;
            invoiceDetail.UnitOfMeasureIdLookup = sid.AccountDetail.AcctDtlUomid.HasValue ? new RelatedEntityLookup() { SourceId = sid.AccountDetail.AcctDtlUomid, SourceSystemId = sourceSystemId } : null;
            invoiceDetail.LineItemDescription = GetLineItemDescription(sid);
            invoiceDetail.IsActive = true;
            invoiceDetail.FOBLocationIdLookup = sid.SlsInvceDtlFoblcleId.HasValue ? new RelatedEntityLookup() { SourceId = sid.SlsInvceDtlFoblcleId, SourceSystemId = sourceSystemId } : null;
            invoiceDetail.InvoiceDetailTypeMeaning = InvoiceDetailType.PrimaryMeaning;
            return invoiceDetail;
        }



        private RelatedEntityLookup GetTradeDetailIdLookup(SalesInvoiceDetailEntity sid, int sourceSystemId)
        {
            if (sid.AccountDetail.AcctDtlDlDtlId.HasValue && sid.AccountDetail.AcctDtlDlDtlDlHdrId.HasValue)
            {
                return new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = sid.AccountDetail.AcctDtlDlDtlId.Value, SourceId2 = sid.AccountDetail.AcctDtlDlDtlDlHdrId.Value };
            }
            return null;
        }

        private decimal GetInvoiceDetailQuantity(SalesInvoiceDetailEntity sid)
        {
            if (sid.AccountDetail.AcctDtlUomid.HasValue)
            {
                decimal specificGravity = Convert.ToDecimal(sid.AccountDetail.SpecificGravity);
                return UomConversionCache.Instance.ConvertValue(sid.AccountDetail.Volume, _sourceSystemUomId, sid.AccountDetail.AcctDtlUomid.Value, specificGravity) * -1;
            }
            else
            {
                return 0;
            }
        }

        private RelatedEntityLookup GetBillOfLadingDetailIdLookup(SalesInvoiceDetailEntity sid, int sourceSystemId)
        {
            //isNull(TDL.XDtlLgXDtlXHdrID, case when TD_T.TxDtlSrceTble = ''X'' then TD_T.TxDtlSrceID else null end)
            if (sid.AccountDetail != null)
            {
                //SID -> AD -> TaxDL -> TaxD -> AD -> TDL
                if (
                    sid.AccountDetail.TaxDetailLog != null
                    && sid.AccountDetail.TaxDetailLog.TaxDetail != null
                    && sid.AccountDetail.TaxDetailLog.TaxDetail.AccountDetail != null
                    && sid.AccountDetail.TaxDetailLog.TaxDetail.AccountDetail.TransactionDetailLogs.Any()
                    )
                {
                    var tdls = sid.AccountDetail.TaxDetailLog.TaxDetail.AccountDetail.TransactionDetailLogs;
                    Log.Debug("sid.AccountDetail.TaxDetailLog.TaxDetail.AccountDetail.TransactionDetailLogs count - " + tdls.Count());
                    return new RelatedEntityLookup() { SourceId = tdls.First().XdtlLgXdtlXhdrId, SourceSystemId = sourceSystemId };
                }

                //SID -> AD -> TransDL
                if (sid.AccountDetail.TransactionDetailLogs.Any())
                {
                    var tdls = sid.AccountDetail.TransactionDetailLogs;
                    Log.Debug("sid.AccountDetail.TransactionDetailLogs count - " + tdls.Count());
                    return new RelatedEntityLookup() { SourceId = tdls.First().XdtlLgXdtlXhdrId, SourceSystemId = sourceSystemId };
                }

                //SID -> AD -> TaxDL
                if (sid.AccountDetail.TaxDetailLog != null
                    && sid.AccountDetail.TaxDetailLog.TaxDetail != null
                    && sid.AccountDetail.TaxDetailLog.TaxDetail.TxDtlSrceTble?.Trim() == "X")
                {
                    Log.Debug("sid.AccountDetail.TaxDetailLog.TaxDetail");
                    return new RelatedEntityLookup() { SourceId = sid.AccountDetail.TaxDetailLog.TaxDetail.TxDtlSrceId, SourceSystemId = sourceSystemId };
                }
            }

            return null;
        }

        private string GetLineItemDescription(SalesInvoiceDetailEntity sid)
        {
            if (sid.AccountDetail != null && sid.AccountDetail.TransactionType != null)
            {
                return sid.AccountDetail.TransactionType.TrnsctnTypDesc;
            }

            return null;
        }

        private string GetPriceDescription(SalesInvoiceDetailEntity sid)
        {
            var price = Math.Round(sid.SlsInvceDtlPrUntVle.GetValueOrDefault(), DetailPriceDescriptionDecimalPrecision, MidpointRounding.AwayFromZero);
            var priceDescriptionFormat = "{0:F" + DetailPriceDescriptionDecimalsToDisplay + "} {1}/{2}";
            return String.Format(priceDescriptionFormat,
                Math.Abs(price),
                sid.Currency.CrrncySmbl,
                sid.AccountDetail.UnitOfMeasure != null ? sid.AccountDetail.UnitOfMeasure.Uomabbv : ""
                );
        }

        private RelatedEntityLookup GetDestinationLocationIdLookup(SalesInvoiceDetailEntity sid, int sourceSystemId)
        {
            if (sid.AccountDetail != null && sid.AccountDetail.AcctDtlDestinationLcleId.HasValue)
            {
                return new RelatedEntityLookup() { SourceId = sid.AccountDetail.AcctDtlDestinationLcleId, SourceSystemId = sourceSystemId };
            }

            if (sid.SlsInvceDtlDstntnLcleId.HasValue)
            {
                return new RelatedEntityLookup() { SourceId = sid.SlsInvceDtlDstntnLcleId.Value, SourceSystemId = sourceSystemId };
            }

            return null;
        }

        private DeliverOrReceiveCodeValue GetDeliverOrReceiveCodeValue(string dlDtlSpplyDmnd)
        {
            switch (dlDtlSpplyDmnd)
            {
                case "R":
                    return CodeSetFacade.Instance.DeliverOrReceive.Receive;
                default:
                    return CodeSetFacade.Instance.DeliverOrReceive.Deliver;
            }
        }

        #region PrefetchPaths

        public static IPathEdge PrefetchSalesInvoiceType => new PathEdge<SalesInvoiceTypeEntity>(
                SalesInvoiceHeaderEntity.PrefetchPathSalesInvoiceType
        );

        public static IPathEdge PrefetchTerm => new PathEdge<TermEntity>(
                SalesInvoiceHeaderEntity.PrefetchPathTerm
        );

        public static IPathEdge PrefetchSalesInvoiceDetails => new PathEdge<SalesInvoiceDetailEntity>(
                SalesInvoiceHeaderEntity.PrefetchPathSalesInvoiceDetails,
                PrefetchCurrency,
                PrefetchAccountDetail
        );

        public static IPathEdge PrefetchCurrency => new PathEdge<CurrencyEntity>(
                SalesInvoiceDetailEntity.PrefetchPathCurrency
        );

        public static IPathEdge PrefetchAccountDetail => new PathEdge<AccountDetailEntity>(
                SalesInvoiceDetailEntity.PrefetchPathAccountDetail,
                PrefetchUom,
                PrefetchTransactionType,
                PrefetchTransactionDetailLog,
                PrefetchTaxDetailLog
        );

        public static IPathEdge PrefetchTransactionType => new PathEdge<TransactionTypeEntity>(
                AccountDetailEntity.PrefetchPathTransactionType
        );

        public static IPathEdge PrefetchUom => new PathEdge<UnitOfMeasureEntity>(AccountDetailEntity.PrefetchPathUnitOfMeasure);

        //may account details to 1 Tax detail log
        public static IPathEdge PrefetchTaxDetailLog => new PathEdge<TaxDetailLogEntity>(
                AccountDetailEntity.PrefetchPathTaxDetailLog,
                tdl => tdl.AccountDetails.Any(ad => ad.AcctDtlSrceTble == "T" && tdl.TxDtlLgId == ad.AcctDtlSrceId),
                PrefetchTaxDetail
        );

        public static IPathEdge PrefetchTaxDetail => new PathEdge<TaxDetailEntity>(
                TaxDetailLogEntity.PrefetchPathTaxDetail,
                //PrefetchTransactionHeaderFromTaxDetail,
                PrefetchAccountDetailFromTaxDetail
        );

        public static IPathEdge PrefetchAccountDetailFromTaxDetail => new PathEdge<AccountDetailEntity>(
                TaxDetailEntity.PrefetchPathAccountDetail,
                ad2 => ad2.AcctDtlSrceTble == "X" && ad2.TaxDetails.Any(tdT => ad2.AcctDtlId == tdT.TxDtlSrceId && tdT.TxDtlSrceTble == "A"),
                PrefetchTransactionDetailLog
        );

        public static IPathEdge PrefetchTransactionDetailLog => new PathEdge<TransactionDetailLogEntity>(
                AccountDetailEntity.PrefetchPathTransactionDetailLogs,
                tdl => tdl.AccountDetail.AcctDtlSrceTble == "X"
        );

        #endregion

    }
}
