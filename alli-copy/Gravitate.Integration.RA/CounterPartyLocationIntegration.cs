using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class CounterPartyLocationIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CounterPartyLocationIntegration));

        private readonly IQueryable<OfficeEntity> _officeQueryable;
        private readonly ICounterPartyLocationIntegrationService _cplIntegrationService;

        public CounterPartyLocationIntegration(
            IQueryable<OfficeEntity> baQueryable,
            ICounterPartyLocationIntegrationService cplIntegrationService
            )
        {
            _officeQueryable = baQueryable;
            _cplIntegrationService = cplIntegrationService;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
            OfficeEntity[] raOffices = _officeQueryable.WithPath(PrefetchLocale, PrefetchAddress).ToArray();

            Log.Info($"RA Office query took {sw.Elapsed.ToString()} for {raOffices.Count()} Offices");

            CounterPartyLocationIntegrationDTO[] dtos = raOffices.Select(raOffice => TranslateEntity(raOffice, sourceSystemId)).ToArray();

            Log.Info("Calling BulkSyncCounterPartyLocations");
            _cplIntegrationService.BulkSyncCounterPartyLocations(sourceSystemId, dtos);
            Log.Info("Completed call to BulkSyncCounterPartyLocations");
        }

        public CounterPartyLocationIntegrationDTO TranslateEntity(OfficeEntity raOffice, int sourceSystemId)
        {
            CounterPartyLocationIntegrationDTO dto = new CounterPartyLocationIntegrationDTO();

            dto.Name = raOffice.Locale.LcleNme;
            dto.SourceId = raOffice.OffceLcleId;

            SetContactInfo(dto, raOffice);

            dto.CounterPartyLookup = new RelatedEntityLookup() { SourceId = raOffice.OffceBaid, SourceSystemId = sourceSystemId };
            dto.LocationLookup = new RelatedEntityLookup() { SourceId = raOffice.OffceLcleId, SourceSystemId = sourceSystemId };

            return dto;
        }

        protected virtual void SetContactInfo(CounterPartyLocationIntegrationDTO dto, OfficeEntity raOffice)
        {
            dto.ContactInfoDTO = new ContactInfoDTO()
            {
                ContactName = dto.Name
            };

            SetPhoneDtOs(dto.ContactInfoDTO, raOffice);
            SetAddressDtOs(dto.ContactInfoDTO, raOffice);
        }

        protected virtual void SetPhoneDtOs(ContactInfoDTO contactInfoDto, OfficeEntity raOffice)
        {
            if (raOffice.OffceVcePhne != null)
            {
                contactInfoDto.PhoneDTOs =
                    new PhoneDTO[]
                    {
                        new PhoneDTO() {
                            PhoneTypeMeaning = PhoneType.OfficeMeaning,
                            PhoneNumber = raOffice.OffceVcePhne
                        }
                    };
            }
        }

        protected virtual void SetAddressDtOs(ContactInfoDTO contactInfoDto, OfficeEntity raOffice)
        {
            contactInfoDto.AddressDTOs = raOffice.Addresses.Select(raAddress => TranslateEntity(raAddress)).ToArray();
        }

        protected virtual AddressDTO TranslateEntity(AddressEntity raAddress)
        {
            return new AddressDTO()
            {
                AddressTypeMeaning = GetAddressTypeCodeValue(raAddress),
                AddressLine1 = raAddress.AddrssLne1,
                AddressLine2 = raAddress.AddrssLne2,
                City = raAddress.AddrssCty,
                StateCode = raAddress.AddrssStte,
                PostalCode = raAddress.AddrssPstlCde,
                CountryName = raAddress.AddrssCntry
            };
        }

        protected virtual string GetAddressTypeCodeValue(AddressEntity raAddress)
        {
            switch (raAddress.AddrssTpe)
            {
                case "M":
                    return AddressType.OfficeMailingMeaning;
                case "I":
                    return AddressType.InvoiceMeaning;
                case "R":
                    return AddressType.RemitToMeaning;
                case "S":
                    return AddressType.ShippingMeaning;
                default:
                    Log.Warn("Unable to translate address type: " + raAddress.AddrssTpe);
                    return null;
            }
        }

        public static IPathEdge PrefetchLocale => new PathEdge<LocaleEntity>(OfficeEntity.PrefetchPathLocale);

        public static IPathEdge PrefetchAddress => new PathEdge<LocaleEntity>(OfficeEntity.PrefetchPathAddresses);
    }
}
