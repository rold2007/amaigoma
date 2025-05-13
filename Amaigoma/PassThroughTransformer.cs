using System.Collections.Immutable;

namespace Amaigoma
{
   public record PassThroughTransformer
   {
      ImmutableList<ImmutableList<double>> DataSamples;

      public PassThroughTransformer(ImmutableList<ImmutableList<double>> dataSamples)
      {
         DataSamples = dataSamples;
      }

      public double ConvertAll(int id, int featureIndex)
      {
         return DataSamples[id][featureIndex];
      }

      public int FeaturesCount => DataSamples[0].Count;
   }

   public record PassThroughLabelsTransformer
   {
      ImmutableList<int> Labels;

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