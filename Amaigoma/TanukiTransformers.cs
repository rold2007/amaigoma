using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   public sealed class TanukiTransformers
   {
      private readonly ImmutableList<Tuple<Range, Converter<IEnumerable<double>, IEnumerable<double>>>> dataTransformers = ImmutableList<Tuple<Range, Converter<IEnumerable<double>, IEnumerable<double>>>>.Empty;
      private readonly Comparer<Tuple<Range, Converter<IEnumerable<double>, IEnumerable<double>>>> rangeComparer = Comparer<Tuple<Range, Converter<IEnumerable<double>, IEnumerable<double>>>>.Create((x, y) => x.Item1.Start.Value.CompareTo(y.Item1.Start.Value));

      public TanukiTransformers(Converter<IEnumerable<double>, IEnumerable<double>> converters, IEnumerable<double> dataSample)
      {
         Delegate[] delegates = converters.GetInvocationList();

         Index start = new();
         Index end;

         foreach (Delegate dataTransformer in delegates)
         {
            Converter<IEnumerable<double>, IEnumerable<double>> converter = dataTransformer as Converter<IEnumerable<double>, IEnumerable<double>>;
            int transformedDataCount = converter(dataSample).Count();

            end = new Index(start.Value + transformedDataCount);

            dataTransformers = dataTransformers.Add(new Tuple<Range, Converter<IEnumerable<double>, IEnumerable<double>>>(new Range(start, end), converter));

            start = end;
         }
      }

      public Tuple<Range, Converter<IEnumerable<double>, IEnumerable<double>>> DataTransformer(int transformedDataIndex)
      {
         int foundIndex = dataTransformers.BinarySearch(new Tuple<Range, Converter<IEnumerable<double>, IEnumerable<double>>>(new Range(transformedDataIndex, transformedDataIndex), null), rangeComparer);

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
