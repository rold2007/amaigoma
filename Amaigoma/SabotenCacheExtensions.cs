namespace Amaigoma
{
   using System;
   using System.Collections.Generic;

   public static class SabotenCacheExtensions
   {
      public static SabotenCache Prefetch(this SabotenCache dataSample, int featureIndex, TanukiTransformers theTransformers)
      {
         if (dataSample.CacheHit(featureIndex))
         {
            return dataSample;
         }
         else
         {
            Tuple<Range, Converter<IList<double>, IList<double>>> dataTransformer = theTransformers.DataTransformer(featureIndex);
            IEnumerable<double> transformedData = dataTransformer.Item2(dataSample.Data);

            return dataSample.LoadCache(dataTransformer.Item1, transformedData);
         }
      }

      public static SabotenCache Prefetch(this SabotenCache dataSample, int featureIndex, Tuple<Range, Converter<IList<double>, IList<double>>> dataTransformer)
      {
         if (dataSample.CacheHit(featureIndex))
         {
            return dataSample;
         }
         else
         {
            IEnumerable<double> transformedData = dataTransformer.Item2(dataSample.Data);

            return dataSample.LoadCache(dataTransformer.Item1, transformedData);
         }
      }

      public static IEnumerable<SabotenCache> Prefetch(this IEnumerable<SabotenCache> dataSamples, int featureIndex, TanukiTransformers theTransformers)
      {
         Tuple<Range, Converter<IList<double>, IList<double>>> dataTransformer = theTransformers.DataTransformer(featureIndex);

         foreach (SabotenCache dataSample in dataSamples)
         {
            yield return dataSample.Prefetch(featureIndex, dataTransformer);
         }
      }
   }
}
