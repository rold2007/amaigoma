namespace AmaigomaTests
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Text;
   using System.Threading.Tasks;
   using Amaigoma;
   using MathNet.Numerics.LinearAlgebra;
   using Shouldly;
   using Xunit;

   public class PakiraTreeTests
   {
      [Fact]
      public void Constructor()
      {
         PakiraTree pakiraTree = PakiraTree.Empty;

         pakiraTree.Root.ShouldBeNull();
      }

      [Fact]
      public void AddNode()
      {
         PakiraTree pakiraTree = PakiraTree.Empty;
         PakiraNode root = new PakiraNode(42, 69);
         PakiraTree left = PakiraTree.Empty.AddLeaf(new PakiraLeaf(6));
         PakiraTree right = PakiraTree.Empty.AddLeaf(new PakiraLeaf(9));

         pakiraTree = pakiraTree.AddNode(root, left, right);

         pakiraTree.Root.ShouldBe(root);
         pakiraTree.GetNodes().Count.ShouldBe(3);
         pakiraTree.GetParentNode(root).ShouldBeNull();
      }

      [Fact]
      public void AddNodeComplex()
      {
         PakiraNode root = new PakiraNode(42, 69);
         PakiraTree left = PakiraTree.Empty.AddNode(new PakiraNode(6, 7), PakiraTree.Empty.AddLeaf(new PakiraLeaf(8)), PakiraTree.Empty.AddLeaf(new PakiraLeaf(9)));
         PakiraTree right = PakiraTree.Empty.AddNode(new PakiraNode(10, 11), PakiraTree.Empty.AddLeaf(new PakiraLeaf(12)), PakiraTree.Empty.AddLeaf(new PakiraLeaf(13)));

         PakiraTree pakiraTree = PakiraTree.Empty.AddNode(root, left, right);

         pakiraTree.Root.ShouldBe(root);
         pakiraTree.GetNodes().Count.ShouldBe(7);
         pakiraTree.GetParentNode(left.Root).ShouldBe(root);
         pakiraTree.GetParentNode(right.Root).ShouldBe(root);
      }
   }
}
