namespace AmaigomaTests
{
   using Amaigoma;
   using MathNet.Numerics.LinearAlgebra;
   using Shouldly;
   using Xunit;

   public class PakiraGeneratorTests
   {
      [Fact]
      public void Constructor()
      {
         PakiraGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
      }

      [Fact]
      public void Generate()
      {
         PakiraGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         PakiraModel pakiraModel = new PakiraModel();
         const int featureCount = 2;
         const int sampleCount = 3;
         Matrix<double> samples = Matrix<double>.Build.Dense(featureCount, sampleCount);
         Vector<double> labels = Vector<double>.Build.Dense(sampleCount);

         // Sample 0
         samples.At(0, 0, 2.0);
         samples.At(1, 0, 3.0);

         // Sample 1
         samples.At(0, 1, 128.0);
         samples.At(1, 1, 140.0);

         // Sample 2
         samples.At(0, 2, 33.0);
         samples.At(1, 2, 200.0);

         labels.At(0, 42);
         labels.At(1, 54);
         labels.At(2, 42);

         pakiraGenerator.Generate(pakiraModel, samples, labels);

         pakiraModel.Tree.Root.ShouldNotBeNull();
      }

      static public PakiraGenerator CreatePakiraGeneratorInstance()
      {
         return new PakiraGenerator();
      }
   }
}
