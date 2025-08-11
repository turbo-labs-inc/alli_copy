using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class LocationIntegration : BaseIntegration
    {
        //These need to be lower case and trimmed
        private IDictionary<string, string> _pricingAndTradingLocaleDictionary;

        private static readonly ILog Log = LogManager.GetLogger(typeof(LocationIntegration));

        private readonly IQueryable<LocaleEntity> _localeQueryable;
        private readonly IQueryable<VAesGravBookLocationAssociationEntity> _locationBookQuerable;
        private readonly ILocationIntegrationService _locationIntegrationService;

        //Get the first elecment of the group by into the dictionary
        private IDictionary<int, int> LocaleStrategy => LazyMember(() => _locationBookQuerable.GroupBy(l=>l.LocaleId).ToList().Select(g=>g.First()).ToDictionary(key => key.LocaleId, value => value.BookSourceId));

        private IDictionary<int, LocaleEntity> LocaleDictionary;

        public LocationIntegration(IQueryable<LocaleEntity> localeQueryable, IQueryable<VAesGravBookLocationAssociationEntity> locationStrategyQueryable, ILocationIntegrationService locationIntegrationService)
        {
            _localeQueryable = localeQueryable;
            _locationIntegrationService = locationIntegrationService;
            _locationBookQuerable = locationStrategyQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
            _pricingAndTradingLocaleDictionary = base.GetStringArray("PricingAndTradingLocaleTypes", true).Select(x => x.Trim().ToLower()).ToDictionary(key => key);
            var localeTypeIdsToExclude = base.GetIntArray("LocaleTypeIdsToExclude");
            var raLocations = _localeQueryable.WithPath(PrefetchLocationType, PrefetchLocaleGeneralConfig).Where(x => !localeTypeIdsToExclude.Contains(x.LcleTpeId)).ToArray();
            LocaleDictionary = raLocations.ToDictionary(key => key.LcleId, value => value);
            Log.Info($"RA location query took {sw.Elapsed} for {raLocations.Length} locations");
            var locationDtOs = raLocations.Select(x => TranslateEntity(x, sourceSystemId)).ToArray();

            Log.Info("Calling BulkSyncLocations");
            _locationIntegrationService.BulkSyncLocations(sourceSystemId, locationDtOs);
            Log.Info("Completed call to BulkSyncLocations");
        }

        public LocationIntegrationDTO TranslateEntity(LocaleEntity raLocation, int sourceSystemId)
        {
            LocationIntegrationDTO locationDto = new LocationIntegrationDTO();

            locationDto.SourceId = raLocation.LcleId;
            locationDto.Name = raLocation.LcleNme;
            GetAbbreviation(raLocation, locationDto);
            locationDto.CountryCode = "";
            locationDto.Longitude = null;
            locationDto.Latitude = null;
            locationDto.TerminalName = null;
            locationDto.IsPricingLocation = false;
            locationDto.IsActive = raLocation.LcleStts != RAStatus.Inactive;
            SetCustomValues(locationDto, raLocation, sourceSystemId);

            return locationDto;
        }

        private static void GetAbbreviation(LocaleEntity raLocation, LocationIntegrationDTO locationDto)
        {
            var lcleAbbr = (raLocation.LcleAbbrvtn ?? "").Trim();
            var lcleAbbrExtension = (raLocation.LcleAbbrvtnExtension ?? "").Trim();
            var abbreviation = ($"{lcleAbbr}{lcleAbbrExtension}");
            locationDto.Abbreviation = abbreviation.Length > 50 ? abbreviation.Substring(0, 50) : abbreviation;
        }

        private void SetCustomValues(LocationIntegrationDTO locationDto, LocaleEntity raLocation, int sourceSystemId)
        {
            
            if (LocaleStrategy.ContainsKey(raLocation.LcleId))
            {
                locationDto.BookLookup = new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = LocaleStrategy[raLocation.LcleId] };
            }

            if (_pricingAndTradingLocaleDictionary.ContainsKey(raLocation.LocaleType.LcleTpeDscrptn.Trim().ToLower()) || (locationDto.IsActive && locationDto.BookLookup != null))
            {
                locationDto.LocationTypeMeaning = LocationType.TradingLocationMeaning;
                locationDto.IsPricingLocation = true;
            }

            //JCM-TODO
            //if (LocaleParentLocale.ContainsKey(raLocation.LcleId))
            //{
            //    var basePricePublisher = LocaleDictionary[LocaleParentLocale[raLocation.LcleId]].GeneralConfigurations.FirstOrDefault(gc => gc.GnrlCnfgQlfr == "BasePriceService")?.GnrlCnfgMulti;
            //    var basePriceLocation = LocaleDictionary[LocaleParentLocale[raLocation.LcleId]].GeneralConfigurations.FirstOrDefault(gc => gc.GnrlCnfgQlfr == "BasePriceLocation")?.GnrlCnfgMulti;
            //    locationDto.CustomProperties.Add("BasePricePublisher", basePricePublisher);
            //    locationDto.CustomProperties.Add("BasePriceLocation", basePriceLocation);
            //}
        }

        public IPathEdge PrefetchLocationType => new PathEdge<LocaleTypeEntity>(LocaleEntity.PrefetchPathLocaleType);
        public IPathEdge PrefetchLocaleGeneralConfig => GetGeneralConfigPrefetches(LocaleEntity.PrefetchPathGeneralConfigurations, "Locale");
    }
}
