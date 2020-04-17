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
         Matrix<double> samples = Matrix<double>.Build.Dense(sampleCount, featureCount);
         Vector<double> labels = Vector<double>.Build.Dense(sampleCount);

         // Sample 0
         samples.At(0, 0, 2.0);
         samples.At(0, 1, 90.0);

         // Sample 1
         samples.At(1, 0, 250.0);
         samples.At(1, 1, 140.0);

         // Sample 2
         samples.At(2, 0, 200.0);
         samples.At(2, 1, 100.0);

         labels.At(0, 42);
         labels.At(1, 54);
         labels.At(2, 42);

         pakiraGenerator.Generate(pakiraDecisionTreeModel, samples, labels);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.Predict(samples.Row(0)).ShouldBe(labels.At(0));
         pakiraDecisionTreeModel.Predict(samples.Row(1)).ShouldBe(labels.At(1));
         pakiraDecisionTreeModel.Predict(samples.Row(2)).ShouldBe(labels.At(2));
      }

      [Fact]
      public void MinimumSampleCount()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel();
         const int featureCount = 2;
         const int sampleCount = 3;
         Matrix<double> samples = Matrix<double>.Build.Dense(sampleCount, featureCount);
         Vector<double> labels = Vector<double>.Build.Dense(sampleCount);

         // Sample 0
         samples.At(0, 0, 2.0);
         samples.At(0, 1, 3.0);

         // Sample 1
         samples.At(1, 0, 20.0);
         samples.At(1, 1, 140.0);

         // Sample 2
         samples.At(2, 0, 33.0);
         samples.At(2, 1, 200.0);

         labels.At(0, 42);
         labels.At(1, 54);
         labels.At(2, 42);

         pakiraGenerator.Generate(pakiraDecisionTreeModel, samples, labels);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.Predict(samples.Row(0)).ShouldBe(labels.At(0));
         pakiraDecisionTreeModel.Predict(samples.Row(1)).ShouldBe(labels.At(1));
         pakiraDecisionTreeModel.Predict(samples.Row(2)).ShouldBe(labels.At(2));
      }

      static public PakiraDecisionTreeGenerator CreatePakiraGeneratorInstance()
      {
         return new PakiraDecisionTreeGenerator();
      }
   }
}
