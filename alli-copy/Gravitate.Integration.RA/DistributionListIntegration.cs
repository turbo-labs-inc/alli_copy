using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class DistributionListIntegration : BaseIntegration
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DistributionListIntegration));

        private readonly IQueryable<DealDetailEntity> _dealDetailQueryable;
        private readonly IQueryable<GeneralConfigurationEntity> _generalConfigQueryable;
        private readonly IQueryable<ContactEntity> _contactQueryable;
        private IQueryable<DealHeaderEntity> _dealHeaderQueryable;

        private readonly IDistributionListIntegrationService distributionListIntegrationService;

        private string SendGravitateMessagesQualifier => GetIntegrationSetting("SendGravitateMessagesQualifier");
        private string ProductGroupQualifier => GetIntegrationSetting("ProductGroupQualifier");

        private int sourceSystemId = -1;

        public DistributionListIntegration(IQueryable<DealDetailEntity> dealDetailQueryable, IQueryable<GeneralConfigurationEntity> generalConfigQueryable, IQueryable<ContactEntity> contactQueryable, IQueryable<DealHeaderEntity> dealHeaderQueryable, IDistributionListIntegrationService distributionListIntegrationService)
        {
            this._dealDetailQueryable = dealDetailQueryable;
            this._generalConfigQueryable = generalConfigQueryable;
            this._contactQueryable = contactQueryable;
            this._dealHeaderQueryable = dealHeaderQueryable;
            this.distributionListIntegrationService = distributionListIntegrationService;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            this.sourceSystemId = sourceSystemId;

            //var distributionDtoList = GroupByName(GetDistributionLists()).ToArray();
            var distributionDtoList = LoadDynamicLists().ToArray();

            foreach (var distributionListIntegrationDto in distributionDtoList)
            {
                distributionListIntegrationDto.DistributionListType = CodeSetFacade.Instance.DistributionListType.Extract.CodeValueId;
            }
            log.Info($"Sending {distributionDtoList.Length} lists with {distributionDtoList.Sum(x => x.ColleagueLookups.Count())} members ");
            distributionListIntegrationService.CreateOrUpdateDistributionLists(sourceSystemId, distributionDtoList);
        }

        private ILookup<int, string> _productGroupLookup;
        public ILookup<int, string> ProductGroupLookup => (ProductGroupQualifier != null ? _productGroupLookup = _productGroupLookup ?? _generalConfigQueryable.Where(gc => gc.GnrlCnfgTblNme == "Product" && gc.GnrlCnfgQlfr == ProductGroupQualifier).ToLookup(gc => Convert.ToInt32(gc.GnrlCnfgHdrId), gc => gc.GnrlCnfgMulti) : null);

        private ILookup<int, int> _messagingDisabledLookup;
        public ILookup<int, int> MessagingDisabledLookup => (SendGravitateMessagesQualifier != null ? _messagingDisabledLookup = _messagingDisabledLookup ?? _generalConfigQueryable.Where(gc => gc.GnrlCnfgTblNme == "Contact" && gc.GnrlCnfgQlfr == SendGravitateMessagesQualifier && gc.GnrlCnfgMulti == "n").ToLookup(gc => Convert.ToInt32(gc.GnrlCnfgHdrId), gc => gc.GnrlCnfgHdrId) : null);

        public IEnumerable<DistributionListIntegrationDTO> LoadDynamicLists()
        {
            var jsonConfigString = base.GetIntegrationSetting("DynamicListConfig");
            return LoadDynamicLists(jsonConfigString);
        }

        public IEnumerable<DistributionListIntegrationDTO> LoadDynamicLists(string jsonConfigString)
        {
            var configArray = JsonConvert.DeserializeObject<DistributionListConfig[]>(jsonConfigString);

            foreach (var config in configArray)
            {
                var dtos = LoadDynamicLists(config);
                foreach (var dto in dtos) yield return dto;
            }
        }

        public IEnumerable<DistributionListIntegrationDTO> LoadDynamicLists(DistributionListConfig config)
        {
            var now = DateTime.Now;

            //always filter to ones that are currently effective and active (for now...)
            var detailQueryable = _dealDetailQueryable.Where(dd => (dd.DlDtlFrmDte <= now && dd.DlDtlToDte >= now) && dd.DlDtlStat == RAStatus.Active && dd.DealHeader.DlHdrStat == RAStatus.Active);

            if (config.DealTypeFilters.Any(false)) detailQueryable = detailQueryable.Where(dd => config.DealTypeFilters.Contains(dd.DealHeader.DlHdrTyp.Value));
            if (config.InternalBAIDFilters.Any(false)) detailQueryable = detailQueryable.Where(dd => config.InternalBAIDFilters.Contains(dd.DealHeader.DlHdrIntrnlBaid));
            if (config.LocaleTypeFilters.Any(false)) detailQueryable = detailQueryable.Where(dd => config.LocaleTypeFilters.Contains(dd.Locale.LocaleType.LcleTpeDscrptn));
            if (config.ProductIdFilters.Any(false)) detailQueryable = detailQueryable.Where(dd => config.ProductIdFilters.Contains(dd.DlDtlPrdctId));
            if (config.ReceiveOrDeliverFilter != null) detailQueryable = detailQueryable.Where(dd => dd.DlDtlSpplyDmnd == config.ReceiveOrDeliverFilter);

            var results = detailQueryable.Select(
                dd => new DistListResult()
                {
                    ProductId = dd.DlDtlPrdctId,
                    ProductName = dd.Product.PrdctNme,
                    LocationId = dd.DlDtlLcleId,
                    LocationName = dd.Locale.LcleNme,
                    InternalBAId = dd.DealHeader.DlHdrIntrnlBaid,
                    InternalBAName = dd.DealHeader.InternalBusinessAssociate.Banme,
                    ExternalBAId = dd.DealHeader.DlHdrExtrnlBaid,
                    ExternalBAName = dd.DealHeader.ExternalBusinessAssociate.Banme
                }).ToList();

            if (config.ProductGroupFilters.Any(false)) results = results.Where(t => config.ProductGroupFilters.Contains(_productGroupLookup[t.ProductId].FirstOrDefault())).ToList();

            if (config.GroupByProductGroup)
            {
                if (ProductGroupQualifier == null)
                    throw new Exception("Product group qualifier is required when grouping by ProductGroup.");

                foreach (var t in results)
                {
                    t.ProductGroup = ProductGroupLookup[t.ProductId].FirstOrDefault();
                }
            }

            //blank out the fields we're not grouping by using ternary operators
            var groupResults = results.GroupBy(x =>
                new
                {
                    ProductId = config.GroupByProduct ? x.ProductId : 0,
                    ProductName = config.GroupByProduct ? x.ProductName : null,
                    ProductGroup = config.GroupByProductGroup ? x.ProductGroup : null,
                    LocationId = config.GroupByLocation ? x.LocationId : 0,
                    LocationName = config.GroupByLocation ? x.LocationName : null,
                    InternalBAId = config.GroupByInternalBA ? x.InternalBAId : 0,
                    InternalBAName = config.GroupByInternalBA ? x.InternalBAName : null,
                    ExternalBAName = config.GroupByExternalBA ? x.ExternalBAName : null,
                    ExternalBAId = config.GroupByExternalBA ? x.ExternalBAId : 0
                }
            );

            var allContacts = _contactQueryable.Select(c => new { c.CntctBaid, c.CntctId }).ToList();

            if (MessagingDisabledLookup != null) allContacts = allContacts.Where(c => !MessagingDisabledLookup.Contains(c.CntctId)).ToList();

            var contactLookup = allContacts.ToLookup(c => c.CntctBaid, c => c.CntctId);

            config.ValidateAndUpdateFormatString(); //prep and validate this before we iterate over the results

            foreach (var group in groupResults)
            {
                var dyn = (group as dynamic);

                var formatParams = new List<object>();

                if (config.GroupByProduct) formatParams.Add(group.Key.ProductName);
                if (config.GroupByProductGroup) formatParams.Add(group.Key.ProductGroup);
                if (config.GroupByLocation) formatParams.Add(group.Key.LocationName);
                if (config.GroupByInternalBA) formatParams.Add(group.Key.InternalBAName);
                if (config.GroupByExternalBA) formatParams.Add(group.Key.ExternalBAName);

                string listName = String.Format(config.Format, formatParams.ToArray());

                var dto = new DistributionListIntegrationDTO()
                {
                    DistributionListName = listName,
                    Roles = config.Roles
                };

                dto.ColleagueLookups = group.Select(g => g.ExternalBAId).Distinct().SelectMany(baid => contactLookup[baid].Select(contactId => new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = contactId }));

                yield return dto;
            }
        }

        public class DistributionListConfig
        {
            public int[] DealTypeFilters { get; set; }
            public int[] InternalBAIDFilters { get; set; }
            public string[] LocaleTypeFilters { get; set; }
            public string[] ProductGroupFilters { get; set; }
            public int[] ProductIdFilters { get; set; }
            public string ReceiveOrDeliverFilter { get; set; }

            public bool GroupByProductGroup { get; set; }
            public bool GroupByProduct { get; set; }
            public bool GroupByLocation { get; set; }
            public bool GroupByInternalBA { get; set; }
            public bool GroupByExternalBA { get; set; }

            public string[] Roles { get; set; }

            public string Format { get; set; }

            public void ValidateAndUpdateFormatString()
            {
                int i = 0;
                if (GroupByProduct) Format = Format.Replace("{ProductName}", $"{{{i++}}}");
                else if (Format.Contains("{ProductName}")) throw new Exception("You can only include ProductName in the format when GroupByProduct is true.");

                if (GroupByProductGroup) Format = Format.Replace("{ProductGroup}", $"{{{i++}}}");
                else if (Format.Contains("{ProductGroup}")) throw new Exception("You can only include ProductGroup in the format when GroupByProductGroup is true.");

                if (GroupByLocation) Format = Format.Replace("{LocationName}", $"{{{i++}}}");
                else if (Format.Contains("{LocationName}")) throw new Exception("You can only include LocationName in the format when GroupByLocation is true.");

                if (GroupByInternalBA) Format = Format.Replace("{InternalBAName}", $"{{{i++}}}");
                else if (Format.Contains("{ProductName}")) throw new Exception("You can only include InternalBAName in the format when GroupByInternalBA is true.");

                if (GroupByExternalBA) Format = Format.Replace("{ExternalBAName}", $"{{{i++}}}");
                else if (Format.Contains("{ExternalBAName}")) throw new Exception("You can only include InternalBAName in the format when GroupByExternalBA is true.");
            }
        }

        public class DistListResult
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public int LocationId { get; set; }
            public string LocationName { get; set; }
            public string ProductGroup { get; set; }
            public int InternalBAId { get; set; }
            public string InternalBAName { get; set; }
            public int ExternalBAId { get; set; }
            public string ExternalBAName { get; set; }
        }
    }
}