using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   // TODO Use Skia to add more advanced features ?
   public sealed record AverageTransformer // ncrunch: no coverage
   {
      // UNDONE This should not be hardcoded here
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

      // TODO Use Benchmark.net to try to improve the benchmark of this method
      public IEnumerable<double> ConvertAll(IEnumerable<double> list)
      {
         ImmutableList<double> features = ImmutableList<double>.Empty;

         const int sizeX = FeatureWindowSize;
         const int sizeY = FeatureWindowSize;
         const int width = sizeX + 1;
         double[] integral = list.ToArray();
         double sum;

         for (int y = 0; y <= (sizeY - WindowSize); y += WindowSize)
         {
            int topOffsetY = (width * y);
            int bottomOffsetY = width * (y + WindowSize);

            for (int x = 0; x <= (sizeX - WindowSize); x += WindowSize)
            {
               int rightX = x + WindowSize;

               // UNDONE All these indices could be precomputed in the constructor. The loop would be a lot simpler.
               sum = integral[rightX + bottomOffsetY];
               sum -= integral[x + bottomOffsetY];
               sum -= integral[rightX + topOffsetY];
               sum += integral[x + topOffsetY];

               features = features.Add(sum * WindowSizeSquaredInverted);
            }
         }

         return features;
      }
   }
}
