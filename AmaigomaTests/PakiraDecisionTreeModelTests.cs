namespace AmaigomaTests
{
   using Amaigoma;
   using System;
   using Xunit;

   public class PakiraNodeTests
   {
      [Fact]
      public void ValueException()
      {
         PakiraNode pakiraNode = new PakiraNode(0, 0.0);

         Assert.Throws<InvalidOperationException>(() => pakiraNode.Value);
      }
   }

   public class PakiraLeafTests
   {
      [Fact]
      public void ColumnException()
      {
         PakiraLeaf pakiraLeaf = new PakiraLeaf(0.0);

         Assert.Throws<InvalidOperationException>(() => pakiraLeaf.Column);
      }

      [Fact]
      public void ThresholdException()
      {
         PakiraLeaf pakiraLeaf = new PakiraLeaf(0.0);

         Assert.Throws<InvalidOperationException>(() => pakiraLeaf.Threshold);
      }
   }
}
