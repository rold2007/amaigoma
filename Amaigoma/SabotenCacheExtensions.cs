namespace Amaigoma
{
   public static class SabotenCacheExtensions
   {
      // This extension method is necessary to prevent SabotenCache and TanukiTransformers to depend on each other
      public static SabotenCache Prefetch(this SabotenCache sabotenCache, TanukiETL tanukiETL, int id, int featureIndex)
      {
         return sabotenCache.LoadCache(featureIndex, tanukiETL.TanukiDataTransformer(id, featureIndex));
      }

      public static SabotenCache PrefetchLoad(this SabotenCache sabotenCache, TanukiETL tanukiETL, int id, int featureIndex)
      {
         if (!sabotenCache.CacheHit(featureIndex))
         {
            sabotenCache = sabotenCache.Prefetch(tanukiETL, id, featureIndex);
            tanukiETL.TanukiSabotenCacheLoad(id, sabotenCache);
         }

         return sabotenCache;
      }
   }
}
