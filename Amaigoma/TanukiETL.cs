using System;
using System.Collections.Immutable;

namespace Amaigoma
{
   // UNDONE DataTransformer should return an int instead of a double
   using DataTransformer = Func<int, int, double>;
   using LabelExtractor = Func<int, int>;

   public record SimpleSabotenCacheExtractor // ncrunch: no coverage
   {
      public SimpleSabotenCacheExtractor()
      {
      }
   }

   public sealed record TanukiETL // ncrunch: no coverage
   {
      public DataTransformer TanukiDataTransformer { get; private set; }
      public LabelExtractor TanukiLabelExtractor { get; private set; }
      public int TanukiFeatureCount { get; private set; }

      public TanukiETL(ImmutableList<ImmutableList<double>> dataSamples, ImmutableList<int> labels) : this(new PassThroughTransformer(dataSamples).ConvertAll, new PassThroughLabelsTransformer(labels).ConvertAll, dataSamples[0].Count)
      {
      }

      public TanukiETL(DataTransformer dataTransformer, LabelExtractor labelExtractor, int featureCount)
      {
         SimpleSabotenCacheExtractor simpleSabotenCacheExtractor = new();

         TanukiDataTransformer = dataTransformer;
         TanukiLabelExtractor = labelExtractor;
         TanukiFeatureCount = featureCount;
      }
   }
}
