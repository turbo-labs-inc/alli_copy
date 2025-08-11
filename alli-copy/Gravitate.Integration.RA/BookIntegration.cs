using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class BookIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BookIntegration));

        private readonly IQueryable<VAesGravBookLocationAssociationEntity> _bookLocation;
        private readonly IBookIntegrationService _bookIntegrationService;

        public BookIntegration(IBookIntegrationService bookIntegrationService, IQueryable<VAesGravBookLocationAssociationEntity> bookLocation)
        {
            _bookIntegrationService = bookIntegrationService;
            _bookLocation = bookLocation;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();

            var bookProjections = _bookLocation.Select(x => new BookProjection(){ Id = x.BookSourceId, Name = x.BookName}).Distinct().ToArray();
            Log.Info($"RA strategy query took {sw.Elapsed} for {bookProjections.Length} strategies");
            var bookDtOs = bookProjections.Select(strategy => TranslateEntity(sourceSystemId, strategy)).ToArray();

            Log.Info("Calling BulkSyncLocations");
            _bookIntegrationService.BulkSyncBooks(sourceSystemId, bookDtOs);
            Log.Info("Completed call to BulkSyncBooks");
        }

        private BookIntegrationDTO TranslateEntity(int sourceSystemId, BookProjection projection)
        {
            BookIntegrationDTO bookDto = new BookIntegrationDTO();

            bookDto.SourceId = projection.Id;
            bookDto.Name = projection.Name;
            bookDto.IsActive = true;
            bookDto.InternalCounterpartyLookup = null;
            return bookDto;
        }

        class BookProjection
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

    }
}
