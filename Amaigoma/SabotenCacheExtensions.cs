namespace Amaigoma
{
   using System;
   using System.Collections.Generic;

   public static class SabotenCacheExtensions
   {
      public static SabotenCache PrefetchAll(this SabotenCache dataSample, TanukiTransformers tanukiTransformers)
      {
         for (int featureIndex = 0; featureIndex < dataSample.Data.Count; featureIndex++)
         {
            dataSample = dataSample.Prefetch(featureIndex, tanukiTransformers);
         }

         return dataSample;
      }

      public static IEnumerable<SabotenCache> PrefetchAll(this IEnumerable<SabotenCache> dataSamples, TanukiTransformers tanukiTransformers)
      {
         List<Tuple<Range, Converter<IList<double>, IList<double>>>> dataTransformers = new List<Tuple<Range, Converter<IList<double>, IList<double>>>>();

         for (int featureIndex = 0; featureIndex < tanukiTransformers.TotalOutputSamples; featureIndex++)
         {
            dataTransformers.Add(tanukiTransformers.DataTransformer(featureIndex));
         }

         foreach (SabotenCache dataSample in dataSamples)
         {
            yield return dataSample.PrefetchAll(dataTransformers);
         }
      }

      private static SabotenCache PrefetchAll(this SabotenCache dataSample, IList<Tuple<Range, Converter<IList<double>, IList<double>>>> dataTransformers)
      {
         for (int featureIndex = 0; featureIndex < dataTransformers.Count; featureIndex++)
         {
            dataSample = dataSample.Prefetch(featureIndex, dataTransformers[featureIndex]);
         }

         return dataSample;
      }

      public static SabotenCache Prefetch(this SabotenCache dataSample, int featureIndex, TanukiTransformers tanukiTransformers)
      {
         if (dataSample.CacheHit(featureIndex))
         {
            return dataSample;
         }
         else
         {
            Tuple<Range, Converter<IList<double>, IList<double>>> dataTransformer = tanukiTransformers.DataTransformer(featureIndex);
            IEnumerable<double> transformedData = dataTransformer.Item2(dataSample.Data);

            return dataSample.LoadCache(dataTransformer.Item1, transformedData);
         }
      }

      public static IEnumerable<SabotenCache> Prefetch(this IEnumerable<SabotenCache> dataSamples, int featureIndex, TanukiTransformers tanukiTransformers)
      {
         Tuple<Range, Converter<IList<double>, IList<double>>> dataTransformer = tanukiTransformers.DataTransformer(featureIndex);

         foreach (SabotenCache dataSample in dataSamples)
         {
            yield return dataSample.Prefetch(featureIndex, dataTransformer);
         }
      }

      private static SabotenCache Prefetch(this SabotenCache dataSample, int featureIndex, Tuple<Range, Converter<IList<double>, IList<double>>> dataTransformer)
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
   }
}
