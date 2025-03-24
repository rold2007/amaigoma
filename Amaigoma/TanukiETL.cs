using System;
using System.Collections.Immutable;

namespace Amaigoma
{
   using DataTransformer = Func<int, int, double>;
   using LabelExtractor = Func<int, int>;
   using SabotenCacheExtractor = Func<int, SabotenCache>;
   using SabotenCacheLoad = Action<int, SabotenCache>;

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

   public sealed record TanukiETL // ncrunch: no coverage
   {
      public DataTransformer TanukiDataTransformer { get; private set; }
      public LabelExtractor TanukiLabelExtractor { get; private set; }
      public SabotenCacheExtractor TanukiSabotenCacheExtractor { get; private set; }
      public SabotenCacheLoad TanukiSabotenCacheLoad { get; private set; }
      public int TanukiFeatureCount { get; private set; }

      public TanukiETL(ImmutableList<ImmutableList<double>> dataSamples, ImmutableList<int> labels) : this(new PassThroughTransformer(dataSamples).ConvertAll, new PassThroughLabelsTransformer(labels).ConvertAll, dataSamples[0].Count)
      {
      }

      public TanukiETL(DataTransformer dataTransformer, LabelExtractor labelExtractor, int featureCount)
      {
         SimpleSabotenCacheExtractor simpleSabotenCacheExtractor = new();

         TanukiDataTransformer = dataTransformer;
         TanukiLabelExtractor = labelExtractor;
         TanukiSabotenCacheExtractor = simpleSabotenCacheExtractor.Extract;
         TanukiSabotenCacheLoad = simpleSabotenCacheExtractor.Load;
         TanukiFeatureCount = featureCount;
      }
   }
}
