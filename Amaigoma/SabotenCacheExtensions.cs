using System;
using System.Collections.Generic;

namespace Amaigoma
{
   using DataTransformer = Converter<IEnumerable<double>, IEnumerable<double>>;

   public static class SabotenCacheExtensions
   {
      // This extension method is necessary to prevent SabotenCache and TanukiTransformers to depend on each other
      public static SabotenCache Prefetch(this SabotenCache sabotenCache, TanukiETL tanukiETL, IEnumerable<double> data, int featureIndex)
      {
         // TODO Maybe the TanukiTransformers should be responsible to do the ET(L) on the data instead of getting its DataTransformer.
         Tuple<Range, DataTransformer> dataTransformer = tanukiETL.DataTransformer(featureIndex);

         IEnumerable<double> transformedData = dataTransformer.Item2(data);

         return sabotenCache.LoadCache(dataTransformer.Item1, transformedData);
      }
   }
}
