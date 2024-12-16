using System;
using System.Collections.Generic;

namespace Amaigoma
{
   using DataTransformer = Converter<IEnumerable<double>, IEnumerable<double>>;

   public static class SabotenCacheExtensions
   {
      // TODO This extension method could be moved inside a static method of sabotencache and get rid of this extension class
      public static SabotenCache Prefetch(this SabotenCache sabotenCache, TanukiTransformers tanukiTransformers, IEnumerable<double> data, int featureIndex)
      {
         // TODO Maybe the TanukiTransformers should be responsible to do the ET(L) on the data instead of getting its DataTransformer.
         Tuple<Range, DataTransformer> dataTransformer = tanukiTransformers.DataTransformer(featureIndex);

         IEnumerable<double> transformedData = dataTransformer.Item2(data);

         return sabotenCache.LoadCache(dataTransformer.Item1, transformedData);
      }
   }
}
