using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   using DataExtractor = Func<int, IEnumerable<double>>;
   using DataTransformer = Func<IEnumerable<double>, double>;
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

      public IEnumerable<double> ConvertAll(int id)
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
      public DataExtractor TanukiDataExtractor { get; private set; }
      public IReadOnlyList<DataTransformer> TanukiDataTransformer { get; private set; }
      public LabelExtractor TanukiLabelExtractor { get; private set; }
      public SabotenCacheExtractor TanukiSabotenCacheExtractor { get; private set; }
      public SabotenCacheLoad TanukiSabotenCacheLoad { get; private set; }

      // TODO Refactor to be able to remove dataSample parameter. We could simply give a parameters count and generate a list of value?
      public TanukiETL(int id, DataExtractor dataExtractor, IReadOnlyList<DataTransformer> dataTransformer, LabelExtractor labelExtractor) : this(dataExtractor(id), dataExtractor, dataTransformer, labelExtractor)
      {
      }

      public TanukiETL(ImmutableList<ImmutableList<double>> dataSamples, ImmutableList<int> labels) : this(dataSamples[0] as IEnumerable<double>, new IndexedDataExtractor(dataSamples).ConvertAll, new PassThroughTransformer(dataSamples[0].Count).ToList(), new IndexedLabelExtractor(labels).ConvertAll)
      {
      }

      private TanukiETL(IEnumerable<double> dataSample, DataExtractor dataExtractor, IReadOnlyList<DataTransformer> dataTransformer, LabelExtractor labelExtractor)
      {
         SimpleSabotenCacheExtractor simpleSabotenCacheExtractor = new();

         TanukiDataExtractor = dataExtractor;
         TanukiDataTransformer = dataTransformer;
         TanukiLabelExtractor = labelExtractor;
         TanukiSabotenCacheExtractor = simpleSabotenCacheExtractor.Extract;
         TanukiSabotenCacheLoad = simpleSabotenCacheExtractor.Load;
      }
   }
}
