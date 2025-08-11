using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class UnitOfMeasureIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UnitOfMeasureIntegration));

        private readonly IQueryable<UnitOfMeasureEntity> _uomQueryable;
        private readonly IUnitOfMeasureIntegrationService _uomIntegrationService;

        public UnitOfMeasureIntegration(IQueryable<UnitOfMeasureEntity> uomQueryable, IUnitOfMeasureIntegrationService uomIntegrationService)
        {
            _uomQueryable = uomQueryable;
            _uomIntegrationService = uomIntegrationService;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
            UnitOfMeasureEntity[] raUnitOfMeasures = _uomQueryable.ToArray();
            Log.Info($"RA uom query took {sw.Elapsed.ToString()} for {raUnitOfMeasures.Count()} UOMs");

            UnitOfMeasureIntegrationDTO[] unitOfMeasureDtos = raUnitOfMeasures.Select(TranslateEntity).ToArray();

            Log.Info($"Calling {nameof(IUnitOfMeasureIntegrationService.BulkSyncUnitOfMeasures)}");
            _uomIntegrationService.BulkSyncUnitOfMeasures(sourceSystemId, unitOfMeasureDtos);
            Log.Info($"Completed call to {nameof(IUnitOfMeasureIntegrationService.BulkSyncUnitOfMeasures)}");
        }

        public UnitOfMeasureIntegrationDTO TranslateEntity(UnitOfMeasureEntity raUom)
        {
            return new UnitOfMeasureIntegrationDTO
            {
                SourceId = raUom.Uom,
                Name = raUom.Uomabbv,
                Abbreviation = raUom.Uomabbv,
                Symbol = raUom.Uomabbv,
                IsActive = (raUom.Uomstts == RAStatus.Active),
                DefaultFormat = raUom.Uom == 3 ? "N0" : null
            };
        }
    }
}