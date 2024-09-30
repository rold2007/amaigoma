using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   using DataTransformer = Converter<IEnumerable<double>, IEnumerable<double>>;

   public static class SabotenCacheExtensions
   {
      public static IEnumerable<SabotenCache> PrefetchAll(this IEnumerable<SabotenCache> dataSamples, TanukiTransformers tanukiTransformers)
      {
         ImmutableList<Tuple<Range, DataTransformer>> dataTransformers = ImmutableList<Tuple<Range, DataTransformer>>.Empty;

         for (int featureIndex = 0; featureIndex < tanukiTransformers.TotalOutputSamples; featureIndex++)
         {
            // TODO The dataTransformers could become huge if TotalOutputSamples is big. Find a better way.
            dataTransformers = dataTransformers.Add(tanukiTransformers.DataTransformer(featureIndex));
         }

         foreach (SabotenCache dataSample in dataSamples)
         {
            SabotenCache extractedDataSample = new SabotenCache(tanukiTransformers.TanukiExtractor(dataSample.Data));

            yield return extractedDataSample.PrefetchAll(dataTransformers);
         }
      }

      public static TrainDataCache PrefetchAll(this TrainDataCache trainDataCache, TanukiTransformers tanukiTransformers)
      {
         return new TrainDataCache(trainDataCache.Samples.PrefetchAll(tanukiTransformers).ToImmutableList<SabotenCache>(), trainDataCache.Labels);
      }

      private static SabotenCache PrefetchAll(this SabotenCache dataSample, ImmutableList<Tuple<Range, DataTransformer>> dataTransformers)
      {
         for (int featureIndex = 0; featureIndex < dataTransformers.Count(); featureIndex++)
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
            SabotenCache extractedDataSample = new SabotenCache(tanukiTransformers.TanukiExtractor(dataSample.Data));

            // TODO Maybe the TanukiTransformers should be responsible to do the ET(L) on the data instead of getting its DataTransformer.
            Tuple<Range, DataTransformer> dataTransformer = tanukiTransformers.DataTransformer(featureIndex);

            IEnumerable<double> transformedData = dataTransformer.Item2(extractedDataSample.Data);

            return extractedDataSample.LoadCache(dataTransformer.Item1, transformedData);
         }
      }

      private static SabotenCache Prefetch(this SabotenCache dataSample, int featureIndex, Tuple<Range, DataTransformer> dataTransformer)
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
