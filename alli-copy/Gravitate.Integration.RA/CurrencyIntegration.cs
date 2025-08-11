using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class CurrencyIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CurrencyIntegration));

        private readonly IQueryable<CurrencyEntity> _currencyQueryable;
        private readonly ICurrencyIntegrationService _currencyIntegrationService;

        public CurrencyIntegration(IQueryable<CurrencyEntity> currencyQueryable, ICurrencyIntegrationService currencyIntegrationService)
        {
            _currencyQueryable = currencyQueryable;
            _currencyIntegrationService = currencyIntegrationService;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
            CurrencyEntity[] raCurrencies = _currencyQueryable.ToArray();
            Log.Info($"RA currency query took {sw.Elapsed.ToString()} for {raCurrencies.Count()} currencies");

            CurrencyIntegrationDTO[] currencyDtOs = raCurrencies.Select(TranslateEntity).ToArray();

            Log.Info("Calling BulkSyncCurrencies");
            _currencyIntegrationService.BulkSyncCurrencies(sourceSystemId, currencyDtOs);
            Log.Info("Completed call to BulkSyncCurrencies");
        }

        public CurrencyIntegrationDTO TranslateEntity(CurrencyEntity raCurrency)
        {
            CurrencyIntegrationDTO ciDto = new CurrencyIntegrationDTO();

            ciDto.Name = raCurrency.CrrncyAbbrvtn;
            ciDto.Symbol = raCurrency.CrrncySmbl;
            ciDto.SourceId = raCurrency.CrrncyId;
            ciDto.IsActive = true;

            return ciDto;
        }
    }
}
