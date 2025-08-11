using Castle.Core.Internal;
using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class CalendarIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CurrencyIntegration));

        private IQueryable<CalendarEntity> calendarEntities;
        private ICalendarIntegrationService _calendarIntegrationService;

        public CalendarIntegration(IQueryable<CalendarEntity> calendarEntities, ICalendarIntegrationService calendarIntegrationService)
        {
            this.calendarEntities = calendarEntities;
            _calendarIntegrationService = calendarIntegrationService;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
            CalendarEntity[] calendars = QueryRaCalendars();
            Log.Info($"RA calendar query took {sw.Elapsed.ToString()} for {calendars.Count()} calendars");
            CalendarIntegrationDTO[] currencyDtOs = calendars.Select(TranslateEntity).ToArray();

            Log.Info($"Calling {nameof(_calendarIntegrationService.BulkSyncCalendars)}");
            _calendarIntegrationService.BulkSyncCalendars(sourceSystemId, currencyDtOs);
            Log.Info($"Completed call to {nameof(_calendarIntegrationService.BulkSyncCalendars)}");
        }


        private CalendarEntity[] QueryRaCalendars()
        {
            var calendarIds = GetIntArray("CalendarIds", false);

            var query = calendarEntities.WithPath(CalendarDateEntity, HolidayEntity);
            if (!calendarIds.IsNullOrEmpty())
            {
                query = query.Where(x => calendarIds.Contains(x.CalendarId));
            }

            return query.ToArray();
        }

        public CalendarIntegrationDTO TranslateEntity(CalendarEntity entity)
        {
            CalendarIntegrationDTO dto = new CalendarIntegrationDTO
            {
                DateGranularity = TimeUnit.Second,
                CalendarName = entity.Name,
                CalendarTypeMeaning = null,
                SourceId = entity.CalendarId,
                DaysByType = new[]
                {
                    GetCalendarDates(entity),
                    GetHolidayDates(entity)
                }

            };
            FilterDates(dto);
            return dto;
        }

        private void FilterDates(CalendarIntegrationDTO dto)
        {
            var start = GetDateTime("LoadStartDate", false);
            if (start.HasValue)
            {
                foreach (var periodSetIntegrationDto in dto.DaysByType)
                {
                    periodSetIntegrationDto.PeriodDTOs = periodSetIntegrationDto.PeriodDTOs
                            .Where(x => x.PeriodFromDateTime >= start).ToArray();
                }
            }
        }

        public PeriodSetIntegrationDTO GetCalendarDates(CalendarEntity entity)
        {
            return new PeriodSetIntegrationDTO()
            {
                PeriodTypeMeaning = PeriodType.CalendarEntryMeaning,
                PeriodDTOs = entity.CalendarDates.Select(TranslateEntity).ToArray(),
            };
        }

        public PeriodSetIntegrationDTO GetHolidayDates(CalendarEntity entity)
        {
            return new PeriodSetIntegrationDTO()
            {
                PeriodTypeMeaning = PeriodType.CalendarHolidayMeaning,
                PeriodDTOs = entity.Holidays.Select(TranslateEntity).ToArray(),
            };
        }

        public PeriodIntegrationDTO TranslateEntity(HolidayEntity date)
        {
            return new PeriodIntegrationDTO()
            {
                Name = date.Name.IsNullOrWhiteSpace() ? $"Holiday: {date.CalendarDate:D}" : date.Name,
                PeriodFromDateTime = date.CalendarDate.BeginningOf(TimeUnit.Day),
                PeriodToDateTime = date.CalendarDate.EndOf(TimeUnit.Day),
            };
        }

        public PeriodIntegrationDTO TranslateEntity(CalendarDateEntity date)
        {
            return new PeriodIntegrationDTO()
            {
                Name = $"{date.Date:D}",
                PeriodFromDateTime = date.Date.BeginningOf(TimeUnit.Day),
                PeriodToDateTime = date.Date.EndOf(TimeUnit.Day),
            };
        }

        #region Prefetches

        private IPathEdge CalendarDateEntity => new PathEdge<CalendarDateEntity>(CalendarEntity.PrefetchPathCalendarDates);
        private IPathEdge HolidayEntity => new PathEdge<HolidayEntity>(CalendarEntity.PrefetchPathHolidays);

        #endregion


    }
}