using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   // UNDONE Need to be consistent with the concept of Extractor/Transformer...
   using DataExtractor = Func<int, int, IEnumerable<double>>;
   using DataTransformer = Func<int, int, double>;
   using LabelExtractor = Func<int, int>;
   using SabotenCacheExtractor = Func<int, SabotenCache>;
   using SabotenCacheLoad = Action<int, SabotenCache>;

   // TODO Move this to a new file.
   public record IndexedDataExtractor // ncrunch: no coverage
   {
      ImmutableList<ImmutableList<double>> DataSamples;

      public IndexedDataExtractor(ImmutableList<ImmutableList<double>> dataSamples)
      {
         DataSamples = dataSamples;
      }

      // UNDONE Take advantage of the indices parameter
      public IEnumerable<double> ConvertAll(int id, int featureIndex)
      {
         return DataSamples[id];
      }
   }

   public record SimpleSabotenCacheExtractor // ncrunch: no coverage
   {
      ImmutableDictionary<int, SabotenCache> sabotenCache = ImmutableDictionary<int, SabotenCache>.Empty;

      public SimpleSabotenCacheExtractor()
      {
      }

      public SabotenCache Extract(int id)
      {
         if (!sabotenCache.ContainsKey(id))
         {
            sabotenCache = sabotenCache.Add(id, new SabotenCache());
         }

         return sabotenCache[id];
      }

      public void Load(int id, SabotenCache sabotenCache)
      {
         this.sabotenCache = this.sabotenCache.SetItem(id, sabotenCache);
      }
   }

   // TODO Move this to a new file.
   public record IndexedLabelExtractor // ncrunch: no coverage
   {
      ImmutableList<int> Labels;

      public IndexedLabelExtractor(ImmutableList<int> labels)
      {
         Labels = labels;
      }

      public int ConvertAll(int id)
      {
         return Labels[id];
      }
   }

   public sealed record TanukiETL // ncrunch: no coverage
   {
      // UNDONE Remove unused stuff
      // public DataExtractor TanukiDataExtractor { get; private set; }
      // public IReadOnlyList<DataTransformer> TanukiDataTransformer { get; private set; }
      public DataTransformer TanukiDataTransformer { get; private set; }
      public LabelExtractor TanukiLabelExtractor { get; private set; }
      public SabotenCacheExtractor TanukiSabotenCacheExtractor { get; private set; }
      public SabotenCacheLoad TanukiSabotenCacheLoad { get; private set; }
      public int TanukiFeatureCount { get; private set; }

      // public TanukiETL(ImmutableList<ImmutableList<double>> dataSamples, ImmutableList<int> labels) : this(/*new IndexedDataExtractor(dataSamples).ConvertAll, */new PassThroughTransformer(dataSamples[0].Count).DataTransformers.ToList(), new IndexedLabelExtractor(labels).ConvertAll)
      // {
      // }

      public TanukiETL(DataTransformer dataTransformer, LabelExtractor labelExtractor, int featureCount)
      {
         SimpleSabotenCacheExtractor simpleSabotenCacheExtractor = new();

         // TanukiDataExtractor = dataExtractor;
         TanukiDataTransformer = dataTransformer;
         TanukiLabelExtractor = labelExtractor;
         TanukiSabotenCacheExtractor = simpleSabotenCacheExtractor.Extract;
         TanukiSabotenCacheLoad = simpleSabotenCacheExtractor.Load;
         TanukiFeatureCount = featureCount;
      }
   }
}
