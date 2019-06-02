namespace AmaigomaTests
{
   using Amaigoma;
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
         List<int[]> samples = new List<int[]>();

         pakiraGenerator.Generate(samples);
      }

      static public PakiraGenerator CreatePakiraGeneratorInstance()
      {
         return new PakiraGenerator();
      }
   }
}
