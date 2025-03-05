using System;
using System.Collections.Generic;

namespace Amaigoma
{
   public static class SabotenCacheExtensions
   {
      // This extension method is necessary to prevent SabotenCache and TanukiTransformers to depend on each other
      public static SabotenCache Prefetch(this SabotenCache sabotenCache, TanukiETL tanukiETL, int id, int featureIndex)
      {
         // TODO Maybe the TanukiTransformers should be responsible to do the ET(L) on the data instead of getting its DataTransformer.
         double transformedData = tanukiETL.TanukiDataTransformer(id, featureIndex);

         return sabotenCache.LoadCache(featureIndex, transformedData);
      }
   }
}
