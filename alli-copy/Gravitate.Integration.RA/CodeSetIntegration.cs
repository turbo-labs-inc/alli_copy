using Gravitate.Integration.RA.Abstraction;
using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Library;
using Gravitate.WCF.DTO.Integration;
using Gravitate.WCF.Interfaces.Integration;
using log4net;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Gravitate.Integration.RA
{
    public class CodeSetIntegration : BaseIntegration
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CodeSetIntegration));

        private readonly IQueryable<MovementHeaderTypeEntity> _movementHeaderTypeQueryable = null;
        private readonly IQueryable<PricetypeEntity> _priceTypeQueryable = null;
        private readonly ICodeSetIntegrationService _codeSetIntegrationService = null;

        public CodeSetIntegration(IQueryable<MovementHeaderTypeEntity> movementHeaderTypeQueryable, ICodeSetIntegrationService codeSetIntegrationService,
            IQueryable<PricetypeEntity> priceTypeQueryable)
        {
            _movementHeaderTypeQueryable = movementHeaderTypeQueryable;
            _codeSetIntegrationService = codeSetIntegrationService;
            _priceTypeQueryable = priceTypeQueryable;
        }

        public override void Execute(int sourceSystemId, IntegrationStatus status)
        {
            List<CodeSetIntegrationDTO> sets = new List<CodeSetIntegrationDTO>();

            // Method of Transport
            Stopwatch sw = Stopwatch.StartNew();
            var methodsOfTransportation = _movementHeaderTypeQueryable.ToArray();
            log.Info($"RA {nameof(MovementHeaderTypeEntity)} query took {sw.Elapsed.ToString()} for {methodsOfTransportation.Count()} Types");
            sets.Add(GetMethodOfTransportationCodeSet(methodsOfTransportation));

            log.Info($"Calling {nameof(ICodeSetIntegrationService.BulkSyncCodeSets)}");
            _codeSetIntegrationService.BulkSyncCodeSets(sourceSystemId, sets.ToArray());
            log.Info($"Completed call to {nameof(ICodeSetIntegrationService.BulkSyncCodeSets)}");

            // Price Types
            sets = new List<CodeSetIntegrationDTO>();
            sw = Stopwatch.StartNew();
            var priceTypes = _priceTypeQueryable.ToArray();
            log.Info($"RA {nameof(PricetypeEntity)} query took {sw.Elapsed.ToString()} for {priceTypes.Count()} Types");
            sets.Add(GetPriceTypeCodeSet(priceTypes));

            log.Info($"Calling {nameof(ICodeSetIntegrationService.BulkSyncCodeSets)}");
            _codeSetIntegrationService.BulkSyncCodeSets(sourceSystemId, sets.ToArray());
            log.Info($"Completed call to {nameof(ICodeSetIntegrationService.BulkSyncCodeSets)}");


        }

        private static string TransformPriceTypeMeaning(string meaning)
        {
            // these replacements match up on values we've already manually inserted into the database
            var toReplace = new KeyValuePair<string, string>[]
            {
                    new KeyValuePair<string, string>(" ", ""),
                    new KeyValuePair<string, string>("-", "_"),
                    new KeyValuePair<string, string>(".", "_"),
                    new KeyValuePair<string, string>(",", "_"),
                    new KeyValuePair<string, string>("+", "plus"),
            };
            return StringExtensions.CreateValidDotNetName(meaning.FillTokenizedString(toReplace)).Trim();
        }

        public CodeSetIntegrationDTO GetMethodOfTransportationCodeSet(IEnumerable<MovementHeaderTypeEntity> entities)
        {
            return new CodeSetIntegrationDTO()
            {
                CodeSetName = "MethodOfTransportation",
                MatchType = CodeSetIntegrationDTO.CodeValueMatchType.Meaning,
                CodeValues = entities.Select(TranslateEntity).ToArray()
            };
        }

        public CodeValueIntegrationDTO TranslateEntity(MovementHeaderTypeEntity type)
        {
            return new CodeValueIntegrationDTO()
            {
                Meaning = type.MvtHdrTyp.Trim(),
                Display = type.Name.Trim(),
                Description = type.Name.Trim()
            };
        }

        public CodeSetIntegrationDTO GetPriceTypeCodeSet(IEnumerable<PricetypeEntity> entities)
        {
            return new CodeSetIntegrationDTO()
            {
                CodeSetName = "PriceType",
                MatchType = CodeSetIntegrationDTO.CodeValueMatchType.Meaning,
                CodeValues = entities.Select(TranslatePriceTypeEntity).ToArray()
            };
        }

        public CodeValueIntegrationDTO TranslatePriceTypeEntity(PricetypeEntity type)
        {
            return new CodeValueIntegrationDTO()
            {
                Meaning = GetPriceTypeCodeValueMeaning(type),
                Display = type.PrceTpeNme,
                Description = type.PrceTpeNme
            };
        }

        public static string GetPriceTypeCodeValueMeaning(PricetypeEntity type)
        {
            return TransformPriceTypeMeaning(type.PrceTpeNme);
        }
        public static string GetPriceTypeCodeValueMeaning(string meaningToTransform)
        {
            return TransformPriceTypeMeaning(meaningToTransform);
        }
    }
}
