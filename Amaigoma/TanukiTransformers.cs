using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   using DataExtractor = Converter<int, IEnumerable<double>>;
   using DataTransformer = Converter<IEnumerable<double>, IEnumerable<double>>;
   using LabelExtractor = Converter<int, int>;
   using SabotenCacheExtractor = Converter<int, SabotenCache>;
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

   // TODO Move this to a new file.
   public record PassThroughDataTransformer // ncrunch: no coverage
   {
      public PassThroughDataTransformer()
      {
      }

      public IEnumerable<double> ConvertAll(IEnumerable<double> sample)
      {
         return sample;
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

   // TODO Rename to TanukiETL
   public sealed record TanukiTransformers // ncrunch: no coverage
   {
      private readonly ImmutableList<Tuple<Range, DataTransformer>> dataTransformers = ImmutableList<Tuple<Range, DataTransformer>>.Empty;
      private readonly Comparer<Tuple<Range, DataTransformer>> rangeComparer = Comparer<Tuple<Range, DataTransformer>>.Create((x, y) => x.Item1.Start.Value.CompareTo(y.Item1.Start.Value));

      public DataExtractor TanukiDataExtractor { get; private set; }
      public DataTransformer TanukiDataTransformer { get; private set; }
      public LabelExtractor TanukiLabelExtractor { get; private set; }
      public SabotenCacheExtractor TanukiSabotenCacheExtractor { get; private set; }
      public SabotenCacheLoad TanukiSabotenCacheLoad { get; private set; }

      // TODO Refactor to be able to remove dataSample parameter. We could simply give a parameters count and generate a list of value?
      public TanukiTransformers(int id, DataExtractor dataExtractor, DataTransformer dataTransformer, LabelExtractor labelExtractor) : this(dataExtractor(id), dataExtractor, dataTransformer, labelExtractor)
      {
      }

      public TanukiTransformers(ImmutableList<ImmutableList<double>> dataSamples, ImmutableList<int> labels) : this(dataSamples[0] as IEnumerable<double>, new IndexedDataExtractor(dataSamples).ConvertAll, new PassThroughDataTransformer().ConvertAll, new IndexedLabelExtractor(labels).ConvertAll)
      {
      }

      private TanukiTransformers(IEnumerable<double> dataSample, DataExtractor dataExtractor, DataTransformer dataTransformer, LabelExtractor labelExtractor)
      {
         SimpleSabotenCacheExtractor simpleSabotenCacheExtractor = new();

         TanukiDataExtractor = dataExtractor;
         TanukiDataTransformer = dataTransformer;
         TanukiLabelExtractor = labelExtractor;
         TanukiSabotenCacheExtractor = simpleSabotenCacheExtractor.Extract;
         TanukiSabotenCacheLoad = simpleSabotenCacheExtractor.Load;

         Delegate[] dataTransformerDelegates = TanukiDataTransformer.GetInvocationList();

         Index start = new();
         Index end;

         foreach (Delegate dataTransformerDelegate in dataTransformerDelegates)
         {
            DataTransformer converter = dataTransformerDelegate as DataTransformer;
            int transformedDataCount = converter(dataSample).Count();

            end = new Index(start.Value + transformedDataCount);

            dataTransformers = dataTransformers.Add(new Tuple<Range, DataTransformer>(new Range(start, end), converter));

            start = end;
         }
      }

      public Tuple<Range, DataTransformer> DataTransformer(int transformedDataIndex)
      {
         int foundIndex = dataTransformers.BinarySearch(new Tuple<Range, DataTransformer>(new Range(transformedDataIndex, transformedDataIndex), null), rangeComparer);

         if (foundIndex < 0)
         {
            foundIndex = ~foundIndex - 1;
         }

         transformedDataIndex.ShouldBeLessThan(dataTransformers[foundIndex].Item1.End.Value);

         return dataTransformers[foundIndex];
      }

      public int TotalOutputSamples
      {
         get
         {
            return dataTransformers.Last().Item1.End.Value;
         }
      }
   }
}
