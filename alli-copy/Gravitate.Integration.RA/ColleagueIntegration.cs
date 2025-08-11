using Gravitate.Domain.Adapter.DAL;
using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Integration.RA.DAL.HelperClasses;
using Gravitate.Library;
using Gravitate.Library.Caching;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class ColleagueIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ColleagueIntegration));

        private readonly IQueryable<ContactEntity> _contactQueryable;
        private readonly IColleagueIntegrationService _colleagueIntegrationService;
        private readonly IQueryable<GeneralConfigurationEntity> _gcQueryable;

        protected ILookup<int, int> primaryBusinessOwnerBAIdLookup;


        public ColleagueIntegration(
                IQueryable<ContactEntity> contactQueryable,
                IColleagueIntegrationService colleagueIntegrationService, IQueryable<GeneralConfigurationEntity> gcQueryable)
        {
            _contactQueryable = contactQueryable;
            _colleagueIntegrationService = colleagueIntegrationService;
            _gcQueryable = gcQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
            ContactEntity[] raContacts = _contactQueryable.WithPath(PrefetchUsers, PrefetchContactRoles).ToArray(); //PrefetchGeneralConfig, 
            Log.Info($"RA contact query took {sw.Elapsed.ToString()} for {raContacts.Count()} contacts");

            ColleagueIntegrationDTO[] colDtOs = raContacts.Select(raContact => TranslateEntity(raContact, sourceSystemId)).ToArray();

            Log.Info("Calling BulkSyncColleagues");
            _colleagueIntegrationService.BulkSyncColleagues(sourceSystemId, colDtOs);
            Log.Info("Completed call to BulkSyncColleagues");
            ColleagueCache.Instance.Clear(CacheClearAction.ReloadLocal);
        }

        public ColleagueIntegrationDTO TranslateEntity(ContactEntity raContact, int sourceSystemId)
        {
            var userId = raContact.Users.FirstOrDefault()?.UserId;

            var colleagueDto = new ColleagueIntegrationDTO
            {
                SourceId = raContact.CntctId,
                FirstName = raContact.CntctFrstNme,
                LastName = raContact.CntctLstNme,
                Email = raContact.CntctEmlAddrss,
                Phone = raContact.CntctVcePhne,
                InternalCounterPartyLookup = new RelatedEntityLookup() { SourceId = raContact.CntctBaid, SourceSystemId = sourceSystemId },
                //Changing this to derive from Internal and External only
                //CounterPartyLookup = new RelatedEntityLookup() { SourceId = raContact.CntctBaid, SourceSystemId = sourceSystemId };
                ExternalCounterPartyLookup = new RelatedEntityLookup() { SourceId = raContact.CntctBaid, SourceSystemId = sourceSystemId },
                IsActive = (raContact.CntctStts == RAStatus.Active),
                ContactInfoDTO = GetContactInfo(raContact),
                CustomProperties = new Dictionary<string, string>() { { "RAUserId", Convert.ToString(userId) } },
                Duties = raContact.ContactRoles.Any(cr => cr.CntctRleTpe == "MT" || cr.CntctRleTpe == "MW") ? new string[] { "Trading" } : null // TODO: Enable when we set up QDE

            };

            var primaryBusinessOwnerBAIds = userId != null ? GetPrimaryBusinessOwnerBAIds(userId.Value) : null;
            if (primaryBusinessOwnerBAIds.Any(false))
            {
                colleagueDto.ColleagueCounterPartyRelationships =
                    new[]
                    {
                        new ColleagueCounterPartyIntegrationDTO()
                        {
                            RelationshipMeaning = ColleagueRelationshipType.PrimaryBusinessOwnerMeaning,
                            RelatedCounterpartyLookups =
                                primaryBusinessOwnerBAIds?.Select(
                                    baid => new RelatedEntityLookup()
                                    {
                                        SourceSystemId = sourceSystemId,
                                        SourceId = baid
                                    }).ToArray()
                        }
                    };
            }

            SetCustomValues(raContact, colleagueDto);

            return colleagueDto;
        }

        protected IEnumerable<int> GetPrimaryBusinessOwnerBAIds(int userId)
        {
            primaryBusinessOwnerBAIdLookup =
                primaryBusinessOwnerBAIdLookup ??
                 _gcQueryable
                     .Where(gc => gc.GnrlCnfgMulti != null && gc.GnrlCnfgHdrId != 0 && gc.GnrlCnfgTblNme == "BusinessAssociate" && gc.GnrlCnfgQlfr.Equals("PrimaryBusinessOwner", StringComparison.InvariantCultureIgnoreCase))
                     .ToLookup(gc => Convert.ToInt32(gc.GnrlCnfgMulti), gc => gc.GnrlCnfgHdrId);

            return primaryBusinessOwnerBAIdLookup[userId];
        }

        private ContactInfoDTO GetContactInfo(ContactEntity raContact)
        {
            var phones = new List<PhoneDTO>();
            if (!raContact.CntctVcePhne.IsNullOrWhiteSpace())
            {
                phones.Add(new PhoneDTO() { PhoneNumber = raContact.CntctVcePhne, PhoneTypeMeaning = PhoneType.OfficeMeaning });
            }
            if (!raContact.CntctFxPhne.IsNullOrWhiteSpace())
            {
                phones.Add(new PhoneDTO() { PhoneNumber = raContact.CntctFxPhne, PhoneTypeMeaning = PhoneType.FaxMeaning });
            }
            return phones.Any() ? new ContactInfoDTO() { PhoneDTOs = phones.ToArray() } : null;
        }

        private void SetCustomValues(ContactEntity contact, ColleagueIntegrationDTO colDto)
        {
            //Need to pull this into LLBLGenPro
            //ColDTO.CustomDatabaseValues = GetGeneralConfigAttributes(contact.GeneralConfigurations);
        }

        private Dictionary<string, string> GetGeneralConfigAttributes(EntityCollection<GeneralConfigurationEntity> entityCollection)
        {
            //Documentations says entityCollection should be empty not null
            return entityCollection.ToDictionary(key => key.GnrlCnfgQlfr.ToUpper().ToUpper(), value => value.GnrlCnfgMulti);
        }

        //public static IPathEdge PrefetchGeneralConfig
        //{
        //    get
        //    {
        //        return new PathEdge<GeneralConfigurationEntity>(
        //            ContactEntity.PrefetchPathGeneralConfigurations,
        //            gc =>
        //                gc.GnrlCnfgTblNme == GeneralConfigurationDataConstants.Contact.GnrlCnfgTblNme
        //            //&& gc.GnrlCnfgQlfr == ""//GeneralConfigurationDataConstants.BusinessAssociate.GnrlCnfgQlfr.Something
        //            );
        //    }
        //}

        public static IPathEdge PrefetchUsers => new PathEdge<UserEntity>(ContactEntity.PrefetchPathUsers);

        public static IPathEdge PrefetchContactRoles => new PathEdge<UserEntity>(ContactEntity.PrefetchPathContactRoles);

    }
}
