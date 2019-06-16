namespace AmaigomaTests
{
   using Amaigoma;
   using MathNet.Numerics.LinearAlgebra;
   using numl.Model;
   using Shouldly;
   using System.Collections.Generic;
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
         PakiraDescriptor pakiraDescriptor = new PakiraDescriptor();
         TestDataDistributionProvider testDataDistributionProvider = new TestDataDistributionProvider();
         Matrix<double> samples = Matrix<double>.Build.Dense(2, 2);
         Vector<double> labels = Vector<double>.Build.Dense(2);
         StringProperty labelsProperty = new StringProperty();

         labelsProperty.Dictionary = new string[] { "Label1", "Label2" };
         pakiraDescriptor.Label = labelsProperty;

         pakiraModel.Descriptor = pakiraDescriptor;

         samples.At(0, 0, 0.0);
         samples.At(0, 1, 0.0);
         samples.At(1, 0, 1.0);
         samples.At(1, 1, 1.0);

         pakiraGenerator.Generate(pakiraModel, testDataDistributionProvider, samples, labels);

         pakiraModel.Tree.Root.ShouldNotBeNull();
      }

      static public PakiraGenerator CreatePakiraGeneratorInstance()
      {
         return new PakiraGenerator();
      }
   }

   internal class TestDataDistributionProvider : IDataProvider
   {
   }
}
