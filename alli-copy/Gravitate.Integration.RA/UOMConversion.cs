using Gravitate.Integration.RA.DAL.EntityClasses;
using Gravitate.Integration.RA.Windsor;
using Gravitate.Library;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;

namespace Gravitate.Integration.RA
{
    public class UomConversionCache
    {
        public const string CacheKey = "Gravitate.Integration.RA.UOMConversionCache";

        private static readonly ILog Log = LogManager.GetLogger(typeof(UomConversionCache));

        //If you need to Mock this class, probably easiest to stick a mock object in MemoryCache.Default using the CacheKey.
        public static UomConversionCache Instance
        {
            get
            {
                UomConversionCache cache = (UomConversionCache)MemoryCache.Default.Get(CacheKey);
                if (cache == null)
                {
                    cache = new UomConversionCache();

                    IQueryable<vUOMConversionEntity> queryable = IntegrationContainer.Instance.Resolve<IQueryable<vUOMConversionEntity>>();
                    try
                    {
                        cache.AllUoMConversions = queryable.ToArray();

                        cache.MappingDictionary = cache.AllUoMConversions.ToDictionary(key => new Tuple<int, int>(key.FromUom, key.ToUom), val => val);

                        MemoryCache.Default.Add(CacheKey, cache, DateTimeOffset.Now.AddMinutes(ApplicationSettingsManager.DefaultCacheTimeoutInMinutes));
                    }
                    finally
                    {
                        IntegrationContainer.Instance.Release(queryable);
                    }
                }

                return cache;
            }
        }

        protected IEnumerable<vUOMConversionEntity> AllUoMConversions { get; set; }

        protected IDictionary<Tuple<int, int>, vUOMConversionEntity> MappingDictionary { get; set; }

        public void Clear()
        {
            MemoryCache.Default.Remove(CacheKey);
        }

        public decimal ConvertValue(decimal? value, int fromUom, int toUom, decimal? specificGravity)
        {
            if (!value.HasValue) return 0;
            if (fromUom == toUom) return value.Value;
            decimal specificGravityFactor = 1;
            string conversionType = GetConversionType(fromUom, toUom);

            switch (conversionType)
            {
                case "VW":
                    specificGravityFactor = specificGravity ?? specificGravityFactor;
                    break;
                case "WV":
                    specificGravityFactor = 1 / (specificGravity ?? specificGravityFactor);
                    break;
                default:
                    break;
            }

            return value.Value * Convert.ToDecimal(MappingDictionary[new Tuple<int, int>(fromUom, toUom)].ConversionFactor) * specificGravityFactor;
        }

        private string GetConversionType(int fromUom, int toUom)
        {
            try
            {
                var key = new Tuple<int, int>(fromUom, toUom);
                var value = MappingDictionary[key];
                return value.FromUomtpe + value.ToUomtpe;
            }
            catch (KeyNotFoundException knf)
            {
                Log.Error($"Key not found (fromUom: {fromUom}, toUom {toUom})", knf);
                throw;
            }
        }
    }
}
