namespace AmaigomaTests
{
   using Amaigoma;
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
         List<int[]> samples = new List<int[]>();
         StringProperty labelsProperty = new StringProperty();

         labelsProperty.Dictionary = new string[] { "Label1", "Label2" };
         pakiraDescriptor.Label = labelsProperty;

         pakiraModel.Descriptor = pakiraDescriptor;

         samples.Add(new int[] { 0, 0 });
         samples.Add(new int[] { 1, 1 });

         pakiraGenerator.Generate(pakiraModel, samples);

         pakiraModel.Tree.Root.ShouldNotBeNull();
      }

      static public PakiraGenerator CreatePakiraGeneratorInstance()
      {
         return new PakiraGenerator();
      }
   }
}
