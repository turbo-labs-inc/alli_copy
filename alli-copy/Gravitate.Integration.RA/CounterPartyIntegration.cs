using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL;
using Gravitate.Integration.RA.DAL.DatabaseGeneric;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Integration.RA.DAL.HelperClasses;
using Gravitate.Library;
using Gravitate.Library.CodeSets.DALFacade;
using Gravitate.Library.Integration;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using MoreLinq;
using SD.LLBLGen.Pro.LinqSupportClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class CounterPartyIntegration : BaseIntegration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CounterPartyIntegration));


        private readonly IQueryable<BusinessAssociateEntity> _baQueryable;
        private readonly IQueryable<UserEntity> _userQueryable;
        private readonly IQueryable<DynamicListBoxEntity> _dynamicListBoxQueryable;
        private readonly ICounterPartyIntegrationService _cpIntegrationService;
        //private readonly IQueryable<VCustomBusinessAssociateCreditUsageEntity> _vBACredit;
        private readonly IQueryable<VAesBusinessAssociateCreditUsageEntity> _vBACredit;
        private readonly string[] _relationTypes;
        private Dictionary<string, ContactEntity> _contactByUserIdStringDictionary;
        private ILookup<int, BACreditProjection> _baCreditLookup;
        private readonly IQueryable<VCustomGravitatePaymentTermsTruckBulkEntity> _raPaymentTermsTruckBulkQueryable;

        public CounterPartyIntegration(
            IQueryable<BusinessAssociateEntity> baQueryable,
            IQueryable<UserEntity> userQueryable,
            IQueryable<DynamicListBoxEntity> dynamicListBoxQueryable,
            ICounterPartyIntegrationService cpIntegrationService,
            IQueryable<VAesBusinessAssociateCreditUsageEntity> vBaCredit,
            IQueryable<VCustomGravitatePaymentTermsTruckBulkEntity> raPaymentTermsTruckBulkQueryable
            )
        {
            _baQueryable = baQueryable;
            _userQueryable = userQueryable;
            _dynamicListBoxQueryable = dynamicListBoxQueryable;
            _cpIntegrationService = cpIntegrationService;
            _vBACredit = vBaCredit;
            _relationTypes = base.GetStringArray("TradeRelationList");
            _raPaymentTermsTruckBulkQueryable = raPaymentTermsTruckBulkQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            _contactByUserIdStringDictionary = MoreEnumerable.DistinctBy(_userQueryable.WithPath(PrefetchContactUsers), x => x.UserId)
                .ToDictionary(key => key.UserId.ToString(), value => value.Contact);

            var creditInternalBAId = base.GetInt("BACreditLimitInternalBaId", true);

            
            var baCreditProjections = _vBACredit.Where(cred => cred.InternalBaid == creditInternalBAId).Select(cred => new BACreditProjection() { InternalBaId = cred.InternalBaid, ExternalBaId = cred.ExternalBaid, CreditLimit = Convert.ToDecimal(cred.CreditLimit), ArBalance = cred.TotalAmountRemaining, MultipleRelatedBAs = (cred.RelatedBacount > 1) }).ToList();
            _baCreditLookup = baCreditProjections.ToLookup(ar => ar.ExternalBaId);

            Stopwatch sw = Stopwatch.StartNew();
            SyncRelationTypes(sourceSystemId, sw);

            sw = Stopwatch.StartNew();
            BusinessAssociateEntity[] raBas = _baQueryable
                                                    .WithPath(
                                                        PrefetchGeneralConfig,
                                                        PrefetchPathBusinessAssociateBrokerDefault,
                                                        PrefetchPathBusinessAssociateRelation,
                                                        PrefetchPathBusinessAssociateCreditCustomers,
                                                        PrefetchPathUltimateBusinessAssociateCreditCustomers)
                                                    .ToArray();
            Log.Info($"RA ba query took {sw.Elapsed.ToString()} for {raBas.Count()} bas");

            CounterPartyIntegrationDTO[] cpDtOs = raBas.Select(raBa => TranslateEntity(raBa, sourceSystemId)).ToArray();

            Log.Info("Calling BulkSyncCounterParties");
            _cpIntegrationService.BulkSyncCounterParties(sourceSystemId, cpDtOs);
            Log.Info("Completed call to BulkSyncCounterParties");
        }


        private void SyncRelationTypes(int sourceSystemId, Stopwatch sw)
        {
            var listBoxes = _dynamicListBoxQueryable.Where(dlb => dlb.DynLstBxQlfr.Trim() == "BusinessRelation" && dlb.DynLstBxStts != RAStatus.Inactive)
                            .ToArray();

            Log.Info($"Business Relation Dynamic List Box query took {sw.Elapsed.ToString()} for {listBoxes.Count()}");

            var listBoxGroups = listBoxes.GroupBy(key => key.DynLstBxTyp);

            CounterPartyProfileIntegrationDTO[] cppDtOs = listBoxGroups.Select(group =>
            {
                var item = group.First();
                if (group.Count() > 1)
                {
                    Log.Warn($"Business Relation with Qualifier {group.Key} appeared more than once. " +
                             $"Selecting: {item.DynLstBxDesc} from: {String.Join(", ", group.Select(x => x.DynLstBxDesc))}");
                }

                return TranslateBusinessRelationDynamicListBox(item, sourceSystemId);
            }).ToArray();

            Log.Info("Calling BulkSyncCounterPartyRelationTypes");
            _cpIntegrationService.BulkSyncCounterPartyProfiles(sourceSystemId, cppDtOs);
            Log.Info("Completed call to BulkSyncCounterPartyRelationTypes");

        }

        public CounterPartyProfileIntegrationDTO TranslateBusinessRelationDynamicListBox(DynamicListBoxEntity listBox, int sourceSystemId)
        {
            CounterPartyProfileIntegrationDTO cpDto = new CounterPartyProfileIntegrationDTO();
            cpDto.SourceIdString = listBox.DynLstBxTyp;
            cpDto.Abbreviation = listBox.DynLstBxAbbv;
            cpDto.Description = listBox.DynLstBxDesc;
            return cpDto;
        }
        private Dictionary<string, string> GetGeneralConfigAttributes(EntityCollection<GeneralConfigurationEntity> entityCollection)
        {
            //Documentations says entityCollection should be empty not null
            return entityCollection.DistinctBy(x => x.GnrlCnfgQlfr).ToDictionary(key => key.GnrlCnfgQlfr.ToUpper().ToUpper(), value => value.GnrlCnfgMulti);
        }


        public CounterPartyIntegrationDTO TranslateEntity(BusinessAssociateEntity raBa, int sourceSystemId)
        {
            CounterPartyIntegrationDTO cpDto = new CounterPartyIntegrationDTO();

            cpDto.SourceId = raBa.Baid;
            cpDto.Domain = null;
            cpDto.HasTradeRelationship = raBa.BusinessAssociateRelations.Any(bar => _relationTypes.Contains(bar.BarltnRltn));
            cpDto.CategoryMeaning = GetCounterPartyCategoryName(raBa);
            cpDto.Name = cpDto.CategoryMeaning == CounterpartyCategory.InternalMeaning ? raBa.ContractCode : raBa.Name;
            cpDto.Abbreviation = cpDto.CategoryMeaning == CounterpartyCategory.InternalMeaning ? raBa.ContractCode : raBa.Abbreviation;
            cpDto.ParentCounterPartyLookup = raBa.BaprntBaid != raBa.Baid ? new RelatedEntityLookup() { SourceSystemId = sourceSystemId, SourceId = raBa.BaprntBaid } : null;
            cpDto.IsActive = (raBa.Bastts == RAStatus.Active);
            cpDto.CounterPartyCreditDto = GetCreditDto(raBa, sourceSystemId);
            cpDto.CustomProperties = GetGeneralConfigAttributes(raBa.GeneralConfigurations);

            //BCA 20240809 -- We want all customers with a trade relationship to load data into the portal...
            cpDto.HasCustomerPortal = cpDto.HasTradeRelationship; // raBa.GeneralConfigurations.FirstOrDefault(gc => gc.GnrlCnfgQlfr.Equals("ActivatePortal", StringComparison.CurrentCultureIgnoreCase))?.GnrlCnfgMulti == "Y";
            SetCustomProperties(raBa, cpDto);

            cpDto.ProfileAssociations = new List<RelatedEntityLookup>();
            foreach (var raBaBusinessAssociateRelation in raBa.BusinessAssociateRelations)
            {
                cpDto.ProfileAssociations.Add(new RelatedEntityLookup()
                {
                    SourceSystemId = sourceSystemId,
                    SourceIdString = raBaBusinessAssociateRelation.BarltnRltn
                });
            }
            return cpDto;
        }



        private CounterPartyCreditDTO GetCreditDto(BusinessAssociateEntity raBa, int sourceSystemId)
        {
            if (raBa.BusinessAssociateCreditCustomers.Any())
            {
                // there should only be one
                var customer = raBa.BusinessAssociateCreditCustomers.First();
                return new CounterPartyCreditDTO()
                {
                    CreditBlock = customer.CreditBlock,
                    Comments = customer.Comments,
                    CreatedDate = customer.CreatedDate,
                    CreditWatch = customer.CreditWatch,
                    NettingEnabled = customer.NettingEnabled,
                    RevisionDate = customer.RevisionDate,
                };
            }
            return null;
        }

        private void SetCustomProperties(BusinessAssociateEntity raBa, CounterPartyIntegrationDTO cpDto)
        {
            //cpDto.CustomDatabaseValues = GetGeneralConfigAttributes(raBa.GeneralConfigurations);
            string primaryBaOwnerUserId;
            ContactEntity primaryBaOwnerContact;
            BACreditProjection baCredit = null;
                        
            if (_baCreditLookup != null)
            {
                var paymentTermsTruck = _raPaymentTermsTruckBulkQueryable.First(x => x.Baid == raBa.Baid && x.Name == "PaymentTermsTruck");
                var paymentTermsBulk = _raPaymentTermsTruckBulkQueryable.First(x => x.Baid == raBa.Baid && x.Name == "PaymentTermsBulk");

                baCredit = _baCreditLookup[raBa.Baid].MinByFirst(ar => ar.InternalBaId);
                cpDto.CustomProperties.Add("CreditLimit", baCredit?.CreditLimit.ToString());
                cpDto.CustomProperties.Add("ARBalance", baCredit?.ArBalance?.ToString());
                cpDto.CustomProperties.Add("IsCreditShared", baCredit?.MultipleRelatedBAs.ToString());
                cpDto.CustomProperties.Add("PaymentTermsTruck", (paymentTermsTruck != null ? paymentTermsTruck.TrmAbbrvtn : ""));
                cpDto.CustomProperties.Add("PaymentTermsBulk", (paymentTermsBulk != null ? paymentTermsBulk.TrmAbbrvtn : ""));
            }

            Log.Debug($"BAID: {raBa.Baid} CreditLimit: {baCredit?.CreditLimit.ToString()}, ARBalance: {baCredit?.ArBalance?.ToString()}");

            if (cpDto.CustomProperties.TryGetValue("PRIMARYBUSINESSOWNER", out primaryBaOwnerUserId))
            {
                //cpDto.CustomProperties.Remove("PRIMARYBUSINESSOWNER"); 
                if (_contactByUserIdStringDictionary.TryGetValue(primaryBaOwnerUserId, out primaryBaOwnerContact))
                {
                    
                    cpDto.CustomProperties.Add("PrimaryBusinessOwnerName", primaryBaOwnerContact.CntctFrstNme + " " + primaryBaOwnerContact.CntctLstNme);
                }
                cpDto.CustomProperties.Add("PRIMARYBAOWNER", primaryBaOwnerUserId.ToString());
            }

        }

        private string GetCounterPartyCategoryName(BusinessAssociateEntity raBa)
        {
            if (base.GetIntArray("InternalBAIDs").Contains(raBa.Baid))
            {
                return CodeSetFacade.Instance.CounterpartyCategory.Internal.Meaning;
            }
            else if (raBa.BusinessAssociateBrokerDefaults.Any())
            {
                return CodeSetFacade.Instance.CounterpartyCategory.Broker.Meaning;
            }
            else
            {
                return CodeSetFacade.Instance.CounterpartyCategory.Customer.Meaning;
            }
        }

        public static IPathEdge PrefetchGeneralConfig
        {
            get
            {
                return new PathEdge<GeneralConfigurationEntity>(
                    BusinessAssociateEntity.PrefetchPathGeneralConfigurations,
                    gc =>
                        gc.GnrlCnfgTblNme == GeneralConfigurationDataConstants.BusinessAssociate.GnrlCnfgTblNme
                    //&& gc.GnrlCnfgQlfr == ""//GeneralConfigurationDataConstants.BusinessAssociate.GnrlCnfgQlfr.Something
                    );
            }
        }

        public static IPathEdge PrefetchPathBusinessAssociateBrokerDefault
        {
            get
            {
                return new PathEdge<BusinessAssociateBrokerDefaultEntity>(
                    BusinessAssociateEntity.PrefetchPathBusinessAssociateBrokerDefaults,
                    bd => bd.FromDate <= DateTime.Now && bd.ToDate > DateTime.Now); //note: if this logic is removed from the prefetch, it needs to be added to the logic that determines the CounterPartyCategory.
            }
        }

        public IPathEdge PrefetchPathBusinessAssociateRelation =>
                new PathEdge<BusinessAssociateRelationEntity>(
                        BusinessAssociateEntity.PrefetchPathBusinessAssociateRelations);

        public IPathEdge PrefetchContactUsers =>
            new PathEdge<ContactEntity>(UserEntity.PrefetchPathContact);

        public static IPathEdge PrefetchPathBusinessAssociateCreditCustomers => new PathEdge<CreditCustomerEntity>(BusinessAssociateEntity.PrefetchPathBusinessAssociateCreditCustomers);

        public static IPathEdge PrefetchPathUltimateBusinessAssociateCreditCustomers => new PathEdge<CreditCustomerEntity>(BusinessAssociateEntity.PrefetchPathUltimateBusinessAssociateCreditCustomers);

        class BACreditProjection
        {
            public int InternalBaId { get; set; }
            public int ExternalBaId { get; set; }
            public decimal CreditLimit { get; set; }
            public decimal? ArBalance { get; set; }
            public bool MultipleRelatedBAs { get; set; }

        }
    }
}
