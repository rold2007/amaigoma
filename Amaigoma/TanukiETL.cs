using System;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   using DataTransformer = Func<int, int, int>;
   using LabelExtractor = Func<int, int>;

   // TODO Rename TanukiETL to something clearer like DataETL
   public sealed record TanukiETL
   {
      private ImmutableList<DataTransformer> TanukiDataTransformers { get; set; } = ImmutableList<DataTransformer>.Empty;
      public LabelExtractor TanukiLabelExtractor { get; private set; }
      private ImmutableList<int> TanukiFeatureCounts { get; set; } = ImmutableList<int>.Empty.Add(0);
      private ImmutableList<int> TransformerIndex { get; set; } = ImmutableList<int>.Empty;

      public int TanukiFeatureCount
      {
         get
         {
            return TanukiFeatureCounts.Last();
         }
      }

      public TanukiETL(ImmutableList<ImmutableList<int>> dataSamples, ImmutableList<int> labels) : this(new PassThroughTransformer(dataSamples).ConvertAll, new PassThroughLabelsTransformer(labels).ConvertAll, dataSamples[0].Count)
      {
      }

      public TanukiETL(DataTransformer dataTransformer, LabelExtractor labelExtractor, int featureCount)
      {
         TransformerIndex = TransformerIndex.AddRange(Enumerable.Repeat(TanukiDataTransformers.Count, featureCount));

         TanukiDataTransformers = TanukiDataTransformers.Add(dataTransformer);
         TanukiLabelExtractor = labelExtractor;
         TanukiFeatureCounts = TanukiFeatureCounts.Add(featureCount);
      }

      private TanukiETL(ImmutableList<DataTransformer> dataTransformers, LabelExtractor labelExtractor, ImmutableList<int> featureCount, ImmutableList<int> transformerIndex)
      {
         TanukiDataTransformers = dataTransformers;
         TanukiLabelExtractor = labelExtractor;
         TanukiFeatureCounts = featureCount;
         TransformerIndex = transformerIndex;
      }

      public TanukiETL AddDataTransformer(DataTransformer dataTransformer, int featureCount)
      {
         return new(
            TanukiDataTransformers.Add(dataTransformer),
            TanukiLabelExtractor,
            TanukiFeatureCounts.Add(TanukiFeatureCount + featureCount),
            TransformerIndex.AddRange(Enumerable.Repeat(TanukiDataTransformers.Count, featureCount)));
      }

      public int TanukiDataTransformer(int id, int featureIndex)
      {
         int transformerIndex = TransformerIndex[featureIndex];

         return TanukiDataTransformers[transformerIndex](id, featureIndex - TanukiFeatureCounts[transformerIndex]);
      }
   }
}
