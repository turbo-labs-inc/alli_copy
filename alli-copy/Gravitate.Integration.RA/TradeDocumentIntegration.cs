using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Xml;

namespace Gravitate.Integration.RA
{

    public class TradeDocumentIntegration : BaseIncrementalIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TradeDocumentIntegration));

        private readonly IDocumentIntegrationService _documentIntegrationService;
        private readonly IQueryable<DealHeaderEntity> _dealHeaderQueryable;
        private readonly IQueryable<DmtDistributedDocumentLogEntity> _dmtDistributedDocumentLogQueryable;

        public TradeDocumentIntegration(IDocumentIntegrationService documentIntegrationService, IQueryable<DealHeaderEntity> dealHeaderQueryable, IQueryable<DmtDistributedDocumentLogEntity> dmtDistributedDocumentLogQueryable)
        {
            _documentIntegrationService = documentIntegrationService;
            _dealHeaderQueryable = dealHeaderQueryable;
            _dmtDistributedDocumentLogQueryable = dmtDistributedDocumentLogQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Log.Info("Starting Document Integration");
            int batchSize = base.GetInt("BatchSize", true).Value;
            int resultCount = 0;
            Resync(sourceSystemId, batchSize);
            do
            {
                resultCount = ProcessBatch(sourceSystemId, status, batchSize, false);

            } while (resultCount == batchSize);

        }
        private void Resync(int sourceSystemId, int batchSize)
        {
            int resultCount = 0;
            IntegrationStatus status = new IntegrationStatus();

            if (base.CounterPartySourceIdsToResync.Any(false))
            {
                Log.Info($"Resyncing Trades for BAIDs: {String.Join(", ", base.CounterPartySourceIdsToResync)}");

                do
                {
                    resultCount = ProcessBatch(sourceSystemId, status, batchSize, true);
                } while (resultCount == batchSize);
            }
        }
        private int ProcessBatch(int sourceSystemId, IntegrationStatus status, int batchSize, bool isResync)
        {
            var raDeals = GetRADeals(status, batchSize, isResync);

            var resultCount = raDeals.Count();

            var maxSyncDateTime = raDeals.Any(d => d.DealHeaderArchives.Any(dha => dha.ApprovalDate.HasValue))
                ? raDeals.Max(d => d.DealHeaderArchives.OrderByDescending(dha => dha.ApprovalDate).FirstOrDefault().PrintDate)
                : status.MaxSyncDateTime;

            var maxPkId = raDeals.Any(d => d.DealHeaderArchives.Any(dha => dha.ApprovalDate.HasValue))
                ? raDeals.Where(d => d.DealHeaderArchives.OrderByDescending(dha => dha.ApprovalDate).FirstOrDefault().PrintDate == maxSyncDateTime).Max(d => d.DlHdrId)
                : status.MaxSyncDateMaxPKId;

            Log.Info($"Acquired batch of {resultCount} trade confirms to process. Batch MaxPkId: {maxPkId}, MaxSyncDateTime: {maxSyncDateTime}, IsResync: {isResync}");

            var tradeDocumentDtos = raDeals.Select(raDeal => CreateDocDtoFromTrade(raDeal, sourceSystemId)).Where(doc => doc != null).ToArray();

            if (tradeDocumentDtos.Any())
            {
                _documentIntegrationService.CreateOrUpdateTradeDocuments(sourceSystemId, tradeDocumentDtos);
            }
            else
            {
                Log.Info("Found no docs for batch.  Continuing to next batch.");
            }



            status.MaxSyncDateTime = maxSyncDateTime;
            status.MaxSyncDateMaxPKId = maxPkId;

            return resultCount;
        }


        private DocumentIntegrationDTO CreateDocDtoFromTrade(DealHeaderEntity raDeal, int sourceSystemId)
        {
            var confirmInfo = GetConfirmInfo(raDeal.DlHdrId);
            if (confirmInfo == null || string.IsNullOrWhiteSpace(confirmInfo.FullPath)) return null;

            var filename = TryGetFileName(confirmInfo.FullPath, Log);

            var documentDto = new DocumentIntegrationDTO();
            documentDto.PrimaryEntityLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raDeal.DlHdrId };
            documentDto.Name = ShortenFilename(filename, 100);
            documentDto.FileRelativePath = raDeal.DlHdrCrtnDte.Year + "\\" + filename;
            documentDto.FileTypeMeaning = "PDF";
            documentDto.DocumentTypeMeaning = "TradeConfirm";
            documentDto.SignatureStatusMeaning = "SignatureNotNeeded";
            documentDto.SaveToDisk = false;
            documentDto.Data = null;
            documentDto.IsActive = true;

            //NOTE: This doesn't seem to be supported yet by the integration service yet
            //documentDto.LastModified = confirmInfo.DistributedDate;

            return documentDto;
        }

        private ConfirmDocInfo GetConfirmInfo(int dlHdrId)
        {
            var doc =
                _dmtDistributedDocumentLogQueryable
                    .Where(dmtl =>
                        dmtl.DmtDocument.DealHeaderArchives.Any(dha => dha.DlHdrId == dlHdrId)
                        && dmtl.DistributionMethod == "FS"
                        && dmtl.DmtDocument.FileName != null
                        && dmtl.DmtDocument.FileType != null
                        && dmtl.DistributionParameters != null
                    )
                    .OrderByDescending(x => x.DistributedDate).Select(dmtl => new { dmtl.DmtDocument.FileName, dmtl.DmtDocument.FileType, dmtl.DistributionParameters, dmtl.DistributedDate }).FirstOrDefault();

            if (doc != null)
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(doc.DistributionParameters);
                string fullPath = Path.Combine(xmlDoc.InnerText, $"{doc.FileName}.{doc.FileType}");

                Log.Info($"DlHdrId: {dlHdrId} DistributedDate: {doc.DistributedDate} FullPath: '{fullPath}'");

                return new ConfirmDocInfo() { FullPath = fullPath, DistributedDate = doc.DistributedDate };
            }
            else
            {
                return null;
            }
        }

        class ConfirmDocInfo
        {
            public string FullPath { get; set; }
            public DateTime DistributedDate { get; set; }
        }

        private DealHeaderEntity[] GetRADeals(IntegrationStatus status, int batchSize, bool isResync)
        {
            var dealQuery =
                _dealHeaderQueryable
                    .WithPath(PrefetchDealHeaderArchive)
                    .Where(GetDealFilterExpression())
                    .Where(dh => dh.DealHeaderArchives.OrderByDescending(dha => dha.ApprovalDate).FirstOrDefault().PrintDate != null);

            if (status.MaxSyncDateTime.HasValue && status.MaxSyncDateMaxPKId.HasValue)
            {
                dealQuery = dealQuery
                    .Where(dh =>
                        dh.DealHeaderArchives.OrderByDescending(dha => dha.ApprovalDate).FirstOrDefault().PrintDate > status.MaxSyncDateTime ||
                        (dh.DealHeaderArchives.OrderByDescending(dha => dha.ApprovalDate).FirstOrDefault().PrintDate == status.MaxSyncDateTime && dh.DlHdrId > status.MaxSyncDateMaxPKId.Value)
                    );
            }

            var raTrades = dealQuery
                .OrderBy(dh => dh.DealHeaderArchives.OrderByDescending(dha => dha.ApprovalDate).FirstOrDefault().PrintDate)
                .ThenBy(dh => dh.DlHdrId)
                .Take(batchSize)
                .ToArray();

            return raTrades;
        }



        public Expression<Func<DealHeaderEntity, bool>> GetDealFilterExpression()
        {
            var tradeTypes = base.GetIntArray("TradeTypes", true);
            var loadStartDate = base.GetDateTime("LoadStartDate", true).Value;
            var internalBaIds = base.GetIntArray("InternalBAIDs", false);

            DateTime currentDateSmallDateCutoff = DateTime.Now.SetSecond(0).SetMillisecond(0); //Only get updates prior to the start of the current minute

            if (internalBaIds == null)
            {
                return dh => dh.DlHdrRvsnDte < currentDateSmallDateCutoff && tradeTypes.Contains(dh.DlHdrTyp.Value) && dh.DlHdrToDte >= loadStartDate;
            }
            else
            {
                return dh => dh.DlHdrRvsnDte < currentDateSmallDateCutoff && tradeTypes.Contains(dh.DlHdrTyp.Value) && dh.DlHdrToDte >= loadStartDate && internalBaIds.Contains(dh.DlHdrIntrnlBaid);
            }
        }

        public IPathEdge PrefetchDealHeaderArchive => new PathEdge<DealHeaderArchiveEntity>(DealHeaderEntity.PrefetchPathDealHeaderArchives);

    }


}
