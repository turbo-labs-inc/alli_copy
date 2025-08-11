using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace Gravitate.Integration.RA
{
    public class InvoiceDocumentIntegration : BaseIncrementalIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(InvoiceDocumentIntegration));

        private readonly IDocumentIntegrationService _documentIntegrationService;
        private readonly IQueryable<SalesInvoiceHeaderEntity> _sihQueryable;
        private readonly IQueryable<DmtDistributedDocumentLogEntity> _dmtDistributedDocumentLogQueryable;

        public InvoiceDocumentIntegration(IDocumentIntegrationService documentIntegrationService, IQueryable<SalesInvoiceHeaderEntity> sihQueryable, IQueryable<DmtDistributedDocumentLogEntity> dmtDistributedDocumentLogQueryable)
        {
            _documentIntegrationService = documentIntegrationService;
            _sihQueryable = sihQueryable;
            _dmtDistributedDocumentLogQueryable = dmtDistributedDocumentLogQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Log.Info("Starting Invoice Document Integration");
            var batchSize = base.GetInt("BatchSize", true).GetValueOrDefault();
            int resultCount;

            Resync(sourceSystemId, batchSize);

            do
            {
                resultCount = ProcessBatch(sourceSystemId, status, batchSize, false);
            } while (resultCount == batchSize);
        }

        private void Resync(int sourceSystemId, int batchSize)
        {
            IntegrationStatus status = new IntegrationStatus();

            if (CounterPartySourceIdsToResync.Any(false))
            {
                Log.Info($"Resyncing Invoices for BAIDs: {String.Join(", ", base.CounterPartySourceIdsToResync)}");

                int resultCount;
                do
                {
                    resultCount = ProcessBatch(sourceSystemId, status, batchSize, true);
                } while (resultCount == batchSize);
            }
        }

        private int ProcessBatch(int sourceSystemId, IntegrationStatus status, int batchSize, bool isResync)
        {
            var raInvoices = GetRaInvoices(status, batchSize, isResync);

            var resultCount = raInvoices.Count();
            DateTime? maxSyncDateTime = raInvoices.Any() ? raInvoices.Max(d => d.MaxDistributedDate) : status.MaxSyncDateTime;
            int? maxPkId = raInvoices.Any() ? raInvoices.Where(d => d.MaxDistributedDate == maxSyncDateTime).Max(d => d.SlsInvceHdrId) : status.MaxSyncDateMaxPKId;
            Log.Info($"Acquired batch of {resultCount} invoice docs to process. Batch MaxPkId: {maxPkId}, MaxSyncDateTime: {maxSyncDateTime}, IsResync: {isResync}");

            DocumentIntegrationDTO[] invoiceDocumentDtos =
                raInvoices
                    .Select(raInvoice => CreateDtoFromInvoice(raInvoice, sourceSystemId))
                    .Where(doc => doc != null)
                    .ToArray();

            _documentIntegrationService.CreateOrUpdateInvoiceDocuments(sourceSystemId, invoiceDocumentDtos);

            status.MaxSyncDateTime = maxSyncDateTime;
            status.MaxSyncDateMaxPKId = maxPkId;
            return resultCount;
        }

        public class DocProjection
        {
            public int SlsInvceHdrId { get; set; }

            public string SlsInvceHdrNmbr { get; set; }

            public DateTime MaxDistributedDate { get; set; }
        }

        private DocProjection[] GetRaInvoices(IntegrationStatus status, int batchSize, bool isResync)
        {
            int[] invoiceTypes = base.GetIntArray("InvoiceTypes", true);
            DateTime loadStartDate = base.GetDateTime("LoadStartDate", true).Value;
            int[] internalBaiDs = base.GetIntArray("InternalBAIDs", false);
            DateTime currentDateSmallDateCutoff = base.IncrementalIntegrationsStartDateTime; //Only get updates from to the start of the current run
            var cpSourceIdsToResync = base.CounterPartySourceIdsToResync;

            var queryable =
                _sihQueryable
                    .Where(
                        sih =>
                            invoiceTypes.Contains(sih.SlsInvceHdrSlsInvceTpeId)
                            && sih.SlsInvceHdrPstdDte >= loadStartDate
                            && sih.SlsInvceHdrStts == "S");

            if (internalBaiDs != null)
            {
                queryable = queryable.Where(sih => internalBaiDs.Contains(sih.SlsInvceHdrIntrnlBaid));
            }

            if (isResync)
            {
                queryable = queryable.Where(dh => cpSourceIdsToResync.Contains(dh.SlsInvceHdrBarltnBaid));
            }

            if (status.MaxSyncDateTime.HasValue && status.MaxSyncDateMaxPKId.HasValue)
            {

                queryable = queryable.Where(sih => sih.SalesInvoiceLogs.Any(sil => sil.DmtDocument.DmtDistributedDocumentLogs
                    .Where(dmtl => dmtl.DistributionMethod == "FS"
                    && dmtl.DmtDocument.FileName != null
                    && dmtl.DmtDocument.FileType != null
                    && dmtl.DistributionParameters != null)
                    .Any(ddl => ddl.DistributedDate > status.MaxSyncDateTime || ddl.DistributedDate == status.MaxSyncDateTime && sih.SlsInvceHdrId > status.MaxSyncDateMaxPKId.Value)));

            }

            return
                queryable
                    .Select(sih =>
                        new DocProjection()
                        {
                            SlsInvceHdrId = sih.SlsInvceHdrId,
                            SlsInvceHdrNmbr = sih.SlsInvceHdrNmbr,
                            MaxDistributedDate =
                                sih.SalesInvoiceLogs.Max(sil =>
                                    sil.DmtDocument.DmtDistributedDocumentLogs
                                        .Where(dmtl => dmtl.DistributionMethod == "FS"
                                            && dmtl.DmtDocument.FileName != null
                                            && dmtl.DmtDocument.FileType != null
                                            && dmtl.DistributionParameters != null)
                                        .Max(ddl => ddl.DistributedDate))
                        })
                    .Where(x => x.MaxDistributedDate < currentDateSmallDateCutoff)
                    .OrderBy(dh => dh.MaxDistributedDate)
                    .ThenBy(dh => dh.SlsInvceHdrId)
                    .Take(batchSize)
                    .ToArray();
        }

        private DocumentIntegrationDTO CreateDtoFromInvoice(DocProjection raInvoice, int sourceSystemId)
        {
            var documentDto =
                base.GetBool("SendDocumentData") == true
                    ? GetDocumentWithData(raInvoice, sourceSystemId)
                    : GetDocumentByPath(raInvoice, sourceSystemId);

            documentDto.PrimaryEntityLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raInvoice.SlsInvceHdrId };

            documentDto.FileTypeMeaning = "PDF";
            documentDto.DocumentTypeMeaning = "Invoice";
            documentDto.SignatureStatusMeaning = "SignatureNotNeeded";
            documentDto.SaveToDisk = base.GetBool("SaveToDisk") ?? false;

            if (documentDto.SaveToDisk && String.IsNullOrEmpty(documentDto.FileRelativePath))
            {
                documentDto.FileRelativePath = GetRelativePath_Custom(raInvoice);
                SetNameFromPath(documentDto);
            }

            documentDto.IsActive = true;

            //NOTE: This doesn't seem to be supported by the integration service yet
            //documentDto.LastModified = raInvoice.MaxDistributedDate;

            return documentDto;
        }

        private void SetNameFromPath(DocumentIntegrationDTO documentDto)
        {
            documentDto.Name = ShortenFilename(TryGetFileName(documentDto.FileRelativePath, Log), 100);
        }

        private DocumentIntegrationDTO GetDocumentByPath(DocProjection raInvoice, int sourceSystemId)
        {
            var filename = GetRelativePath_Custom(raInvoice);

            if (string.IsNullOrWhiteSpace(filename))
            {
                return null;
            }

            var documentDto = new DocumentIntegrationDTO();

            documentDto.FileRelativePath = filename;
            SetNameFromPath(documentDto);

            return documentDto;
        }

        private static string GetRelativePath_Custom(DocProjection raInvoice)
        {
            //Changing to be a directory for every 10000 invoice numbers.
            //return Path.Combine(raInvoice.MaxDistributedDate.Year.ToString(), $"{raInvoice.SlsInvceHdrNmbr}.pdf");

            int dirnum = int.Parse(raInvoice.SlsInvceHdrNmbr) / 10000;

            return Path.Combine(dirnum.ToString(), $"{raInvoice.SlsInvceHdrNmbr}.pdf");


        }

        private DocumentIntegrationDTO GetDocumentWithData(DocProjection raInvoice, int sourceSystemId)
        {
            var docData = GetStoredDmtData(raInvoice.SlsInvceHdrId);

            if (docData == null)
            {
                return null;
            }

            var documentDto = new DocumentIntegrationDTO();


            documentDto.Name = ShortenFilename(docData.FileName, 100);
            documentDto.Data = docData.FileData;

            return documentDto;
        }

        private Expression<Func<DmtDistributedDocumentLogEntity, bool>> DistributedLogFilter => dmtl => dmtl.DistributionMethod == "FS"
                                                                                                        && dmtl.DmtDocument.FileName != null
                                                                                                        && dmtl.DmtDocument.FileType != null
                                                                                                        && dmtl.DistributionParameters != null;

        private IOrderedQueryable<DmtDistributedDocumentLogEntity> GetFilteredDmtDocQueryable(int slsInvceHdrId)
        {
            var filteredDmtDocQueryable =
                _dmtDistributedDocumentLogQueryable
                    .Where(DistributedLogFilter)
                    .Where(dmtl =>
                        dmtl.DmtDocument.SalesInvoiceLogs.Any(sil => sil.SlsInvceLgSlsInvceHdrId == slsInvceHdrId)
                    )
                    .OrderByDescending(x => x.DistributedDate);
            return filteredDmtDocQueryable;
        }

        protected override string TryGetFileName(string path, ILog log)
        {
            try
            {
                return Path.GetFileName(path);
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return null;
            }
        }

        public override string ShortenFilename(string filename, int maxLength)
        {
            if (filename == null)
            {
                return null;
            }

            if (filename.Length > maxLength)
            {
                var ext = Path.GetExtension(filename);
                var name = Path.GetFileNameWithoutExtension(filename);
                name = name.Substring(0, maxLength - ext.Length);
                filename = name + ext;
            }

            return filename;
        }

        private class DmtDocData
        {
            public int DocumentId { get; set; }
            public string FileName { get; set; }
            public byte[] FileData { get; set; }
        }

        private DmtDocData GetStoredDmtData(int slsInvceHdrId)
        {
            var filteredDmtDocQueryable = GetFilteredDmtDocQueryable(slsInvceHdrId);

            var doc = filteredDmtDocQueryable.Select(dmtl => new DmtDocData { DocumentId = dmtl.DocumentId, FileName = dmtl.DmtDocument.FileName, FileData = dmtl.DmtDocument.File }).FirstOrDefault();

            if (doc != null)
            {
                Log.Info($"Latest DMTDocument for SlsInvceHdrId: {slsInvceHdrId} is DocumentId: {doc.DocumentId}  Filename: '{doc.FileName}'");

            }

            return doc;
        }
    }
}
