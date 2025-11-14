using System;
using System.Collections.Immutable;

namespace Amaigoma
{
   using DataTransformer = Func<int, int, int>;
   using LabelExtractor = Func<int, int>;

   // TODO Rename TanukiETL to something clearer like DataETL
   public sealed record TanukiETL // ncrunch: no coverage
   {
      public DataTransformer TanukiDataTransformer { get; private set; }
      public LabelExtractor TanukiLabelExtractor { get; private set; }
      public int TanukiFeatureCount { get; private set; }

      public TanukiETL(ImmutableList<ImmutableList<int>> dataSamples, ImmutableList<int> labels) : this(new PassThroughTransformer(dataSamples).ConvertAll, new PassThroughLabelsTransformer(labels).ConvertAll, dataSamples[0].Count)
      {
      }

      public TanukiETL(DataTransformer dataTransformer, LabelExtractor labelExtractor, int featureCount)
      {
         TanukiDataTransformer = dataTransformer;
         TanukiLabelExtractor = labelExtractor;
         TanukiFeatureCount = featureCount;
      }
   }
}
