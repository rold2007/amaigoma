namespace AmaigomaTests
{
   using Amaigoma;
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

      [Fact]
      public void AddNodeTopBottom()
      {
         PakiraNode rootNode = new PakiraNode(6, 7);
         PakiraLeaf leftLeaf = new PakiraLeaf(8);
         PakiraLeaf rightLeaf = new PakiraLeaf(9);
         PakiraTree pakiraTree = PakiraTree.Empty.AddNode(rootNode, leftLeaf, rightLeaf);

         PakiraNode subNode = new PakiraNode(10, 11);

         PakiraLeaf newLeftLeaf = new PakiraLeaf(12);
         PakiraLeaf newRightLeaf = new PakiraLeaf(13);

         pakiraTree = pakiraTree.ReplaceLeaf(rightLeaf, PakiraTree.Empty.AddNode(subNode, newLeftLeaf, newRightLeaf));

         subNode = new PakiraNode(14, 15);
         newLeftLeaf = new PakiraLeaf(16);
         newRightLeaf = new PakiraLeaf(17);

         pakiraTree = pakiraTree.ReplaceLeaf(leftLeaf, PakiraTree.Empty.AddNode(subNode, newLeftLeaf, newRightLeaf));

         pakiraTree.Root.ShouldBe(rootNode);
         pakiraTree.GetNodes().Count.ShouldBe(7);
      }
   }
}
