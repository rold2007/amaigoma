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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
      }

      [Fact]
      public void Generate()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel();
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

         pakiraGenerator.Generate(pakiraDecisionTreeModel, samples, labels);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.Predict(samples.Column(0)).ShouldBe(labels.At(0));
         pakiraDecisionTreeModel.Predict(samples.Column(1)).ShouldBe(labels.At(1));
         pakiraDecisionTreeModel.Predict(samples.Column(2)).ShouldBe(labels.At(2));
      }

      static public PakiraDecisionTreeGenerator CreatePakiraGeneratorInstance()
      {
         return new PakiraDecisionTreeGenerator();
      }
   }
}
