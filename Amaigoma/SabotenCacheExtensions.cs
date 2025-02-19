using System;
using System.Collections.Generic;

namespace Amaigoma
{
   using DataTransformer = Func<IEnumerable<double>, double>;

   public static class SabotenCacheExtensions
   {
      // This extension method is necessary to prevent SabotenCache and TanukiTransformers to depend on each other
      public static SabotenCache Prefetch(this SabotenCache sabotenCache, TanukiETL tanukiETL, int id, int featureIndex)
      {
         // UNDONE Need to fetch only the data needed for this specific feature inseatd of the full data
         IEnumerable<double> data = tanukiETL.TanukiDataExtractor(id);
         IEnumerable<int> dataIndices = tanukiETL.TanukiDataTransformerIndices[featureIndex](featureIndex);

         // TODO Maybe the TanukiTransformers should be responsible to do the ET(L) on the data instead of getting its DataTransformer.
         DataTransformer dataTransformer = tanukiETL.TanukiDataTransformer[featureIndex];

         double transformedData = dataTransformer(data);

         return sabotenCache.LoadCache(featureIndex, transformedData);
      }
   }
}
