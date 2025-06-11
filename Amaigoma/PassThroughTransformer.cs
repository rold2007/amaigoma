using System.Collections.Immutable;

namespace Amaigoma
{
   public record PassThroughTransformer
   {
      readonly ImmutableList<ImmutableList<int>> DataSamples;

      public PassThroughTransformer(ImmutableList<ImmutableList<int>> dataSamples)
      {
         DataSamples = dataSamples;
      }

      public int ConvertAll(int id, int featureIndex)
      {
         return DataSamples[id][featureIndex];
      }

      public int FeaturesCount => DataSamples[0].Count;
   }

   public record PassThroughLabelsTransformer
   {
      readonly ImmutableList<int> Labels;

      public PassThroughLabelsTransformer(ImmutableList<int> labels)
      {
         Labels = labels;
      }

      public int ConvertAll(int id)
      {
         return Labels[id];
      }
   }
}