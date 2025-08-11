using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class PaymentTermIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PaymentTermIntegration));

        private readonly IQueryable<TermEntity> _termQueryable;
        private readonly IPaymentTermIntegrationService _paymentTermIntegrationService;

        public PaymentTermIntegration(IQueryable<TermEntity> termQueryable, IPaymentTermIntegrationService paymentTermIntegrationService)
        {
            _termQueryable = termQueryable;
            _paymentTermIntegrationService = paymentTermIntegrationService;
        }

        public void LogServiceCallComplete() => Log.Info($"Completed call to {nameof(_paymentTermIntegrationService.BulkSyncPaymentTerms)}");
        private void LogServiceCall() => Log.Info($"Calling {nameof(_paymentTermIntegrationService.BulkSyncPaymentTerms)}");
        private static void LogQueryTime(Stopwatch sw, IEnumerable<TermEntity> terms) => Log.Info($"RA term query took {sw.Elapsed.ToString()} for {terms.Count()} terms");

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var terms = _termQueryable.ToArray();
            LogQueryTime(sw, terms);
            var dtos = terms.Select(TranslateEntity).ToArray();
            LogServiceCall();
            _paymentTermIntegrationService.BulkSyncPaymentTerms(sourceSystemId, dtos);
            LogServiceCallComplete();
        }

        public PaymentTermIntegrationDTO TranslateEntity(TermEntity term) => new PaymentTermIntegrationDTO
        {
            Name = term.TrmVrbge,
            Display = term.TrmAbbrvtn,
            SourceId = term.TrmId,
            CustomDatabaseValues = GetCustomDatabaseValues(term)
        };

        /// <summary>
        /// Just including all of the other fields in case we need to do something with them (we don't currently store them in gravitate)
        /// </summary>
        /// <param name="term"></param>
        /// <returns></returns>
        private static Dictionary<string, string> GetCustomDatabaseValues(TermEntity term) => new Dictionary<string, string>()
        {
            {nameof(TermEntity.TrmTrmTpe), term.TrmTrmTpe },
            {nameof(TermEntity.TrmPymntMthd), term.TrmPymntMthd },
            {nameof(TermEntity.TrmStrtDueClcltn), term.TrmStrtDueClcltn },
            {nameof(TermEntity.TrmDyOffstTpe), term.TrmDyOffstTpe},
            {nameof(TermEntity.TrmDyNmbr), term.TrmDyNmbr.ToString() },
            {nameof(TermEntity.TrmDyYr), term.TrmDyYr.ToString() },
            {nameof(TermEntity.TrmDscnt), term.TrmDscnt.ToString() },
            {nameof(TermEntity.TrmFllbckTrmId), term.TrmFllbckTrmId.ToString() },
            {nameof(TermEntity.TrmDscntDyNmbr), term.TrmDscntDyNmbr.ToString() },
            {nameof(TermEntity.TrmSlsInvceCmmnts), term.TrmSlsInvceCmmnts },
            {nameof(TermEntity.CalendarId), term.CalendarId.ToString() },
            {nameof(TermEntity.DueDateSaturdayRule), term.DueDateSaturdayRule },
            {nameof(TermEntity.DueDateSundayRule), term.DueDateSundayRule },
            {nameof(TermEntity.DueDateMondayHolidayRule), term.DueDateMondayHolidayRule },
            {nameof(TermEntity.DueDateHolidayRule), term.DueDateHolidayRule },
            {nameof(TermEntity.PastDueCalculation), term.PastDueCalculation },
            {nameof(TermEntity.PastDueCalculationNumberDays), term.PastDueCalculationNumberDays.ToString() },
            {nameof(TermEntity.PastDueCalendarId), term.PastDueCalendarId.ToString() },
            {nameof(TermEntity.EstimatedDueDateCalculation), term.EstimatedDueDateCalculation },
            {nameof(TermEntity.EstimatedDueDateOffset), term.EstimatedDueDateOffset.ToString() },
            {nameof(TermEntity.Comment), term.Comment },
        };

    }
}
