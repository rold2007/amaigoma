namespace Amaigoma
{
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Collections.Immutable;
   using System.Linq;

   public sealed class TanukiTransformers
   {
      private readonly ImmutableList<Tuple<Range, Converter<IList<double>, IList<double>>>> dataTransformers = ImmutableList<Tuple<Range, Converter<IList<double>, IList<double>>>>.Empty;
      private readonly Comparer<Tuple<Range, Converter<IList<double>, IList<double>>>> rangeComparer = Comparer<Tuple<Range, Converter<IList<double>, IList<double>>>>.Create((x, y) => x.Item1.Start.Value.CompareTo(y.Item1.Start.Value));

      public TanukiTransformers(Converter<IList<double>, IList<double>> converters, IList<double> dataSample)
      {
         Delegate[] delegates = converters.GetInvocationList();

         Index start = new Index();
         Index end;

         foreach (Delegate dataTransformer in delegates)
         {
            Converter<IList<double>, IList<double>> converter = dataTransformer as Converter<IList<double>, IList<double>>;
            int transformedDataCount = converter(dataSample).Count();

            end = new Index(start.Value + transformedDataCount);

            dataTransformers = dataTransformers.Add(new Tuple<Range, Converter<IList<double>, IList<double>>>(new Range(start, end), converter));

            start = end;
         }
      }

      public Tuple<Range, Converter<IList<double>, IList<double>>> DataTransformer(int transformedDataIndex)
      {
         int foundIndex = dataTransformers.BinarySearch(new Tuple<Range, Converter<IList<double>, IList<double>>>(new Range(transformedDataIndex, transformedDataIndex), null), rangeComparer);

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
