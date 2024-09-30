using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   using DataTransformer = Converter<IEnumerable<double>, IEnumerable<double>>;

   public sealed record TanukiTransformers // ncrunch: no coverage
   {
      private static readonly PassThroughTransformer DefaultDataExtractor = new(); // ncrunch: no coverage
      private readonly ImmutableList<Tuple<Range, DataTransformer>> dataTransformers = ImmutableList<Tuple<Range, DataTransformer>>.Empty;
      private readonly Comparer<Tuple<Range, DataTransformer>> rangeComparer = Comparer<Tuple<Range, DataTransformer>>.Create((x, y) => x.Item1.Start.Value.CompareTo(y.Item1.Start.Value));

      public DataTransformer TanukiExtractor { get; private set; }

      public TanukiTransformers(DataTransformer converters, IEnumerable<double> dataSample) : this(converters, dataSample, DefaultDataExtractor.ConvertAll)
      {
      }

      // TODO Refactor to be able to remove dataSample parameter. We could simply give a parameters count and generate a list of value?
      public TanukiTransformers(DataTransformer converters, IEnumerable<double> dataSample, DataTransformer extractor)
      {
         TanukiExtractor = extractor;

         Delegate[] delegates = converters.GetInvocationList();

         Index start = new();
         Index end;

         IEnumerable<double> extractedDataSample = extractor(dataSample);

         foreach (Delegate dataTransformer in delegates)
         {
            DataTransformer converter = dataTransformer as DataTransformer;
            int transformedDataCount = converter(extractedDataSample).Count();

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
