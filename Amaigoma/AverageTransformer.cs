using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   // TODO Use Skia to add more advanced features ?
   public sealed record AverageTransformer // ncrunch: no coverage
   {
      // TODO This should not be hardcoded here
      public const int FeatureWindowSize = 17;

      private int WindowSize
      {
         get;
      }

      private double WindowSizeSquaredInverted
      {
         get;
      }

      public AverageTransformer(int windowSize)
      {
         WindowSize = windowSize;
         WindowSizeSquaredInverted = 1.0 / (windowSize * windowSize);
      }

      // UNDONE Use Benchmark.net to try to improve the benchmark of this method
      public IEnumerable<double> ConvertAll(IEnumerable<double> list)
      {
         ImmutableList<double> features = ImmutableList<double>.Empty;

         const int sizeX = FeatureWindowSize;
         const int sizeY = FeatureWindowSize;
         const int width = sizeX + 1;

         double[] integral = list.ToArray();
         double sum;

         // TODO These loops can be simplified (remove the -1 everywhere). But better to have a sturdy unit test before.
         for (int y = 1; y <= (sizeY - WindowSize + 1); y += WindowSize)
         {
            int topY = (y - 1);
            int bottomY = (y + WindowSize - 1);

            for (int x = 1; x <= (sizeX - WindowSize + 1); x += WindowSize)
            {
               int leftX = x - 1;
               int rightX = x + WindowSize - 1;

               sum = integral[rightX + (width * bottomY)];
               sum -= integral[leftX + (width * bottomY)];
               sum -= integral[rightX + (width * topY)];
               sum += integral[leftX + (width * topY)];

               features = features.Add(sum * WindowSizeSquaredInverted);
            }
         }

         return features;
      }
   }
}
